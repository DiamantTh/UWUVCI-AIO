using UWUVCI.Core.Pipeline;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Implements the GameCube injection pipeline:
/// <list type="number">
///   <item>Extracts BASE.zip into TempBase.</item>
///   <item>Replaces main.dol with Nintendont (optional force-4:3).</item>
///   <item>Places primary game image (ISO/NKit/GCM/GCZ) in TempBase/files/game.iso.</item>
///   <item>Optionally places a second disc image in TempBase/files/disc2.iso.</item>
///   <item>Calls <see cref="WitNfsService"/> to produce the final NFS payload.</item>
/// </list>
/// </summary>
public static class GCNInjectService
{
    public static async Task InjectAsync(
        string        toolsPath,
        string        tempPath,
        string        baseRomPath,
        string        romPath,
        GcnInjectOptions opt,
        IPlatformInfo platform,
        IToolRunner   runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opt);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(platform);

        var tempBase = PrepareTempBase(toolsPath, tempPath);
        ApplyNintendontDol(toolsPath, tempBase, opt.Force43);
        await PlacePrimaryGameAsync(toolsPath, tempBase, romPath, opt, platform, runner, cancellationToken).ConfigureAwait(false);
        await PlaceDisc2Async(toolsPath, tempBase, romPath, opt, platform, runner, cancellationToken).ConfigureAwait(false);

        var nfsOptions = new NfsInjectOptions
        {
            Debug       = opt.Debug,
            Kind        = InjectKind.GCN,
            Passthrough = true,        // GCN always uses passthrough
            Index       = opt.Index,
            LR          = opt.LR,
        };

        await WitNfsService.BuildIsoExtractTicketsAndInjectAsync(
            toolsPath, tempPath, baseRomPath, nfsOptions, platform, runner, cancellationToken).ConfigureAwait(false);
    }

    // ---- Composable steps --------------------------------------------------

    internal static string PrepareTempBase(string toolsPath, string tempPath)
    {
        var tempBase = Path.Combine(tempPath, "TempBase");
        if (Directory.Exists(tempBase))
            Directory.Delete(tempBase, recursive: true);

        var baseDir = BaseExtractor.GetOrExtractBase(toolsPath, "BASE.zip");
        IOHelpers.MoveOrCopyDirectory(baseDir, tempBase);
        return tempBase;
    }

    internal static void ApplyNintendontDol(string toolsPath, string tempBase, bool force43)
    {
        var dolName = force43 ? "nintendont_force.dol" : "nintendont.dol";
        var destDol = Path.Combine(tempBase, "sys", "main.dol");
        File.Copy(Path.Combine(toolsPath, dolName), destDol, overwrite: true);
    }

    internal static async Task PlacePrimaryGameAsync(
        string toolsPath, string tempBase, string romPath,
        GcnInjectOptions opt, IPlatformInfo platform, IToolRunner runner,
        CancellationToken ct)
    {
        var destFile = Path.Combine(tempBase, "files", "game.iso");
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

        await PlaceImageAsync(toolsPath, romPath, destFile, "out.iso", "out.nkit.iso",
            opt.DontTrim, opt.Debug, platform, runner, ct).ConfigureAwait(false);
    }

    internal static async Task PlaceDisc2Async(
        string toolsPath, string tempBase, string primaryRom,
        GcnInjectOptions opt, IPlatformInfo platform, IToolRunner runner,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(opt.Disc2Path) || !File.Exists(opt.Disc2Path)) return;

        var destFile = Path.Combine(tempBase, "files", "disc2.iso");
        await PlaceImageAsync(toolsPath, opt.Disc2Path, destFile, "out(Disc 1).iso", "out(Disc 1).nkit.iso",
            opt.DontTrim, opt.Debug, platform, runner, ct).ConfigureAwait(false);
    }

    // ---- Shared helper: copy/convert a game image to the target location ---

    private static async Task PlaceImageAsync(
        string toolsPath,
        string sourcePath,
        string destPath,
        string isoTempName,
        string nkitTempName,
        bool   dontTrim,
        bool   debug,
        IPlatformInfo platform,
        IToolRunner runner,
        CancellationToken ct)
    {
        var ext = (Path.GetExtension(sourcePath) ?? "").ToLowerInvariant();
        bool isNkit = sourcePath.Contains("nkit", StringComparison.OrdinalIgnoreCase);
        bool isGcz  = ext == ".gcz";

        if (dontTrim)
        {
            // Use raw copy; NKit/GCZ first need conversion to ISO
            if (isNkit || isGcz)
            {
                var converted = await NKitService.ConvertToIsoAsync(toolsPath, sourcePath, isoTempName, runner, ct).ConfigureAwait(false);
                IOHelpers.MoveOverwrite(converted, destPath);
            }
            else
            {
                File.Copy(sourcePath, destPath, overwrite: true);
            }
        }
        else
        {
            // Trim via NKit
            if (ext is ".iso" or ".gcm" or ".gcz" || isNkit)
            {
                var converted = await NKitService.ConvertToNKitAsync(toolsPath, sourcePath, nkitTempName, runner, ct).ConfigureAwait(false);
                IOHelpers.MoveOverwrite(converted, destPath);
            }
            else
            {
                File.Copy(sourcePath, destPath, overwrite: true);
            }
        }
    }
}
