namespace UWUVCI.Services;

/// <summary>
/// Native C# replacement for pokepatch.exe.
/// Applies the unofficial savegame-compatibility patch for GBA Pokémon titles.
///
/// The patch locates two occurrences of the byte sequence D0 88 8D 83 42 in the
/// GBA ROM and zeroes out the 3–4 bytes immediately following each occurrence.
/// This disables the Nintendo-licensed save-chip detection so third-party flash
/// carts / GBA VC injections can persist save data correctly.
///
/// Algorithm ported directly from the original UWUVCI legacy Injection.cs
/// PokePatch() static method.
/// </summary>
public static class GbaPokePatch
{
    // The byte pattern that precedes the save-chip detection code in every
    // affected Pokémon GBA title.
    private static readonly byte[] Pattern = [0xD0, 0x88, 0x8D, 0x83, 0x42];

    /// <summary>Applies the PokePatch to the ROM file at <paramref name="romPath"/> in-place.</summary>
    public static void Apply(string romPath)
    {
        ArgumentNullException.ThrowIfNull(romPath);

        byte[] rom = File.ReadAllBytes(romPath);
        var positions = FindAll(rom, Pattern);

        if (positions.Count < 2)
        {
            // Not a Pokémon title or already patched – silently skip.
            return;
        }

        PatchOccurrence(rom, positions[0]);
        PatchOccurrence(rom, positions[1]);

        File.WriteAllBytes(romPath, rom);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void PatchOccurrence(byte[] rom, int matchOffset)
    {
        // The patch position is 5 bytes after the start of the matched pattern.
        int patchAt = matchOffset + Pattern.Length;
        if (patchAt + 4 > rom.Length) return;

        // Read the 4 bytes that follow the pattern.
        // If the 4th byte is 0x24, zero only the first 3; otherwise zero all 4.
        if (rom[patchAt + 3] != 0x24)
            Array.Clear(rom, patchAt, 4);
        else
            Array.Clear(rom, patchAt, 3);
    }

    private static List<int> FindAll(byte[] data, byte[] pattern)
    {
        var results = new List<int>();
        int first  = pattern[0];
        int limit  = data.Length - pattern.Length;

        int i = Array.IndexOf(data, (byte)first, 0);
        while (i >= 0 && i <= limit)
        {
            bool match = true;
            for (int j = 1; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) results.Add(i);
            i = Array.IndexOf(data, (byte)first, i + 1);
        }
        return results;
    }
}
