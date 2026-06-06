namespace UWUVCI.Services;

/// <summary>
/// Native C# replacement for RetroInject.exe.
/// Injects a NES or SNES ROM into a decompressed Wii U Virtual Console RPX.
///
/// The decompressed RPX contains a placeholder ROM that was shipped with the
/// original eShop title.  This implementation locates the placeholder ROM and
/// replaces it in-place with the user-supplied ROM.
///
/// NES ROMs   – located via the iNES magic  NES\x1A  (0x4E 0x45 0x53 0x1A)
/// SNES ROMs  – located via the SNES internal-header "NINTENDO" developer
///              string that appears at a fixed distance inside the ROM body
///              (LoROM: 0x7FD0 / HiROM: 0xFFD0 from ROM start in RPX).
///
/// Error handling mirrors the original tool:
///   InvalidOperationException("ROM is too large for this base title.")
///   is thrown when the new ROM is bigger than the placeholder.
/// </summary>
public static class RetroInjectHelper
{
    // iNES / NES magic header bytes
    private static readonly byte[] InesMagic = [0x4E, 0x45, 0x53, 0x1A]; // "NES\x1A"

    // SNES "NINTENDO" developer string found inside every licensed SNES ROM.
    // At LoROM: 0x7FD0 | HiROM: 0xFFD0  (relative to ROM-start inside the RPX).
    private static readonly byte[] NintendoTag =
        "NINTENDO"u8.ToArray(); // 8 bytes

    // Minimum RPX alignment used when searching for ROM bodies
    private const int SearchStep = 16;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Injects <paramref name="romPath"/> into the decompressed RPX at
    /// <paramref name="rpxPath"/> and writes the result back to
    /// <paramref name="outputRpxPath"/> (may equal <paramref name="rpxPath"/>
    /// for in-place replacement).
    /// </summary>
    public static void InjectRom(string rpxPath, string romPath, bool isNes, string outputRpxPath)
    {
        ArgumentNullException.ThrowIfNull(rpxPath);
        ArgumentNullException.ThrowIfNull(romPath);
        ArgumentNullException.ThrowIfNull(outputRpxPath);

        byte[] rpx = File.ReadAllBytes(rpxPath);
        byte[] rom = File.ReadAllBytes(romPath);

        int injectionOffset;
        int slotSize;

        if (isNes)
            (injectionOffset, slotSize) = FindNesSlot(rpx);
        else
            (injectionOffset, slotSize) = FindSnesSlot(rpx);

        if (rom.Length > slotSize)
            throw new InvalidOperationException("ROM is too large for this base title.");

        // Copy new ROM into slot; zero-pad remainder of original slot.
        Array.Copy(rom, 0, rpx, injectionOffset, rom.Length);
        if (rom.Length < slotSize)
            Array.Clear(rpx, injectionOffset + rom.Length, slotSize - rom.Length);

        Directory.CreateDirectory(Path.GetDirectoryName(outputRpxPath)
                                  ?? throw new InvalidOperationException(
                                         $"Cannot determine directory of '{outputRpxPath}'"));
        File.WriteAllBytes(outputRpxPath, rpx);
    }

    // ── NES slot detection ───────────────────────────────────────────────────

    private static (int offset, int size) FindNesSlot(byte[] rpx)
    {
        int offset = IndexOf(rpx, InesMagic);
        if (offset < 0)
            throw new InvalidOperationException(
                "Could not locate NES iNES header in the decompressed RPX. " +
                "Ensure the base is a valid NES Virtual Console title.");

        // Parse iNES header (16 bytes):
        //   byte 4 = #PRG banks (16 KiB each)
        //   byte 5 = #CHR banks (8 KiB each)
        if (offset + 16 > rpx.Length)
            throw new InvalidOperationException("iNES header is truncated inside the RPX.");

        int prgBanks = rpx[offset + 4];
        int chrBanks = rpx[offset + 5];
        int slotSize = 16 + (prgBanks * 16384) + (chrBanks * 8192);

        return (offset, slotSize);
    }

    // ── SNES slot detection ──────────────────────────────────────────────────

    // The Wii U SNES VC always embeds the ROM at a 16-byte-aligned offset.
    // We locate it by searching for the "NINTENDO" developer tag at LoROM or
    // HiROM positions within each candidate ROM start.

    private const int LoRomHeaderOffset = 0x7FD0; // LoROM internal header
    private const int HiRomHeaderOffset = 0xFFD0; // HiROM internal header

    private static (int offset, int size) FindSnesSlot(byte[] rpx)
    {
        // Walk through the RPX in steps looking for a 16-byte-aligned position
        // where either the LoROM or HiROM header contains "NINTENDO".
        for (int candidate = 0; candidate + HiRomHeaderOffset + NintendoTag.Length <= rpx.Length; candidate += SearchStep)
        {
            if (candidate + LoRomHeaderOffset + NintendoTag.Length <= rpx.Length
                && MatchAt(rpx, candidate + LoRomHeaderOffset, NintendoTag))
            {
                // LoROM match – determine ROM size from header byte at +0x17 (size as 2^n KiB)
                int sizeExp  = rpx[candidate + LoRomHeaderOffset + 0x17];
                int slotSize = (1 << sizeExp) * 1024;
                return (candidate, slotSize);
            }

            if (candidate + HiRomHeaderOffset + NintendoTag.Length <= rpx.Length
                && MatchAt(rpx, candidate + HiRomHeaderOffset, NintendoTag))
            {
                int sizeExp  = rpx[candidate + HiRomHeaderOffset + 0x17];
                int slotSize = (1 << sizeExp) * 1024;
                return (candidate, slotSize);
            }
        }

        throw new InvalidOperationException(
            "Could not locate SNES internal header in the decompressed RPX. " +
            "Ensure the base is a valid SNES Virtual Console title.");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        int limit = haystack.Length - needle.Length;
        for (int i = 0; i <= limit; i++)
        {
            if (MatchAt(haystack, i, needle))
                return i;
        }
        return -1;
    }

    private static bool MatchAt(byte[] data, int offset, byte[] pattern)
    {
        for (int j = 0; j < pattern.Length; j++)
            if (data[offset + j] != pattern[j]) return false;
        return true;
    }
}
