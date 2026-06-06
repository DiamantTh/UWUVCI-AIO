using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Injects a TurboGrafx-16 or TurboCD ROM into a Wii U Virtual Console base.
/// For TurboCD (directory input): uses BuildTurboCDPcePkg.exe.
/// For TG16 (single ROM): uses BuildPcePkg.exe.
/// Both produce pce.pkg which is installed into content/pceemu/.
/// </summary>
public static class Tg16InjectService
{
    public static async Task InjectAsync(
        string    toolsPath,
        string    tempPath,
        string    baseRomPath,
        string    romPath,
        IToolRunner runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        Directory.CreateDirectory(tempPath);

        string pcePkg;

        if (Directory.Exists(romPath))
        {
            // TurboCD: copy directory as "test" in tempPath, run BuildTurboCDPcePkg
            pcePkg = await BuildTurboCdPkgAsync(toolsPath, tempPath, romPath, runner, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // TG16 ROM: run BuildPcePkg
            pcePkg = await BuildTg16PkgAsync(toolsPath, tempPath, romPath, runner, cancellationToken).ConfigureAwait(false);
        }

        // Install pce.pkg → content/pceemu/pce.pkg
        var destDir = Path.Combine(baseRomPath, "content", "pceemu");
        Directory.CreateDirectory(destDir);
        var destPkg = Path.Combine(destDir, "pce.pkg");

        if (File.Exists(destPkg)) File.Delete(destPkg);
        File.Move(pcePkg, destPkg);
    }

    // ---- builders ----------------------------------------------------------

    private static Task<string> BuildTurboCdPkgAsync(
        string toolsPath, string tempPath, string romDir,
        IToolRunner runner, CancellationToken ct)
    {
        // BuildTurboCDPcePkg.exe was Windows-only.
        // Native C# implementation requires knowledge of the Wii U TG16 pce.pkg
        // container format for TurboCD disc images. Not yet implemented.
        // See REWRITE_PLAN.md §Phase-18 for the format specification.
        throw new PlatformNotSupportedException(
            "TurboCD injection requires a native pce.pkg builder for disc images " +
            "that is not yet available. The BuildTurboCDPcePkg.exe was Windows-only; " +
            "see REWRITE_PLAN.md §Phase-18 for the format specification.");
    }

    private static Task<string> BuildTg16PkgAsync(
        string toolsPath, string tempPath, string romPath,
        IToolRunner runner, CancellationToken ct)
    {
        // BuildPcePkg.exe was Windows-only.
        // Native C# implementation requires knowledge of the Wii U pce.pkg
        // container format. Not yet implemented.
        // See REWRITE_PLAN.md §Phase-18 for the format specification.
        throw new PlatformNotSupportedException(
            "TG16 injection requires a native pce.pkg builder that is not yet " +
            "available. The BuildPcePkg.exe was Windows-only; " +
            "see REWRITE_PLAN.md §Phase-18 for the format specification.");
    }
}
