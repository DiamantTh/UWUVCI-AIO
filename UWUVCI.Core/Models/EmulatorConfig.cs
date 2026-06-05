namespace UWUVCI.Core.Models;

/// <summary>
/// Per-game configuration for N64 and GBA virtual console emulator options.
/// Replaces the legacy N64Conf class.
/// </summary>
public sealed class EmulatorConfig
{
    /// <summary>Path to the .ini / .xml emulator configuration file, or null for the default.</summary>
    public string? IniPath { get; set; }

    /// <summary>Raw bytes of the INI file (in-memory alternative to <see cref="IniPath"/>).</summary>
    public byte[]? IniBin { get; set; }

    /// <summary>N64: remove the dark/dimming filter.</summary>
    public bool DarkFilter { get; set; }

    /// <summary>N64 / GBA: force wide-screen (16:9) output.</summary>
    public bool WideScreen { get; set; }

    /// <summary>true when the INI was sourced from the community database, not the user.</summary>
    public bool CommunityIni { get; set; }
}
