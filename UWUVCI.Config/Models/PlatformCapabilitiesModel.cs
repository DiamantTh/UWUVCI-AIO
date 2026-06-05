namespace UWUVCI.Config.Models;

/// <summary>
/// Describes what the current platform can do.
/// Populated at runtime from a combination of TOML manifest + actual capability probes.
/// </summary>
public sealed class PlatformCapabilitiesModel
{
    /// <summary>Human-readable platform identifier (e.g. "linux-x64", "windows-x64").</summary>
    public string PlatformId { get; set; } = "";

    /// <summary>True when running on native Windows (no Wine/Proton).</summary>
    public bool IsNativeWindows { get; set; } = false;

    /// <summary>True when a Wine-like environment is detected.</summary>
    public bool IsWineLike { get; set; } = false;

    /// <summary>True when running on Linux (native, not Wine).</summary>
    public bool IsLinux { get; set; } = false;

    /// <summary>Process execution is available (always false in WASM).</summary>
    public bool CanRunProcesses { get; set; } = true;

    /// <summary>File picker dialogs are available.</summary>
    public bool HasFilePicker { get; set; } = true;

    /// <summary>Tool execution capability (Wine or native).</summary>
    public bool CanRunWineTools { get; set; } = false;
    public bool CanRunNativeTools { get; set; } = false;

    /// <summary>
    /// Optional TOML-driven capability overrides, keyed by feature name.
    /// e.g. "inject.wii" = true
    /// </summary>
    public IDictionary<string, bool> FeatureFlags { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
}
