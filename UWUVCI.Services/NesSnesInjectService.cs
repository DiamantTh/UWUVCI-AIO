using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>Options shared by NES and SNES injections.</summary>
public sealed class NesSnesInjectOptions
{
    public bool   IsNes          { get; set; }
    /// <summary>Apply pixel-perfect aspect-ratio patch (ChangeAspectRatio.exe).</summary>
    public bool   PixelPerfect   { get; set; }
    /// <summary>NES palette name to apply (empty = keep base RPX default).</summary>
    public string NesPaletteName { get; set; } = string.Empty;
    /// <summary>Default palette name used by the base RPX (for fallback detection).</summary>
    public string DefaultPaletteName { get; set; } = "Default (Base RPX)";
    public bool   Debug          { get; set; }
}

/// <summary>
/// Injects a NES or SNES ROM into a Wii U Virtual Console base using retroinject.exe.
/// Flow: decompress RPX → (optional pixel-perfect patch) → retroinject → (NES palette) → recompress RPX.
/// </summary>
public static class NesSnesInjectService
{
    public static async Task InjectAsync(
        string          toolsPath,
        string          baseRomPath,
        string          romPath,
        NesSnesInjectOptions opt,
        IToolRunner     runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opt);
        ArgumentNullException.ThrowIfNull(runner);

        var rpxFiles = Directory.GetFiles(Path.Combine(baseRomPath, "code"), "*.rpx");
        if (rpxFiles.Length == 0)
            throw new FileNotFoundException("No .rpx file found in base code/ directory.");
        var rpxFile = rpxFiles[0];

        // 1) Decompress RPX
        RpxTool(rpxFile, compress: false);

        // 2) Optional: pixel-perfect aspect ratio patch
        if (opt.PixelPerfect)
        {
            // ChangeAspectRatio.exe was Windows-only; native implementation pending.
            // Non-fatal: log and continue.
            Console.Error.WriteLine(
                "[NesSnes] PixelPerfect patch skipped: native implementation of " +
                "ChangeAspectRatio not yet available.");
        }

        // 3) Inject ROM via native RetroInjectHelper (replaces retroinject.exe)
        try
        {
            RetroInjectHelper.InjectRom(rpxFile, romPath, opt.IsNes, rpxFile);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("too large"))
        {
            throw new InvalidOperationException("ROM is too large for this base title.");
        }

        // 4) NES-specific: apply palette patch
        if (opt.IsNes && !string.IsNullOrWhiteSpace(opt.NesPaletteName))
        {
            NesPalettePatcher.Apply(rpxFile, opt.NesPaletteName, opt.DefaultPaletteName);
        }

        // 5) Recompress RPX
        RpxTool(rpxFile, compress: true);
    }

    // ---- helpers -----------------------------------------------------------

    private static void RpxTool(string rpxPath, bool compress)
    {
        // Native replacement for wiiurpxtool -d / -c
        if (compress)
            WiiURpxService.Compress(rpxPath);
        else
            WiiURpxService.Decompress(rpxPath);
    }
}
