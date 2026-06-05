namespace UWUVCI.Core.Models;

/// <summary>Controls how the Wii ISO is trimmed before injection.</summary>
public enum WiiTrimMode
{
    /// <summary>Full trim (default).</summary>
    Trim            = 0,
    /// <summary>Only remove trailing garbage; keep file size closer to original.</summary>
    OnlyTrimGarbage = 1,
    /// <summary>Do not modify the ISO at all.</summary>
    DoNotModify     = 2,
}
