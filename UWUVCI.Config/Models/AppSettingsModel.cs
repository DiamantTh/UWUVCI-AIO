using Tomlyn.Model;

namespace UWUVCI.Config.Models;

/// <summary>
/// TOML-backed application settings.
/// Ported from legacy JsonAppSettings.
/// SysKey/SysKey1 removed intentionally (no protection features in rewrite).
/// CKey is preserved but must never be written to logs.
/// </summary>
public sealed class AppSettingsModel
{
    // --- paths ---
    public bool PathsSet { get; set; } = false;
    public string BasePath { get; set; } = "";
    public string OutPath { get; set; } = "";
    public string ToolsPath { get; set; } = "";
    public string TempPath { get; set; } = "";

    // --- console key (never log) ---
    public string CKey { get; set; } = "";

    // --- behaviour flags ---
    public bool SetBaseOnce { get; set; } = false;
    public bool SetOutOnce { get; set; } = false;
    public bool dont { get; set; } = false;
    public bool ndsw { get; set; } = false;
    public bool snesw { get; set; } = false;
    public bool gczw { get; set; } = false;

    // --- platform ---
    /// <summary>true = force native Windows, false = force Wine, null = auto-detect</summary>
    public bool? NativeWindows { get; set; } = null;
    public int UnixWaitDelayMs { get; set; } = 2000;
    public int FileCopyParallelism { get; set; } = 6;

    // --- UI ---
    public string Theme { get; set; } = "Dark";

    // --- misc ---
    public string Ancast { get; set; } = "";
    public bool IsFirstLaunch { get; set; } = true;
    public string LastVersionSeen { get; set; } = "0.0.0";
    public bool ForceTutorialOnNextLaunch { get; set; } = false;
    public bool HasAcknowledgedTutorial { get; set; } = false;
}
