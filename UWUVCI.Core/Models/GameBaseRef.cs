namespace UWUVCI.Core.Models;

/// <summary>
/// Identifies the Wii U base title used as the injection shell.
/// Replaces the untyped GameBases class from GameBaseClassLibrary.
/// </summary>
public sealed class GameBaseRef
{
    /// <summary>
    /// Base title identifier / display name, e.g. "Super Metroid EU".
    /// The value "Custom" signals that a user-supplied base directory is used.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Region string stored alongside the name in the base catalogue.</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>true when the user chose a custom base path instead of a catalogue entry.</summary>
    public bool IsCustom => Name == "Custom";
}
