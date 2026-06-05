namespace UWUVCI.Core.Models;

/// <summary>
/// Represents one of the four Wii U boot / icon textures.
/// Replaces the legacy PNGTGA class; carries either a file path or raw bytes
/// (or both after preprocessing).
/// </summary>
public sealed class ImageAsset
{
    /// <summary>Absolute path on the host file system, or null when using in-memory bytes.</summary>
    public string? ImgPath { get; set; }

    /// <summary>Raw image bytes, or null when loading lazily from <see cref="ImgPath"/>.</summary>
    public byte[]? ImgBin { get; set; }

    /// <summary>Original file extension (e.g. ".png", ".tga"), lower-case.</summary>
    public string? Extension { get; set; }

    /// <returns>true when either a path or in-memory data is present.</returns>
    public bool HasContent => !string.IsNullOrWhiteSpace(ImgPath) || ImgBin is { Length: > 0 };
}
