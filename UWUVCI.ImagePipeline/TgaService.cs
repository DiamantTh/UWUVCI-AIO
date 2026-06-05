using SkiaSharp;

namespace UWUVCI.ImagePipeline;

/// <summary>
/// Minimal TGA reader/writer covering the subset used by UWUVCI:
///   – Uncompressed truecolor (type 2): RGB / RGBA
///   – Run-length encoded truecolor (type 10): RGB / RGBA
/// No palette, no grayscale, no extensions – sufficient for Wii iconTex / bootTvTex.
///
/// Decision rationale (see REWRITE_PLAN Phase 5):
/// SkiaSharp 3.x does not support TGA natively; Pfim adds ~1 MB of native
/// overhead. A small custom reader/writer for the fixed UWUVCI TGA subset is
/// the lightest option with no additional native dependencies.
/// </summary>
public static class TgaService
{
    // ---- public API -------------------------------------------------------

    public static SKBitmap LoadTga(string path)
    {
        using var stream = File.OpenRead(path);
        return LoadTga(stream);
    }

    public static SKBitmap LoadTga(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        return Decode(reader);
    }

    public static SKBitmap LoadTgaFromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data, writable: false);
        return LoadTga(ms);
    }

    /// <summary>
    /// Save as uncompressed TGA type 2 (BGR or BGRA, 24 or 32 bpp).
    /// </summary>
    public static void SaveTga(SKBitmap bmp, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var stream = File.Create(path);
        SaveTga(bmp, stream);
    }

    public static void SaveTga(SKBitmap bmp, Stream stream)
    {
        bool hasAlpha = bmp.AlphaType != SKAlphaType.Opaque;
        int bpp = hasAlpha ? 32 : 24;

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        WriteHeader(writer, bmp.Width, bmp.Height, bpp);

        // TGA bottom-up row order
        for (int y = bmp.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                writer.Write(px.Blue);
                writer.Write(px.Green);
                writer.Write(px.Red);
                if (hasAlpha) writer.Write(px.Alpha);
            }
        }
    }

    public static byte[] EncodeTga(SKBitmap bmp)
    {
        using var ms = new MemoryStream();
        SaveTga(bmp, ms);
        return ms.ToArray();
    }

    // ---- decode -----------------------------------------------------------

    private static SKBitmap Decode(BinaryReader r)
    {
        // Header (18 bytes)
        byte idLength    = r.ReadByte();
        byte colorMapType = r.ReadByte();
        byte imageType   = r.ReadByte();

        // Color map spec (5 bytes – ignored for type 2/10)
        r.ReadBytes(5);

        // Image spec (10 bytes)
        /*short xOrigin =*/ r.ReadInt16();
        /*short yOrigin =*/ r.ReadInt16();
        short width    = r.ReadInt16();
        short height   = r.ReadInt16();
        byte bpp       = r.ReadByte();
        byte descriptor = r.ReadByte();

        // Skip image ID
        if (idLength > 0) r.ReadBytes(idLength);
        // Skip color map data
        if (colorMapType != 0) r.ReadBytes(r.ReadInt16() * ((r.ReadByte() + 7) / 8));

        bool bottomUp = (descriptor & 0x20) == 0;

        return imageType switch
        {
            2  => DecodeUncompressed(r, width, height, bpp, bottomUp),
            10 => DecodeRle(r, width, height, bpp, bottomUp),
            _  => throw new NotSupportedException($"TGA image type {imageType} is not supported.")
        };
    }

    private static SKBitmap DecodeUncompressed(BinaryReader r, int w, int h, int bpp, bool bottomUp)
    {
        var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        int bytesPerPixel = bpp / 8;

        for (int row = 0; row < h; row++)
        {
            int y = bottomUp ? h - 1 - row : row;
            for (int x = 0; x < w; x++)
            {
                var px = ReadPixel(r, bytesPerPixel);
                bmp.SetPixel(x, y, px);
            }
        }
        return bmp;
    }

    private static SKBitmap DecodeRle(BinaryReader r, int w, int h, int bpp, bool bottomUp)
    {
        int bytesPerPixel = bpp / 8;
        var pixels = new SKColor[w * h];
        int i = 0;

        while (i < pixels.Length)
        {
            byte packet = r.ReadByte();
            int count   = (packet & 0x7F) + 1;

            if ((packet & 0x80) != 0)
            {
                // RLE: repeat one pixel
                var px = ReadPixel(r, bytesPerPixel);
                for (int k = 0; k < count && i < pixels.Length; k++, i++)
                    pixels[i] = px;
            }
            else
            {
                // Raw: read count pixels
                for (int k = 0; k < count && i < pixels.Length; k++, i++)
                    pixels[i] = ReadPixel(r, bytesPerPixel);
            }
        }

        var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        for (int row = 0; row < h; row++)
        {
            int y = bottomUp ? h - 1 - row : row;
            for (int x = 0; x < w; x++)
                bmp.SetPixel(x, y, pixels[row * w + x]);
        }
        return bmp;
    }

    private static SKColor ReadPixel(BinaryReader r, int bytesPerPixel)
    {
        byte b = r.ReadByte();
        byte g = r.ReadByte();
        byte red = r.ReadByte();
        byte a = bytesPerPixel == 4 ? r.ReadByte() : (byte)255;
        return new SKColor(red, g, b, a);
    }

    // ---- encode helper ----------------------------------------------------

    private static void WriteHeader(BinaryWriter w, int width, int height, int bpp)
    {
        w.Write((byte)0);           // ID length
        w.Write((byte)0);           // Color map type
        w.Write((byte)2);           // Image type: uncompressed truecolor
        w.Write((short)0);          // Color map spec
        w.Write((short)0);
        w.Write((byte)0);
        w.Write((short)0);          // X origin
        w.Write((short)0);          // Y origin
        w.Write((short)width);
        w.Write((short)height);
        w.Write((byte)bpp);
        w.Write((byte)(bpp == 32 ? 0x28 : 0x20)); // descriptor: top-left origin, alpha bits
    }
}
