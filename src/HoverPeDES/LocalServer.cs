using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HoverPeDES;

/// <summary>
/// Tiny loopback-only HTTP server that the browser extension talks to.
/// Only requests whose Origin is a browser-extension origin may change state,
/// so ordinary web pages cannot drive the app.
/// </summary>
internal sealed class LocalServer : IDisposable
{
    public const int Port = 47653;

    public event Action<string, string?>? ShowRequested;   // url, referrer
    public event Action? HideRequested;
    public event Action? PortBusy;

    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();

    public void Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();
        }
        catch (SocketException ex)
        {
            Log.Write(ex);
            PortBusy?.Invoke();
            return;
        }
        _ = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested && _listener != null)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
            catch { break; }
            _ = Task.Run(() => Handle(client));
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = client.SendTimeout = 3000;
                var stream = client.GetStream();
                var (method, path, headers, body) = await ReadRequest(stream);
                if (method == null) return;

                headers.TryGetValue("origin", out var origin);
                headers.TryGetValue("host", out var host);
                bool hostOk = host == $"127.0.0.1:{Port}" || host == $"localhost:{Port}";
                bool extOrigin = origin != null &&
                    (origin.StartsWith("chrome-extension://", StringComparison.Ordinal) ||
                     origin.StartsWith("moz-extension://", StringComparison.Ordinal));

                if (!hostOk) { await Respond(stream, 403, "forbidden", null); return; }
                string? cors = extOrigin ? origin : null;

                if (method == "OPTIONS")
                {
                    await Respond(stream, extOrigin ? 204 : 403, "", cors);
                    return;
                }

                if (method == "GET" && path == "/ping")
                {
                    await Respond(stream, 200, "{\"app\":\"HoverPeDES\",\"enabled\":" +
                        (Settings.Current.ExtensionEnabled ? "true" : "false") + "}", cors);
                    return;
                }

                // State-changing endpoints: extension origin only.
                if (method != "POST" || !extOrigin) { await Respond(stream, 403, "forbidden", cors); return; }
                if (!Settings.Current.ExtensionEnabled) { await Respond(stream, 200, "{\"ok\":false}", cors); return; }

                if (path == "/show")
                {
                    using var doc = JsonDocument.Parse(body);
                    string? url = doc.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
                    string? referrer = doc.RootElement.TryGetProperty("referrer", out var r) ? r.GetString() : null;
                    if (!IsSafeUrl(url)) { await Respond(stream, 400, "{\"ok\":false}", cors); return; }
                    ShowRequested?.Invoke(url!, referrer);
                    await Respond(stream, 200, "{\"ok\":true}", cors);
                }
                else if (path == "/hide")
                {
                    HideRequested?.Invoke();
                    await Respond(stream, 200, "{\"ok\":true}", cors);
                }
                else
                {
                    await Respond(stream, 404, "{\"ok\":false}", cors);
                }
            }
            catch (Exception ex) { Log.Write(ex); }
        }
    }

    private static bool IsSafeUrl(string? url) =>
        url != null && url.Length < 4096 &&
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    private static async Task<(string? method, string path, Dictionary<string, string> headers, string body)>
        ReadRequest(NetworkStream stream)
    {
        var buf = new byte[16 * 1024];
        int len = 0, headerEnd = -1;
        while (headerEnd < 0 && len < buf.Length)
        {
            int n = await stream.ReadAsync(buf.AsMemory(len, buf.Length - len));
            if (n <= 0) break;
            len += n;
            headerEnd = IndexOf(buf, len, "\r\n\r\n");
        }
        if (headerEnd < 0) return (null, "", new(), "");

        var head = Encoding.ASCII.GetString(buf, 0, headerEnd).Split("\r\n");
        var first = head[0].Split(' ');
        if (first.Length < 2) return (null, "", new(), "");

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < head.Length; i++)
        {
            int c = head[i].IndexOf(':');
            if (c > 0) headers[head[i][..c].Trim().ToLowerInvariant()] = head[i][(c + 1)..].Trim();
        }

        int bodyStart = headerEnd + 4;
        int contentLength = headers.TryGetValue("content-length", out var cl) && int.TryParse(cl, out var v) ? v : 0;
        contentLength = Math.Clamp(contentLength, 0, buf.Length - bodyStart);
        while (len - bodyStart < contentLength)
        {
            int n = await stream.ReadAsync(buf.AsMemory(len, buf.Length - len));
            if (n <= 0) break;
            len += n;
        }
        string body = Encoding.UTF8.GetString(buf, bodyStart, Math.Min(contentLength, len - bodyStart));
        string path = first[1].Split('?')[0];
        return (first[0].ToUpperInvariant(), path, headers, body);
    }

    private static int IndexOf(byte[] buf, int len, string needle)
    {
        var n = Encoding.ASCII.GetBytes(needle);
        for (int i = 0; i <= len - n.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < n.Length && ok; j++) ok = buf[i + j] == n[j];
            if (ok) return i;
        }
        return -1;
    }

    private static async Task Respond(NetworkStream stream, int status, string body, string? corsOrigin)
    {
        string reason = status switch { 200 => "OK", 204 => "No Content", 400 => "Bad Request", 403 => "Forbidden", 404 => "Not Found", _ => "Error" };
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {status} {reason}\r\n");
        sb.Append("Content-Type: application/json\r\n");
        sb.Append("Connection: close\r\n");
        if (corsOrigin != null)
        {
            sb.Append($"Access-Control-Allow-Origin: {corsOrigin}\r\n");
            sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type, X-HoverPeDES\r\n");
            sb.Append("Access-Control-Allow-Private-Network: true\r\n");
            sb.Append("Vary: Origin\r\n");
        }
        byte[] payload = Encoding.UTF8.GetBytes(body);
        sb.Append($"Content-Length: {payload.Length}\r\n\r\n");
        byte[] head = Encoding.ASCII.GetBytes(sb.ToString());
        await stream.WriteAsync(head);
        if (payload.Length > 0) await stream.WriteAsync(payload);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
    }
}
