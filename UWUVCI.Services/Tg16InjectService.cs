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

    private static async Task<string> BuildTurboCdPkgAsync(
        string toolsPath, string tempPath, string romDir,
        IToolRunner runner, CancellationToken ct)
    {
        var testDir = Path.Combine(tempPath, "test");
        if (Directory.Exists(testDir))
            Directory.Delete(testDir, recursive: true);

        // Copy ROM directory structure into tempPath/test
        IOHelpers.MoveOrCopyDirectory(romDir, testDir);

        var result = await runner.RunAsync(
            "BuildTurboCDPcePkg",
            "test",
            workingDirectory: tempPath,
            cancellationToken: ct).ConfigureAwait(false);

        // Cleanup test dir
        try { Directory.Delete(testDir, recursive: true); } catch { /* best-effort */ }

        if (!result.Success)
            throw new InvalidOperationException(
                $"BuildTurboCDPcePkg failed (exit {result.ExitCode}): {result.StandardError}");

        var pkg = Path.Combine(tempPath, "pce.pkg");
        if (!File.Exists(pkg))
            throw new FileNotFoundException("BuildTurboCDPcePkg did not produce pce.pkg.", pkg);
        return pkg;
    }

    private static async Task<string> BuildTg16PkgAsync(
        string toolsPath, string tempPath, string romPath,
        IToolRunner runner, CancellationToken ct)
    {
        var result = await runner.RunAsync(
            "BuildPcePkg",
            $"\"{romPath}\"",
            workingDirectory: tempPath,
            cancellationToken: ct).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(
                $"BuildPcePkg failed (exit {result.ExitCode}): {result.StandardError}");

        var pkg = Path.Combine(tempPath, "pce.pkg");
        if (!File.Exists(pkg))
            throw new FileNotFoundException("BuildPcePkg did not produce pce.pkg.", pkg);
        return pkg;
    }
}
