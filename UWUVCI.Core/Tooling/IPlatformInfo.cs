namespace UWUVCI.Core.Tooling;

/// <summary>
/// Describes what the current runtime environment can do.
/// Implementations probe the OS/Wine environment; tests can inject fakes.
/// </summary>
public interface IPlatformInfo
{
    /// <summary>True when running as a native Windows process (no Wine).</summary>
    bool IsNativeWindows { get; }

    /// <summary>True when running on Linux (not inside Wine).</summary>
    bool IsLinux { get; }

    /// <summary>True when a Wine-like environment is detected (Wine/Proton/Lutris/CrossOver).</summary>
    bool IsWineLike { get; }

    /// <summary>Flavor string: "Wine" | "Proton" | "Lutris" | "CrossOver" | null.</summary>
    string? WineFlavor { get; }

    /// <summary>
    /// Convert a host POSIX path to a Windows-view path (Z:\… or C:\…).
    /// On native Windows returns the original path unchanged.
    /// </summary>
    string ToWindowsPath(string hostPath);

    /// <summary>
    /// Convert a Windows-view path (as used by Wine tools) to a host POSIX path.
    /// On native Windows returns the original path unchanged.
    /// </summary>
    string ToHostPath(string windowsPath);
}
