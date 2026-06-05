using UWUVCI.Core.Tooling;

namespace UWUVCI.Tooling;

/// <summary>
/// Manages the Tools and Temp directory paths for the application.
/// Resolves platform-appropriate defaults and ensures directories exist.
/// </summary>
public sealed class ToolPathService
{
    private readonly IPlatformInfo _platform;

    public string ToolsPath { get; }
    public string TempPath { get; }

    public ToolPathService(IPlatformInfo platform, string toolsPath, string tempPath)
    {
        _platform = platform;
        ToolsPath = Normalize(toolsPath, platform, DefaultToolsPath(platform));
        TempPath  = Normalize(tempPath,  platform, DefaultTempPath(platform));

        EnsureExists(ToolsPath);
        EnsureExists(TempPath);
    }

    /// <summary>
    /// Returns the tools path in Windows-view form (for passing to Wine tools).
    /// On native Linux without Wine returns the POSIX path as-is.
    /// </summary>
    public string ToolsPathForTool =>
        _platform.IsWineLike ? _platform.ToWindowsPath(ToolsPath) : ToolsPath;

    /// <summary>
    /// Returns the temp path in Windows-view form.
    /// </summary>
    public string TempPathForTool =>
        _platform.IsWineLike ? _platform.ToWindowsPath(TempPath) : TempPath;

    // ---- defaults ----

    private static string DefaultToolsPath(IPlatformInfo p)
    {
        if (p.IsLinux || p.IsWineLike)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UWUVCI-V4", "Tools");
        }

        // Windows
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UWUVCI-V4", "Tools");
    }

    private static string DefaultTempPath(IPlatformInfo p)
    {
        if (p.IsLinux || p.IsWineLike)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UWUVCI-V4", "Temp");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UWUVCI-V4", "Temp");
    }

    private static string Normalize(string configured, IPlatformInfo p, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(defaultPath);

        // If under Wine and the stored path is a Windows path, convert to host path for I/O
        if (p.IsWineLike && configured.Length > 2 && char.IsLetter(configured[0]) && configured[1] == ':')
            return p.ToHostPath(configured);

        return Path.GetFullPath(configured);
    }

    private static void EnsureExists(string dir)
    {
        try { Directory.CreateDirectory(dir); }
        catch { /* best effort */ }
    }
}
