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
        await RpxToolAsync(toolsPath, rpxFile, compress: false, runner, cancellationToken).ConfigureAwait(false);

        // 2) Optional: pixel-perfect aspect ratio patch
        if (opt.PixelPerfect)
        {
            var res = await runner.RunAsync(
                "ChangeAspectRatio",
                $"\"{rpxFile}\"",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            // Non-fatal: log but continue
            if (!res.Success)
                Console.Error.WriteLine($"[NesSnes] ChangeAspectRatio failed (exit {res.ExitCode}): {res.StandardError}");
        }

        // 3) Inject ROM via retroinject
        var injectResult = await runner.RunAsync(
            "retroinject",
            $"\"{rpxFile}\" \"{romPath}\" \"{rpxFile}\"",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Check for "too large" in output even on success exit code
        if (!injectResult.Success
            || injectResult.StandardError.Contains("is too large")
            || injectResult.StandardOutput.Contains("is too large"))
        {
            throw new InvalidOperationException("ROM is too large for this base title.");
        }

        // 4) NES-specific: apply palette patch
        if (opt.IsNes && !string.IsNullOrWhiteSpace(opt.NesPaletteName))
        {
            NesPalettePatcher.Apply(rpxFile, opt.NesPaletteName, opt.DefaultPaletteName);
        }

        // 5) Recompress RPX
        await RpxToolAsync(toolsPath, rpxFile, compress: true, runner, cancellationToken).ConfigureAwait(false);
    }

    // ---- helpers -----------------------------------------------------------

    private static async Task RpxToolAsync(
        string toolsPath, string rpxPath, bool compress,
        IToolRunner runner, CancellationToken ct)
    {
        var prefix = compress ? "-c" : "-d";
        var result = await runner.RunAsync(
            "wiiurpxtool",
            $"{prefix} \"{rpxPath}\"",
            cancellationToken: ct).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(
                $"wiiurpxtool {prefix} failed (exit {result.ExitCode}): {result.StandardError}");
    }
}
