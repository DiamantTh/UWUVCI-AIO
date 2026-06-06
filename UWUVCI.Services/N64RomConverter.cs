namespace UWUVCI.Services;

/// <summary>
/// Native C# replacement for N64Converter.exe.
/// Converts an N64 ROM to z64 (big-endian) format and copies it to the target
/// ROM slot used by the Wii U N64 Virtual Console emulator.
///
/// N64 ROMs exist in three byte-order variants:
///   z64 (big-endian, native)  – first 4 bytes: 80 37 12 40
///   v64 (byte-swapped)        – first 4 bytes: 37 80 40 12
///   n64 (little-endian)       – first 4 bytes: 40 12 37 80
///
/// The Wii U N64 VC emulator requires z64 format.
/// </summary>
public static class N64RomConverter
{
    // ── magic byte sequences ──────────────────────────────────────────────────

    // z64 – big-endian, native (no conversion needed)
    private static readonly byte[] MagicZ64 = [0x80, 0x37, 0x12, 0x40];

    // v64 – every pair of adjacent bytes is swapped relative to z64
    private static readonly byte[] MagicV64 = [0x37, 0x80, 0x40, 0x12];

    // n64 – every 4-byte group is byte-reversed relative to z64
    private static readonly byte[] MagicN64 = [0x40, 0x12, 0x37, 0x80];

    /// <summary>
    /// Reads the ROM at <paramref name="inputPath"/>, converts it to z64 format
    /// if necessary, and writes the result to <paramref name="outputPath"/>.
    /// Both paths may be the same file (in-place conversion).
    /// </summary>
    public static void ConvertAndCopy(string inputPath, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(outputPath);

        var rom = File.ReadAllBytes(inputPath);
        ConvertToZ64InPlace(rom);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)
                                  ?? throw new InvalidOperationException(
                                         $"Cannot determine directory of '{outputPath}'"));
        File.WriteAllBytes(outputPath, rom);
    }

    /// <summary>
    /// Converts a ROM byte array to z64 format in-place.
    /// No-op if the ROM is already z64 or the format is unrecognised.
    /// </summary>
    public static void ConvertToZ64InPlace(byte[] rom)
    {
        if (rom is null || rom.Length < 4)
            return;

        if (StartsWith(rom, MagicZ64))
        {
            // Already z64 – nothing to do.
            return;
        }

        if (StartsWith(rom, MagicV64))
        {
            // v64: swap every adjacent byte pair.
            for (int i = 0; i + 1 < rom.Length; i += 2)
                (rom[i], rom[i + 1]) = (rom[i + 1], rom[i]);
            return;
        }

        if (StartsWith(rom, MagicN64))
        {
            // n64: reverse each 4-byte group.
            for (int i = 0; i + 3 < rom.Length; i += 4)
            {
                (rom[i],     rom[i + 3]) = (rom[i + 3], rom[i]);
                (rom[i + 1], rom[i + 2]) = (rom[i + 2], rom[i + 1]);
            }
            return;
        }

        // Unknown format — pass through unchanged; the emulator will reject it
        // if it is truly invalid.
    }

    /// <summary>Returns true if <paramref name="rom"/> starts with <paramref name="magic"/>.</summary>
    private static bool StartsWith(byte[] rom, byte[] magic)
    {
        for (int i = 0; i < magic.Length; i++)
            if (rom[i] != magic[i]) return false;
        return true;
    }
}
