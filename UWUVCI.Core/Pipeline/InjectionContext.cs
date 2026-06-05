using UWUVCI.Core.Models;

namespace UWUVCI.Core.Pipeline;

/// <summary>
/// All inputs for a single injection job.
/// Replaces the MainViewModel dependency inside the legacy Injection class.
/// No WPF/UI references allowed here.
/// </summary>
public sealed class InjectionContext
{
    // ---- required ----------------------------------------------------------

    /// <summary>Game-specific settings (console, base, images, …).</summary>
    public required GameConfig Config { get; init; }

    /// <summary>Absolute path to the ROM file to inject.</summary>
    public required string RomPath { get; init; }

    /// <summary>Absolute path to the tools directory.</summary>
    public required string ToolsPath { get; init; }

    /// <summary>Absolute path to the temp work directory (will be created/cleaned by the pipeline).</summary>
    public required string TempPath { get; init; }

    /// <summary>Absolute path to the output root directory.</summary>
    public required string OutPath { get; init; }

    // ---- optional ----------------------------------------------------------

    /// <summary>Common encryption key for CNUSPACKER packaging.</summary>
    public string? CKey { get; init; }

    /// <summary>Absolute path to a .btsnd / .mp3 / .wav boot sound, or null.</summary>
    public string? BootSoundPath { get; init; }

    /// <summary>Wii: GCT/cheat-code file paths (one per line, may be empty).</summary>
    public string? GctPaths { get; init; }

    /// <summary>Wii: apply video/PAL patch.</summary>
    public bool PatchVideo { get; init; }

    /// <summary>Wii: apply RegionFrii patch.</summary>
    public bool RegionFrii { get; init; }

    /// <summary>Wii: convert to PAL.</summary>
    public bool ToPal { get; init; }

    /// <summary>Wii: force GCN mode for a Wii ROM.</summary>
    public bool ForceGcnMode { get; init; }

    /// <summary>Wii: force NKIT conversion.</summary>
    public bool ForceNkitConvert { get; init; }

    /// <summary>Wii: pass-through mode (skip wit repack).</summary>
    public bool Passthrough { get; init; }

    /// <summary>Wii: controller slot index override (0 = default, 4 = Classic Controller).</summary>
    public int ControllerIndex { get; init; }

    /// <summary>Wii: remap L+R to ZL+ZR.</summary>
    public bool RemapLR { get; init; }

    /// <summary>Wii/N64: remove deflicker filter.</summary>
    public bool RemoveDeflicker { get; init; }

    /// <summary>Wii/N64: remove dithering.</summary>
    public bool RemoveDithering { get; init; }

    /// <summary>Wii/N64: apply half vertical filter.</summary>
    public bool HalfVFilter { get; init; }

    /// <summary>Enable verbose/debug tool output.</summary>
    public bool Debug { get; init; }

    /// <summary>Skip disk-space check (workaround for inaccessible DriveInfo).</summary>
    public bool SkipDiskSpaceCheck { get; init; }

    /// <summary>Unix-only: extra ms to wait after Wine tool invocations.</summary>
    public int UnixWaitDelayMs { get; init; }

    // ---- progress / logging ------------------------------------------------

    /// <summary>Receives progress updates; never null (defaults to no-op).</summary>
    public IProgressReporter Progress { get; init; } = NullProgressReporter.Instance;

    /// <summary>Receives log entries; never null (defaults to no-op).</summary>
    public IJobLogger Logger { get; init; } = NullJobLogger.Instance;
}
