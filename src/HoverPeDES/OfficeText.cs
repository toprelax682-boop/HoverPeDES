using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace HoverPeDES;

/// <summary>
/// Pulls the first part of the text out of Office Open XML (docx, xlsx, pptx) and
/// OpenDocument (odt, ods, odp) files. Pure .NET, no Microsoft Office needed.
/// </summary>
internal static class OfficeText
{
    private const int MaxChars = 6000;
    private const int MaxRows = 60;
    private const int MaxCols = 10;

    private static readonly XmlReaderSettings Xs = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    public static string Extract(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string text = ext switch
        {
            ".docx" or ".docm" => Docx(zip),
            ".xlsx" or ".xlsm" => Xlsx(zip),
            ".pptx" or ".pptm" => Pptx(zip),
            ".odt" or ".ods" or ".odp" => Odf(zip),
            _ => "",
        };
        text = text.Replace("\r", "").TrimEnd();
        if (text.Length > MaxChars) text = text[..MaxChars] + "\n...";
        return text;
    }

    // Streams the XML and yields whole elements with the given local names (children are consumed with them).
    private static IEnumerable<XElement> Elements(Stream s, params string[] localNames)
    {
        using var r = XmlReader.Create(s, Xs);
        r.MoveToContent();
        while (!r.EOF)
        {
            if (r.NodeType == XmlNodeType.Element && localNames.Contains(r.LocalName))
                yield return (XElement)XNode.ReadFrom(r);
            else
                r.Read();
        }
    }

    private static string Para(XElement p)
    {
        var sb = new StringBuilder();
        foreach (var d in p.Descendants())
        {
            switch (d.Name.LocalName)
            {
                case "t": sb.Append(d.Value); break;
                case "tab": sb.Append('\t'); break;
                case "br":
                case "cr": sb.Append('\n'); break;
            }
        }
        return sb.ToString();
    }

    private static string Docx(ZipArchive z)
    {
        var e = z.GetEntry("word/document.xml");
        if (e == null) return "";
        var sb = new StringBuilder();
        using var s = e.Open();
        foreach (var p in Elements(s, "p"))
        {
            sb.Append(Para(p)).Append('\n');
            if (sb.Length > MaxChars) break;
        }
        return sb.ToString();
    }

    private static string Pptx(ZipArchive z)
    {
        var slides = z.Entries
            .Select(x => (entry: x, m: Regex.Match(x.FullName, @"^ppt/slides/slide(\d+)\.xml$")))
            .Where(x => x.m.Success)
            .OrderBy(x => int.Parse(x.m.Groups[1].Value))
            .Take(8)
            .ToList();

        var sb = new StringBuilder();
        foreach (var (entry, m) in slides)
        {
            sb.Append("--- Slide ").Append(m.Groups[1].Value).Append(" ---\n");
            using var s = entry.Open();
            foreach (var p in Elements(s, "p"))
            {
                string t = Para(p);
                if (t.Length > 0) sb.Append(t).Append('\n');
                if (sb.Length > MaxChars) break;
            }
            if (sb.Length > MaxChars) break;
        }
        return sb.ToString();
    }

    private static string Xlsx(ZipArchive z)
    {
        var shared = new List<string>();
        var ss = z.GetEntry("xl/sharedStrings.xml");
        if (ss != null)
        {
            using var s = ss.Open();
            foreach (var si in Elements(s, "si"))
            {
                shared.Add(string.Concat(si.Descendants().Where(d => d.Name.LocalName == "t").Select(d => d.Value)));
                if (shared.Count >= 20000) break;
            }
        }

        var sheet = z.GetEntry("xl/worksheets/sheet1.xml")
            ?? z.Entries.FirstOrDefault(x => x.FullName.StartsWith("xl/worksheets/", StringComparison.Ordinal)
                                             && x.FullName.EndsWith(".xml", StringComparison.Ordinal));
        if (sheet == null) return "";

        var sb = new StringBuilder();
        int rows = 0;
        using var ws = sheet.Open();
        foreach (var row in Elements(ws, "row"))
        {
            var cells = new string?[MaxCols];
            int last = -1;
            foreach (var c in row.Elements().Where(x => x.Name.LocalName == "c"))
            {
                int col = ColIndex((string?)c.Attribute("r"));
                if (col < 0 || col >= MaxCols) continue;

                string type = (string?)c.Attribute("t") ?? "";
                string val;
                if (type == "inlineStr")
                {
                    val = string.Concat(c.Descendants().Where(d => d.Name.LocalName == "t").Select(d => d.Value));
                }
                else
                {
                    string v = c.Elements().FirstOrDefault(d => d.Name.LocalName == "v")?.Value ?? "";
                    if (type == "s") val = int.TryParse(v, out var idx) && idx >= 0 && idx < shared.Count ? shared[idx] : "";
                    else if (type == "b") val = v == "1" ? "TRUE" : "FALSE";
                    else val = v;
                }
                cells[col] = Clip(val.Replace("\n", " "));
                if (val.Length > 0) last = Math.Max(last, col);
            }
            if (last < 0) continue;
            sb.Append(string.Join(" | ", cells.Take(last + 1).Select(x => x ?? ""))).Append('\n');
            if (++rows >= MaxRows) break;
        }
        return sb.ToString();
    }

    private static string Odf(ZipArchive z)
    {
        var e = z.GetEntry("content.xml");
        if (e == null) return "";
        var sb = new StringBuilder();
        int rows = 0;
        using var s = e.Open();
        foreach (var el in Elements(s, "p", "h", "table-row"))
        {
            if (el.Name.LocalName == "table-row")
            {
                var cells = el.Elements()
                    .Where(x => x.Name.LocalName == "table-cell" || x.Name.LocalName == "covered-table-cell")
                    .Take(MaxCols)
                    .Select(x => Clip(x.Value.Replace("\n", " ")))
                    .ToList();
                while (cells.Count > 0 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
                if (cells.Count == 0) continue;
                sb.Append(string.Join(" | ", cells)).Append('\n');
                if (++rows >= MaxRows) break;
            }
            else
            {
                sb.Append(el.Value).Append('\n');
            }
            if (sb.Length > MaxChars) break;
        }
        return sb.ToString();
    }

    private static int ColIndex(string? r)
    {
        if (string.IsNullOrEmpty(r)) return -1;
        int col = 0, i = 0;
        for (; i < r.Length && char.IsLetter(r[i]); i++)
            col = col * 26 + (char.ToUpperInvariant(r[i]) - 'A' + 1);
        return i == 0 ? -1 : col - 1;
    }

    private static string Clip(string s) => s.Length > 22 ? s[..21] + "…" : s;
}
