using UWUVCI.Core.Tooling;

namespace UWUVCI.Tooling;

/// <summary>
/// Runtime implementation of <see cref="IPlatformInfo"/>.
/// Probes environment variables and OS flags – no config overrides here;
/// callers inject <see cref="NativeWindowsOverride"/> from app settings.
/// </summary>
public sealed class PlatformInfo : IPlatformInfo
{
    // Optional override from app settings: true=force-native, false=force-wine, null=auto
    private readonly bool? _nativeWindowsOverride;

    public PlatformInfo(bool? nativeWindowsOverride = null)
    {
        _nativeWindowsOverride = nativeWindowsOverride;
    }

    // ---- IPlatformInfo ----

    public bool IsNativeWindows
    {
        get
        {
            if (_nativeWindowsOverride.HasValue) return _nativeWindowsOverride.Value;
            return IsWindowsOs && !DetectWineLike();
        }
    }

    public bool IsLinux => !IsWindowsOs && !IsMacOs;

    public bool IsWineLike
    {
        get
        {
            if (_nativeWindowsOverride.HasValue) return !_nativeWindowsOverride.Value;
            return DetectWineLike();
        }
    }

    public string? WineFlavor
    {
        get
        {
            if (!IsWineLike) return null;
            if (HasEnv("CX_BOTTLE_PATH") || HasEnv("CROSSOVER_PREFIX")) return "CrossOver";
            if (HasEnv("STEAM_COMPAT_DATA_PATH")) return "Proton";
            if (HasEnv("LUTRIS_GAME_UUID")) return "Lutris";
            return "Wine";
        }
    }

    public string ToWindowsPath(string hostPath)
    {
        if (string.IsNullOrEmpty(hostPath)) return hostPath ?? "";
        if (!IsWineLike) return hostPath;

        // Already a Windows path
        if (hostPath.Length > 2 && char.IsLetter(hostPath[0]) && hostPath[1] == ':')
            return hostPath.Replace('/', '\\');

        // POSIX → Z:\...
        if (hostPath[0] == '/')
            return @"Z:\" + hostPath.TrimStart('/').Replace('/', '\\');

        return hostPath;
    }

    public string ToHostPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath)) return windowsPath ?? "";
        if (!IsWineLike) return windowsPath;

        // Z:\ → /
        if (windowsPath.StartsWith(@"Z:\", StringComparison.OrdinalIgnoreCase))
            return windowsPath.Substring(2).Replace('\\', '/');
        if (windowsPath.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase))
            return windowsPath.Substring(2).Replace('\\', '/');

        // C:\users\<name>\... → /home/<name>/... or /Users/<name>/...
        if (windowsPath.Length > 9 &&
            char.ToUpperInvariant(windowsPath[0]) == 'C' &&
            windowsPath[1] == ':')
        {
            var rest = windowsPath.Substring(3).TrimStart('\\', '/');
            var parts = rest.Split('\\', '/');
            if (parts.Length >= 2 &&
                parts[0].Equals("users", StringComparison.OrdinalIgnoreCase))
            {
                var user = parts[1];
                var tail = parts.Length > 2 ? string.Join("/", parts[2..]) : "";
                var prefix = IsMacOs ? "/Users/" : "/home/";
                return prefix + user + (tail.Length > 0 ? "/" + tail : "");
            }
        }

        // Generic drive fallback: D:\ → /d/...
        if (windowsPath.Length > 2 && char.IsLetter(windowsPath[0]) && windowsPath[1] == ':')
        {
            char drive = char.ToLowerInvariant(windowsPath[0]);
            return "/" + drive + "/" + windowsPath.Substring(2).TrimStart('\\', '/').Replace('\\', '/');
        }

        return windowsPath.Replace('\\', '/');
    }

    // ---- static OS detection helpers (no side effects) ----

    private static bool IsWindowsOs =>
        Environment.OSVersion.Platform is PlatformID.Win32NT or PlatformID.Win32Windows;

    // macOS inside Wine: Z:\ maps to / and macOS marker exists
    private static bool IsMacOs =>
        File.Exists(@"Z:\System\Library\CoreServices\SystemVersion.plist");

    private static bool DetectWineLike()
    {
        // Strong env markers
        if (HasEnv("WINELOADERNOEXEC") || HasEnv("WINEPREFIX") || HasEnv("WINEDLLPATH") ||
            HasEnv("WINELOADER") || HasEnv("WINEESYNC") || HasEnv("WINEFSYNC"))
            return true;

        // Platform-launcher markers
        if (HasEnv("STEAM_COMPAT_DATA_PATH") || HasEnv("LUTRIS_GAME_UUID") ||
            HasEnv("CX_BOTTLE_PATH") || HasEnv("CROSSOVER_PREFIX"))
            return true;

        // If we are a native Linux/Mac process, we are not Wine
        if (!IsWindowsOs)
            return false;

        return false;
    }

    private static bool HasEnv(string name)
    {
        try { return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)); }
        catch { return false; }
    }
}
