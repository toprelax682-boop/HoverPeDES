using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace HoverPeDES;

/// <summary>
/// Reads the flattened composite image of a Photoshop file (PSD / PSB) WITHOUT loading the layers,
/// so even multi-gigabyte files preview in a moment. Falls back to the JPEG thumbnail stored in the file.
/// </summary>
internal static class PsdPreview
{
    private const int MaxSide = 1600;
    private const long MaxPixels = 400_000_000;

    public static Bitmap? Load(string path)
    {
        try { return LoadCore(path); }
        catch (Exception ex) { Log.Write(ex); return null; }
    }

    private static Bitmap? LoadCore(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        using var br = new BinaryReader(fs);
        if (fs.Length < 40) return null;

        byte[] sig = br.ReadBytes(4);
        if (sig.Length < 4 || sig[0] != 0x38 || sig[1] != 0x42 || sig[2] != 0x50 || sig[3] != 0x53) return null; // "8BPS"
        int version = U16(br);
        if (version != 1 && version != 2) return null;
        br.ReadBytes(6);
        int channels = U16(br);
        long height = U32(br);
        long width = U32(br);
        int depth = U16(br);
        int mode = U16(br);

        long colorLen = U32(br);
        fs.Seek(colorLen, SeekOrigin.Current);

        long resLen = U32(br);
        long resStart = fs.Position;
        fs.Seek(resLen, SeekOrigin.Current);

        long layerLen = version == 1 ? U32(br) : (long)U64(br);
        fs.Seek(layerLen, SeekOrigin.Current);          // skip every layer without reading it
        if (fs.Position + 2 > fs.Length) return Thumbnail(br, fs, resStart, resLen);

        int comp = U16(br);
        Bitmap? bmp = null;
        try
        {
            if (width > 0 && height > 0 && width <= int.MaxValue && height <= int.MaxValue)
                bmp = Composite(br, version, (int)width, (int)height, channels, depth, mode, comp);
        }
        catch (Exception ex) { Log.Write(ex); }

        return bmp ?? Thumbnail(br, fs, resStart, resLen);
    }

    // ---------------------------------------------------------------- merged image

    private static Bitmap? Composite(BinaryReader br, int version, int width, int height,
                                     int channels, int depth, int mode, int comp)
    {
        int color = mode == 3 ? 3 : mode == 1 ? 1 : mode == 4 ? 4 : 0;   // RGB, grayscale, CMYK
        if (color == 0 || channels < color) return null;
        if (depth != 8 && depth != 16) return null;
        if (comp != 0 && comp != 1) return null;                         // raw or RLE only
        if ((long)width * height > MaxPixels) return null;

        int alphaIdx = (mode == 3 || mode == 1) && channels > color ? color : -1;
        int read = alphaIdx >= 0 ? color + 1 : color;
        int bps = depth / 8;
        int rowBytes = width * bps;
        int step = Math.Max(1, (int)Math.Ceiling(Math.Max(width, height) / (double)MaxSide));
        int rw = (width + step - 1) / step, rh = (height + step - 1) / step;

        var planes = new byte[read][];
        for (int c = 0; c < read; c++) planes[c] = new byte[rw * rh];

        int entry = version == 1 ? 2 : 4;
        byte[]? table = null;
        if (comp == 1)
        {
            long tableLen = (long)channels * height * entry;
            if (tableLen > int.MaxValue) return null;
            table = br.ReadBytes((int)tableLen);
            if (table.Length < tableLen) return null;
        }

        var row = new byte[rowBytes];
        for (int c = 0; c < read; c++)
        {
            var plane = planes[c];
            for (int y = 0; y < height; y++)
            {
                if (comp == 0)
                {
                    if (!ReadFully(br, row, rowBytes)) return null;
                }
                else
                {
                    int idx = (c * height + y) * entry;
                    int cnt = entry == 2
                        ? (table![idx] << 8) | table[idx + 1]
                        : (table![idx] << 24) | (table[idx + 1] << 16) | (table[idx + 2] << 8) | table[idx + 3];
                    if (cnt < 0 || cnt > rowBytes * 2 + 64) return null;
                    byte[] data = br.ReadBytes(cnt);
                    if (data.Length < cnt) return null;
                    Array.Clear(row, 0, row.Length);
                    UnpackBits(data, row);
                }

                if (y % step == 0)
                {
                    int baseIdx = (y / step) * rw;
                    for (int x = 0, rx = 0; x < width; x += step, rx++)
                        plane[baseIdx + rx] = row[x * bps];          // high byte for 16-bit samples
                }
            }
        }

        var bgr = new byte[rw * rh * 3];
        bool haveFirst = false, uniform = true;
        int first = 0;
        for (int ry = 0; ry < rh; ry++)
        {
            for (int rx = 0; rx < rw; rx++)
            {
                int i = ry * rw + rx;
                int r, g, b;
                if (mode == 3) { r = planes[0][i]; g = planes[1][i]; b = planes[2][i]; }
                else if (mode == 1) { r = g = b = planes[0][i]; }
                else
                {
                    int k = planes[3][i];                            // Photoshop stores CMYK inverted
                    r = planes[0][i] * k / 255; g = planes[1][i] * k / 255; b = planes[2][i] * k / 255;
                }

                int a = alphaIdx >= 0 ? planes[alphaIdx][i] : 255;
                int key = (r << 16) | (g << 8) | b | (a << 24);
                if (!haveFirst) { first = key; haveFirst = true; }
                else if (key != first) uniform = false;

                if (a < 255)                                         // show transparency on a checkerboard
                {
                    int bg = (((rx >> 3) + (ry >> 3)) & 1) == 0 ? 255 : 204;
                    r = (r * a + bg * (255 - a)) / 255;
                    g = (g * a + bg * (255 - a)) / 255;
                    b = (b * a + bg * (255 - a)) / 255;
                }

                int o = i * 3;
                bgr[o] = (byte)b; bgr[o + 1] = (byte)g; bgr[o + 2] = (byte)r;
            }
        }

        // A perfectly uniform result means the file has no real merged image
        // (saved without "Maximize Compatibility"): let the caller use the thumbnail instead.
        if (uniform) return null;
        return FromBgr(bgr, rw, rh);
    }

