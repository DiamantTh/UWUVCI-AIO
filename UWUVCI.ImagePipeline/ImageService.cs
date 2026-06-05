using SkiaSharp;

namespace UWUVCI.ImagePipeline;

/// <summary>
/// Core image operations: load, save, resize, validate.
/// All methods are pure functions – no global state.
/// </summary>
public static class ImageService
{
    // ---- load / save -------------------------------------------------------

    /// <summary>Load any image SkiaSharp can decode (PNG, JPG, BMP, …).</summary>
    public static SKBitmap Load(string path)
    {
        using var stream = File.OpenRead(path);
        var bmp = SKBitmap.Decode(stream)
            ?? throw new InvalidOperationException($"Could not decode image: {path}");
        return bmp;
    }

    /// <summary>Load from byte array.</summary>
    public static SKBitmap LoadFromBytes(byte[] data)
    {
        var bmp = SKBitmap.Decode(data)
            ?? throw new InvalidOperationException("Could not decode image from byte array.");
        return bmp;
    }

    /// <summary>Save as PNG.</summary>
    public static void SavePng(SKBitmap bmp, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var image  = SKImage.FromBitmap(bmp);
        using var data   = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    /// <summary>Encode to PNG bytes without writing to disk.</summary>
    public static byte[] EncodePng(SKBitmap bmp)
    {
        using var image = SKImage.FromBitmap(bmp);
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // ---- resize / composite ------------------------------------------------

    /// <summary>Resize to exact dimensions using high-quality Lanczos3 filter.</summary>
    public static SKBitmap Resize(SKBitmap src, int width, int height)
    {
        var info    = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var resized = new SKBitmap(info);
        src.ScalePixels(resized, new SKSamplingOptions(SKCubicResampler.Mitchell));
        return resized;
    }

    /// <summary>
    /// Composite <paramref name="overlay"/> onto <paramref name="background"/>
    /// at the given destination rectangle, returning a new bitmap.
    /// </summary>
    public static SKBitmap Composite(SKBitmap background, SKBitmap overlay, SKRectI destRect)
    {
        var result = background.Copy();
        using var canvas = new SKCanvas(result);
        canvas.DrawBitmap(overlay, new SKRect(destRect.Left, destRect.Top, destRect.Right, destRect.Bottom));
        canvas.Flush();
        return result;
    }

    /// <summary>
    /// Create a blank BGRA bitmap filled with the given color.
    /// </summary>
    public static SKBitmap CreateBlank(int width, int height, SKColor fill)
    {
        var bmp = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        bmp.Erase(fill);
        return bmp;
    }

    // ---- validation --------------------------------------------------------

    /// <summary>
    /// Validate that the bitmap matches an expected size.
    /// Returns a list of human-readable error strings; empty = valid.
    /// </summary>
    public static IReadOnlyList<string> Validate(SKBitmap bmp, int expectedWidth, int expectedHeight)
    {
        var errors = new List<string>();
        if (bmp.Width != expectedWidth || bmp.Height != expectedHeight)
            errors.Add($"Expected {expectedWidth}×{expectedHeight} px, got {bmp.Width}×{bmp.Height} px.");
        return errors;
    }

    /// <summary>Validate PNG file size limits for Wii U assets.</summary>
    public static IReadOnlyList<string> ValidateForWiiU(SKBitmap bmp, string assetType)
    {
        return assetType switch
        {
            "iconTex"    => Validate(bmp, 128, 128),
            "bootTvTex"  => Validate(bmp, 1280, 720),
            "bootDrcTex" => Validate(bmp, 854, 480),
            _            => Array.Empty<string>(),
        };
    }
}
