namespace UWUVCI.Core.Runtime;

/// <summary>
/// Resolves all writable user-data directories at runtime, using
/// platform-appropriate paths so the published build never writes into
/// the installation directory.
///
/// Linux  : ~/.local/share/UWUVCI-V3  (XDG_DATA_HOME fallback)
/// Windows: %LOCALAPPDATA%\UWUVCI-V3
/// macOS  : ~/Library/Application Support/UWUVCI-V3
/// </summary>
public static class AppDataPaths
{
    // ---- root --------------------------------------------------------------

    public static string RootDir { get; } = ResolveRoot();

    // ---- well-known sub-dirs -----------------------------------------------

    /// <summary>Injected Wii U title output.</summary>
    public static string OutputDir   => Ensure(Path.Combine(RootDir, "Output"));

    /// <summary>Downloaded base-files.</summary>
    public static string BasesDir    => Ensure(Path.Combine(RootDir, "Bases"));

    /// <summary>Downloaded/bundled tools.</summary>
    public static string ToolsDir    => Ensure(Path.Combine(RootDir, "Tools"));

    /// <summary>Temporary work directory; safe to delete between runs.</summary>
    public static string TempDir     => Ensure(Path.Combine(RootDir, "Temp"));

    /// <summary>TOML settings file.</summary>
    public static string SettingsFile => Path.Combine(RootDir, "settings.toml");

    /// <summary>Rolling log file.</summary>
    public static string LogFile      => Path.Combine(Ensure(Path.Combine(RootDir, "Logs")), "uwuvci.log");

    // ---- helpers -----------------------------------------------------------

    private static string ResolveRoot()
    {
        string appName = "UWUVCI-V3";

        if (OperatingSystem.IsLinux())
        {
            // XDG Base Directory Specification
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var dataHome = !string.IsNullOrEmpty(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(dataHome, appName);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", appName);
        }

        // Windows (and fallback)
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appName);
    }

    private static string Ensure(string dir)
    {
        Directory.CreateDirectory(dir);
        return dir;
    }
}
