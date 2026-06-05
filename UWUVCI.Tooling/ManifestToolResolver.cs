using UWUVCI.Config.Models;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Tooling;

/// <summary>
/// Resolves tool executables from a <see cref="ToolManifestModel"/> and a tools directory.
/// On Linux: prefers native binary, falls back to Wine-wrapped .exe.
/// On Windows: always uses the .exe.
/// </summary>
public sealed class ManifestToolResolver : IToolResolver
{
    private readonly ToolManifestModel _manifest;
    private readonly string _toolsDirectory;
    private readonly IPlatformInfo _platform;

    public ManifestToolResolver(
        ToolManifestModel manifest,
        string toolsDirectory,
        IPlatformInfo platform)
    {
        _manifest = manifest;
        _toolsDirectory = toolsDirectory;
        _platform = platform;
    }

    public string? Resolve(string toolName)
    {
        var entry = FindEntry(toolName);
        if (entry is null) return null;

        // Native Linux binary
        if (_platform.IsLinux && !entry.WindowsOnly && !string.IsNullOrEmpty(entry.LinuxFileName))
        {
            var nativePath = Path.Combine(_toolsDirectory, entry.LinuxFileName);
            if (File.Exists(nativePath)) return nativePath;
        }

        // Windows .exe (used on Windows natively or via Wine on Linux)
        if (!string.IsNullOrEmpty(entry.WindowsFileName))
        {
            var exePath = Path.Combine(_toolsDirectory, entry.WindowsFileName);
            if (File.Exists(exePath)) return exePath;
        }

        return null;
    }

    public bool IsAvailable(string toolName) => Resolve(toolName) is not null;

    private ToolEntryModel? FindEntry(string toolName)
        => _manifest.Tools.FirstOrDefault(t =>
            string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
}