    private static Bitmap FromBgr(byte[] bgr, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(bgr, y * w * 3, bd.Scan0 + y * bd.Stride, w * 3);
        }
        finally { bmp.UnlockBits(bd); }
        return bmp;
    }

    // ---------------------------------------------------------------- embedded thumbnail

    /// <summary>Image resource 1036 holds a small JPEG preview of the document.</summary>
    private static Bitmap? Thumbnail(BinaryReader br, FileStream fs, long start, long length)
    {
        try
        {
            fs.Position = start;
            long end = start + length;
            while (fs.Position + 12 <= end)
            {
                byte[] sig = br.ReadBytes(4);
                if (sig.Length < 4 || sig[0] != 0x38 || sig[1] != 0x42 || sig[2] != 0x49 || sig[3] != 0x4D) break; // "8BIM"
                int id = U16(br);
                int nameLen = br.ReadByte();
                br.ReadBytes((1 + nameLen) % 2 == 0 ? nameLen : nameLen + 1);   // Pascal string padded to even
                long size = U32(br);
                long dataStart = fs.Position;

                if (id == 1036 && size > 28 && size < 20_000_000)
                {
                    br.ReadBytes(28);                                            // thumbnail header
                    byte[] jpg = br.ReadBytes((int)(size - 28));
                    using var ms = new MemoryStream(jpg);
                    using var img = Image.FromStream(ms);
                    return new Bitmap(img);
                }
                fs.Position = dataStart + size + (size & 1);
            }
        }
        catch (Exception ex) { Log.Write(ex); }
        return null;
    }

    // ---------------------------------------------------------------- helpers

    private static void UnpackBits(byte[] src, byte[] dst)
    {
        int si = 0, di = 0;
        while (si < src.Length && di < dst.Length)
        {
            int n = (sbyte)src[si++];
            if (n >= 0)
            {
                int c = n + 1;
                for (int k = 0; k < c && si < src.Length && di < dst.Length; k++) dst[di++] = src[si++];
            }
            else if (n != -128)
            {
                int c = 1 - n;
                if (si >= src.Length) break;
                byte v = src[si++];
                for (int k = 0; k < c && di < dst.Length; k++) dst[di++] = v;
            }
        }
    }

    private static bool ReadFully(BinaryReader br, byte[] buf, int count)
    {
        int off = 0;
        while (off < count)
        {
            int n = br.Read(buf, off, count - off);
            if (n <= 0) return false;
            off += n;
        }
        return true;
    }

    private static int U16(BinaryReader br) { int a = br.ReadByte(), b = br.ReadByte(); return (a << 8) | b; }

    private static uint U32(BinaryReader br)
    {
        uint a = br.ReadByte(), b = br.ReadByte(), c = br.ReadByte(), d = br.ReadByte();
        return (a << 24) | (b << 16) | (c << 8) | d;
    }

    private static ulong U64(BinaryReader br) { ulong hi = U32(br), lo = U32(br); return (hi << 32) | lo; }
}
