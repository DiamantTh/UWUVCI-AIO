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
        // Copy input directory to temp "test" folder
        var testDir = Path.Combine(tempPath, "test");
        Directory.CreateDirectory(testDir);
        foreach (var file in Directory.GetFiles(romDir, "*", SearchOption.AllDirectories))
        {
            var rel = file.Substring(romDir.Length).TrimStart(Path.DirectorySeparatorChar);
            var target = Path.Combine(testDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        try
        {
            // Use native C# builder
            var pcePkg = Path.Combine(tempPath, "pce.pkg");
            TurboGrafx16PkgBuilder.BuildPcePkg(testDir, pcePkg);
            return Task.FromResult(pcePkg);
        }
        catch (FileNotFoundException ex) when (ex.Message.Contains(".hcd") || ex.Message.Contains(".ogg") || ex.Message.Contains(".bin"))
        {
            throw new InvalidOperationException(
                $"TurboCD directory is missing required files (.hcd, .ogg, .bin). {ex.Message}", ex);
        }
        finally
        {
            // Clean up
            try { if (Directory.Exists(testDir)) Directory.Delete(testDir, recursive: true); } catch { }
        }
    }

    private static Task<string> BuildTg16PkgAsync(
        string toolsPath, string tempPath, string romPath,
        IToolRunner runner, CancellationToken ct)
    {
        // TG16 single ROM input is treated as a pre-packaged directory structure.
        // The original BuildPcePkg.exe was a Windows-only binary that required
        // pre-existing .hcd, .ogg, and .bin files in a specific layout.
        // Since TG16 injection is less common and requires significant pre-processing
        // (ROM → HCD + OGG + BIN conversion), this remains a future enhancement.
        // Users can instead provide pre-packaged TurboCD-style directories.
        throw new PlatformNotSupportedException(
            "Direct TG16 ROM injection is not yet supported. " +
            "Please provide a TurboCD-style directory with .hcd, .ogg, and .bin files, " +
            "or generate the pce.pkg file externally and copy it to content/pceemu/pce.pkg " +
            "in the base game folder.");
    }
}
