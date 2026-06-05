namespace UWUVCI.Core.Models;

/// <summary>
/// Immutable-ish game configuration that drives one injection job.
/// Replaces the legacy GameConfig class; carries no WPF or UI references.
/// </summary>
public sealed class GameConfig
{
    // ---- identity ----------------------------------------------------------

    /// <summary>Target virtual console platform.</summary>
    public GameConsole Console { get; set; }

    /// <summary>Selected base title.</summary>
    public GameBaseRef? BaseRom { get; set; }

    /// <summary>
    /// Absolute path to a user-supplied base directory.
    /// Only used when <see cref="BaseRom"/>.<see cref="GameBaseRef.IsCustom"/> is true.
    /// </summary>
    public string? CustomBasePath { get; set; }

    // ---- naming ------------------------------------------------------------

    /// <summary>Full display name written into meta.xml.</summary>
    public string? GameName { get; set; }

    /// <summary>Short name (≤20 chars) written into meta.xml.</summary>
    public string? GameShortName { get; set; }

    // ---- boot images -------------------------------------------------------

    /// <summary>iconTex – 128×128 px, 32-bit.</summary>
    public ImageAsset? IconTex { get; set; }

    /// <summary>bootTvTex – 1280×720 px, 24-bit.</summary>
    public ImageAsset? BootTvTex { get; set; }

    /// <summary>bootDrcTex – 854×480 px, 24-bit.</summary>
    public ImageAsset? BootDrcTex { get; set; }

    /// <summary>bootLogoTex – 170×42 px, 32-bit.</summary>
    public ImageAsset? BootLogoTex { get; set; }

    // ---- emulator options --------------------------------------------------

    /// <summary>N64-specific emulator settings (null when not applicable).</summary>
    public EmulatorConfig? N64Config { get; set; }

    /// <summary>GBA-specific emulator settings (null when not applicable).</summary>
    public EmulatorConfig? GbaConfig { get; set; }

    // ---- console-specific flags --------------------------------------------

    /// <summary>NES palette name, e.g. "Default (Base RPX)".</summary>
    public string NesPalette { get; set; } = "Default (Base RPX)";

    /// <summary>Wii ISO trim strategy.</summary>
    public WiiTrimMode WiiTrimMode { get; set; } = WiiTrimMode.Trim;

    /// <summary>Force 4:3 aspect ratio (GCN).</summary>
    public bool Force4by3 { get; set; }

    /// <summary>Disable gamepad (WII).</summary>
    public bool DisableGamepad { get; set; }

    /// <summary>Enable Poke Patch (N64).</summary>
    public bool PokePatch { get; set; }

    // ---- convenience -------------------------------------------------------

    /// <summary>Returns the four boot images in canonical key order.</summary>
    public IReadOnlyList<(string Key, ImageAsset? Asset)> BootImages =>
    [
        ("iconTex",    IconTex),
        ("bootTvTex",  BootTvTex),
        ("bootDrcTex", BootDrcTex),
        ("bootLogoTex",BootLogoTex),
    ];

    public GameConfig Clone() => (GameConfig)MemberwiseClone();
}
