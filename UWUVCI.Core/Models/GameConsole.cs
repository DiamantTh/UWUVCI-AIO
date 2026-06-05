namespace UWUVCI.Core.Models;

/// <summary>
/// Supported game consoles for Wii U injection.
/// Values match the legacy GameBaseClassLibrary.GameConsoles enum exactly.
/// </summary>
public enum GameConsole
{
    NDS  = 0,
    N64  = 1,
    GBA  = 2,
    NES  = 3,
    SNES = 4,
    TG16 = 5,
    MSX  = 6,
    WII  = 7,
    GCN  = 8,
}
