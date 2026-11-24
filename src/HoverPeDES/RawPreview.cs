using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;

namespace HoverPeDES;

/// <summary>
/// Camera RAW files (CR2, CR3, NEF, ARW, DNG, ORF, RW2, RAF ...) almost always carry a full-size JPEG preview.
/// We find the largest valid JPEG inside the file and show that, rotated with the camera's orientation.
/// </summary>
internal static class RawPreview
{
    private const long MaxFile = 256L * 1024 * 1024;
    private const int MinJpeg = 20_000;
    private const int MaxSide = 1800;

    public static Bitmap? Load(string path)
    {
        try { return LoadCore(path); }
        catch (Exception ex) { Log.Write(ex); return null; }
    }

    private static Bitmap? LoadCore(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists || fi.Length > MaxFile || fi.Length < MinJpeg) return null;

        var data = new byte[fi.Length];
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            int off = 0;
            while (off < data.Length)
            {
                int n = fs.Read(data, off, data.Length - off);
                if (n <= 0) break;
                off += n;
            }
        }

        int bestStart = -1, bestLen = 0, pos = 0;
        ReadOnlySpan<byte> pattern = new byte[] { 0xFF, 0xD8, 0xFF };
        while (pos < data.Length - 4)
        {
            int idx = data.AsSpan(pos).IndexOf(pattern);
            if (idx < 0) break;
            int start = pos + idx;
            int len = JpegLength(data, start);
            if (len > 0)
            {
                if (len > bestLen) { bestLen = len; bestStart = start; }
                pos = start + len;
            }
            else
            {
                pos = start + 1;
            }
        }
        if (bestStart < 0 || bestLen < MinJpeg) return null;

        int orientation = TiffOrientation(data);

        using var ms = new MemoryStream(data, bestStart, bestLen, false);
        using var img = Image.FromStream(ms, false, false);
        if (orientation <= 1) orientation = JpegOrientation(img);

        int w = img.Width, h = img.Height;
        double k = Math.Min(1.0, (double)MaxSide / Math.Max(w, h));
        var bmp = new Bitmap(Math.Max(1, (int)(w * k)), Math.Max(1, (int)(h * k)));
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, 0, 0, bmp.Width, bmp.Height);
        }

        switch (orientation)
        {
            case 3: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
            case 6: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
            case 8: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
        }
        return bmp;
    }

    /// <summary>Length of the JPEG starting at <paramref name="start"/>, or -1 if the structure is not valid.</summary>
    private static int JpegLength(byte[] b, int start)
    {
        int i = start + 2;
        int n = b.Length;
        while (i + 3 < n)
        {
            if (b[i] != 0xFF) return -1;
            byte m = b[i + 1];
            if (m == 0xFF) { i++; continue; }
            if (m == 0xD9) return i + 2 - start;                    // EOI
            if (m == 0x01 || (m >= 0xD0 && m <= 0xD7)) { i += 2; continue; }

            int len = (b[i + 2] << 8) | b[i + 3];
            if (len < 2) return -1;

            if (m == 0xDA)                                           // start of scan: skip entropy-coded data
            {
                i += 2 + len;
                while (i + 1 < n)
                {
                    if (b[i] == 0xFF)
                    {
                        byte nx = b[i + 1];
                        if (nx == 0x00 || (nx >= 0xD0 && nx <= 0xD7)) { i += 2; continue; }
                        if (nx == 0xFF) { i++; continue; }
                        break;                                       // next real marker
                    }
                    i++;
                }
                if (i + 1 >= n) return -1;
                continue;
            }
            i += 2 + len;
        }
        return -1;
    }

    /// <summary>EXIF orientation from IFD0 of TIFF-based RAW files (CR2, NEF, ARW, DNG ...). 1 when unknown.</summary>
    private static int TiffOrientation(byte[] b)
    {
        if (b.Length < 16) return 1;
        bool le = b[0] == (byte)'I' && b[1] == (byte)'I';
        bool be = b[0] == (byte)'M' && b[1] == (byte)'M';
        if (!le && !be) return 1;

        int U16(int o) => le ? b[o] | (b[o + 1] << 8) : (b[o] << 8) | b[o + 1];
        long U32(int o) => le
            ? (long)b[o] | ((long)b[o + 1] << 8) | ((long)b[o + 2] << 16) | ((long)b[o + 3] << 24)
            : ((long)b[o] << 24) | ((long)b[o + 1] << 16) | ((long)b[o + 2] << 8) | b[o + 3];

        long ifd = U32(4);
        if (ifd < 8 || ifd + 2 > b.Length) return 1;
        int count = U16((int)ifd);
        for (int i = 0; i < count; i++)
        {
            int e = (int)ifd + 2 + i * 12;
            if (e + 12 > b.Length) break;
            if (U16(e) == 0x0112) return U16(e + 8);
        }
        return 1;
    }

    private static int JpegOrientation(Image img)
    {
        try
        {
            if (img.PropertyIdList.Contains(0x0112))
            {
                var pi = img.GetPropertyItem(0x0112);
                if (pi?.Value != null && pi.Value.Length >= 2) return BitConverter.ToUInt16(pi.Value, 0);
            }
        }
        catch { /* no EXIF */ }
        return 1;
    }
}
