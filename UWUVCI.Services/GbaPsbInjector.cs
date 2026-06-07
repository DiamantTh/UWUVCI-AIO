using System.Buffers.Binary;
using System.Text;

namespace UWUVCI.Services;

/// <summary>
/// Injects a new ROM into a Wii U GBA Virtual Console base game by patching
/// the <c>content/alldata.psb.m</c> + <c>content/alldata.bin</c> files in-place.
///
/// File layout:
///   alldata.psb.m   — MDF-encrypted zlib-compressed PSB v2 manifest
///   alldata.bin     — Concatenated MDF-compressed+encrypted subfiles, each padded
///                     to a 0x800-byte boundary.  The PSB manifest maps filenames
///                     to (offset, length) pairs inside alldata.bin.
///
/// The ROM subfile path inside alldata.bin contains the substring "system/roms".
/// Algorithm documented from inject_gba by Andrew Dalgleish.
/// </summary>
internal static class GbaPsbInjector
{
    private const int BinAlignment = 0x800; // alldata.bin subfile alignment

    // ---- public API --------------------------------------------------------

    /// <summary>
    /// Replace the GBA ROM stored inside the base game at <paramref name="baseGamePath"/>.
    /// </summary>
    /// <param name="baseGamePath">Root directory of the unpacked Wii U VC base game.</param>
    /// <param name="newRomPath">Path to the (already Goomba-padded) 32 MiB GBA ROM.</param>
    public static void InjectRom(string baseGamePath, string newRomPath)
    {
        var psbFile = Path.Combine(baseGamePath, "content", "alldata.psb.m");
        var binFile = Path.Combine(baseGamePath, "content", "alldata.bin");

        if (!File.Exists(psbFile)) throw new FileNotFoundException("alldata.psb.m not found.", psbFile);
        if (!File.Exists(binFile)) throw new FileNotFoundException("alldata.bin not found.", binFile);

        // 1. Decrypt + decompress the PSB manifest
        var rawPsb = MArchiveService.DecryptAndDecompress(File.ReadAllBytes(psbFile), "alldata.psb.m");

        // 2. Parse file_info entries (name, offset, length, byte positions in raw PSB)
        var parser = new PsbV2(rawPsb);
        var entries = parser.GetFileInfoEntries();

        // 3. Find the ROM entry (first name containing "system/roms")
        var romIdx = FindRomEntryIndex(entries);
        if (romIdx < 0) throw new InvalidDataException("ROM entry (system/roms) not found in PSB file_info.");

        // 4. Read the new ROM and compress+encrypt it using the ROM's filename as seed
        var romName = entries[romIdx].Name;
        var newRomRaw = File.ReadAllBytes(newRomPath);
        var newRomCompressed = MArchiveService.CompressAndEncrypt(newRomRaw, romName);

        // 5. Build updated subfile array: replace ROM entry data, recompute offsets
        var binData = File.ReadAllBytes(binFile);
        var (newBin, updatedEntries) = RebuildBin(binData, entries, romIdx, newRomCompressed);

        // 6. Patch PSB binary in-place (offsets/lengths), validate encoding sizes
        var newRawPsb = PatchPsb(rawPsb, entries, updatedEntries);

        // 7. Compress+encrypt the updated PSB manifest and write both files
        var newPsbEncrypted = MArchiveService.CompressAndEncrypt(newRawPsb, "alldata.psb.m");
        File.WriteAllBytes(psbFile, newPsbEncrypted);
        File.WriteAllBytes(binFile, newBin);
    }

    // ---- private helpers ---------------------------------------------------

    private static int FindRomEntryIndex(List<PsbFileEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Name.Contains("system/roms", StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>
    /// Rebuild alldata.bin by replacing the ROM subfile.
    /// Returns the new bin bytes and the updated entry list (with new offsets/lengths).
    /// </summary>
    private static (byte[] newBin, List<PsbFileEntry> updated) RebuildBin(
        byte[] oldBin,
        List<PsbFileEntry> entries,
        int romIdx,
        byte[] newRomData)
    {
        // Read all old subfile blobs (encrypted+compressed, exact lengths from PSB)
        var subfiles = new byte[entries.Count][];
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            subfiles[i] = oldBin.AsSpan((int)e.Offset, (int)e.Length).ToArray();
        }
        // Replace the ROM blob
        subfiles[romIdx] = newRomData;

        // Compute new offsets (0x800-aligned) and total size
        uint runningOffset = 0;
        var updated = new List<PsbFileEntry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            uint newLen = (uint)subfiles[i].Length;
            updated.Add(e with { Offset = runningOffset, Length = newLen });
            uint paddedLen = Align(newLen, BinAlignment);
            runningOffset += paddedLen;
        }

        // Write new alldata.bin
        var newBin = new byte[runningOffset];
        for (int i = 0; i < entries.Count; i++)
        {
            var e = updated[i];
            subfiles[i].CopyTo(newBin, (int)e.Offset);
            // Padding bytes stay zero
        }

        return (newBin, updated);
    }

    private static uint Align(uint v, int boundary)
    {
        uint b = (uint)boundary;
        return v == 0 ? 0 : ((v + b - 1) / b) * b;
    }

    /// <summary>
    /// Patch the raw PSB bytes in-place with updated file_info offsets/lengths.
    /// Validates that encoding sizes are unchanged (guaranteed for GBA ROMs).
    /// </summary>
    private static byte[] PatchPsb(byte[] rawPsb, List<PsbFileEntry> original, List<PsbFileEntry> updated)
    {
        System.Diagnostics.Debug.Assert(original.Count == updated.Count);

        var result = new byte[rawPsb.Length];
        rawPsb.CopyTo(result, 0);

        for (int i = 0; i < original.Count; i++)
        {
            var orig = original[i];
            var upd  = updated[i];

            if (orig.Offset != upd.Offset)
            {
                int newEncSize = UintEncodedSize(upd.Offset);
                if (newEncSize != orig.PsbOffsetEncSize)
                    throw new InvalidOperationException(
                        $"PSB file_info encoding size changed for offset of '{orig.Name}': " +
                        $"was {orig.PsbOffsetEncSize} byte(s), need {newEncSize}. Full re-encode required.");
                WriteUintAt(result, orig.PsbOffsetPos, upd.Offset);
            }

            if (orig.Length != upd.Length)
            {
                int newEncSize = UintEncodedSize(upd.Length);
                if (newEncSize != orig.PsbLengthEncSize)
                    throw new InvalidOperationException(
                        $"PSB file_info encoding size changed for length of '{orig.Name}': " +
                        $"was {orig.PsbLengthEncSize} byte(s), need {newEncSize}. Full re-encode required.");
                WriteUintAt(result, orig.PsbLengthPos, upd.Length);
            }
        }

        return result;
    }

    /// Returns the total encoded byte count (type byte + value bytes) for value v.
    internal static int UintEncodedSize(uint v)
    {
        if (v == 0) return 1; // type 4, no value bytes
        // Find smallest n such that v < 2^(8n-1)  (MSB=0 in n bytes)
        for (int n = 1; n <= 4; n++)
            if (v < (1u << (8 * n - 1)))
                return 1 + n; // type byte + n value bytes
        return 5; // 4-byte value (type 8 = 1+4)
    }

    private static void WriteUintAt(byte[] buf, int pos, uint v)
    {
        if (v == 0) { buf[pos] = 4; return; } // type 4
        for (int n = 1; n <= 4; n++)
        {
            if (v < (1u << (8 * n - 1)))
            {
                buf[pos] = (byte)(4 + n); // type byte
                for (int j = 0; j < n; j++)
                    buf[pos + 1 + j] = (byte)(v >> (8 * j));
                return;
            }
        }
        // fallback: 4 bytes
        buf[pos] = 8;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(pos + 1), v);
    }
}

// ---- PSB v2 file-info entry record ----------------------------------------

/// <summary>Represents one entry in the PSB v2 file_info table.</summary>
internal sealed record PsbFileEntry(
    /// <summary>Subfile name (e.g. "system/roms/game.gba").</summary>
    string Name,
    /// <summary>Byte offset of compressed data inside alldata.bin.</summary>
    uint Offset,
    /// <summary>Byte length of compressed data inside alldata.bin (unpadded).</summary>
    uint Length,
    /// <summary>Byte position of the encoded Offset value in the raw PSB.</summary>
    int PsbOffsetPos,
    /// <summary>Total encoded size of the Offset value (type byte + data bytes).</summary>
    int PsbOffsetEncSize,
    /// <summary>Byte position of the encoded Length value in the raw PSB.</summary>
    int PsbLengthPos,
    /// <summary>Total encoded size of the Length value (type byte + data bytes).</summary>
    int PsbLengthEncSize);

// ---- PSB v2 parser --------------------------------------------------------

/// <summary>
/// Minimal PSB version-2 parser that extracts file_info entries.
///
/// PSB v2 header layout (40 bytes, all fields little-endian except signature):
///   [0x00]  4B  signature "PSB\0" (big-endian bytes)
///   [0x04]  4B  type (= 2 for Wii U GBA VC)
///   [0x08]  4B  unknown1
///   [0x0C]  4B  offset_names
///   [0x10]  4B  offset_strings
///   [0x14]  4B  offset_strings_data
///   [0x18]  4B  offset_chunk_offsets
///   [0x1C]  4B  offset_chunk_lengths
///   [0x20]  4B  offset_chunk_data
///   [0x24]  4B  offset_entries
///
/// The entries section is a recursive packed structure.  Only the file_info
/// subtree is parsed; all other keys are ignored.
/// </summary>
internal sealed class PsbV2
{
    private readonly byte[] _data;
    private int _pos;

    // Header offsets
    private readonly int _offsetNames;
    private readonly int _offsetEntries;

    // Decoded names (trie)
    private string[] _names = [];

    public PsbV2(byte[] data)
    {
        _data = data;
        if (data.Length < 40) throw new InvalidDataException("PSB too short.");
        if (data[0] != 'P' || data[1] != 'S' || data[2] != 'B' || data[3] != 0)
            throw new InvalidDataException("Not a PSB file (missing 'PSB\\0' magic).");

        _offsetNames   = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12));
        _offsetEntries = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(36));

        DecodeNames();
    }

    // ---- public -------------------------------------------------------------

    /// <summary>Extract all file_info entries from the PSB.</summary>
    public List<PsbFileEntry> GetFileInfoEntries()
    {
        _pos = _offsetEntries;

        // Root must be a type-33 object
        byte rootType = ReadByte();
        if (rootType != 33)
            throw new InvalidDataException($"PSB root: expected type-33 object, got {rootType}.");

        var rootKeys    = ReadUintArray(); // name indexes
        var rootOffsets = ReadUintArray(); // value offsets (relative to values blob start)
        int rootBase    = _pos;

        // Find "file_info" among root keys
        int fileInfoNameIdx = Array.IndexOf(_names, "file_info");
        if (fileInfoNameIdx < 0)
            throw new InvalidDataException("'file_info' name not found in PSB names table.");

        int fileInfoPosInKeys = -1;
        for (int i = 0; i < rootKeys.Length; i++)
            if ((int)rootKeys[i] == fileInfoNameIdx) { fileInfoPosInKeys = i; break; }
        if (fileInfoPosInKeys < 0)
            throw new InvalidDataException("'file_info' key not found in PSB root object.");

        // Navigate to the file_info value
        _pos = rootBase + (int)rootOffsets[fileInfoPosInKeys];
        return ParseFileInfoObject();
    }

    // ---- PSB v2 structure parsing -------------------------------------------

    private List<PsbFileEntry> ParseFileInfoObject()
    {
        byte type = ReadByte();
        if (type != 33)
            throw new InvalidDataException($"file_info: expected type-33 object, got {type}.");

        var nameIdxs   = ReadUintArray(); // filename indexes into _names[]
        var valOffsets = ReadUintArray(); // value offsets from blob base
        int blobBase   = _pos;

        var result = new List<PsbFileEntry>(nameIdxs.Length);
        for (int i = 0; i < nameIdxs.Length; i++)
        {
            string name = _names[(int)nameIdxs[i]];
            _pos = blobBase + (int)valOffsets[i];

            byte arrType = ReadByte();
            if (arrType != 32)
                throw new InvalidDataException(
                    $"file_info['{name}']: expected type-32 array, got {arrType}.");

            // Offsets within the 2-element array [offset_value, length_value]
            var elemOffsets = ReadUintArray(); // should have 2 elements
            if (elemOffsets.Length < 2)
                throw new InvalidDataException(
                    $"file_info['{name}']: expected 2-element array, got {elemOffsets.Length}.");
            int elemBase = _pos;

            // Offset value
            _pos = elemBase + (int)elemOffsets[0];
            int offsetPos = _pos;
            (uint fileOffset, int offsetEncSize) = ReadUintEncoded();

            // Length value
            _pos = elemBase + (int)elemOffsets[1];
            int lengthPos = _pos;
            (uint fileLength, int lengthEncSize) = ReadUintEncoded();

            result.Add(new PsbFileEntry(name, fileOffset, fileLength,
                                        offsetPos, offsetEncSize,
                                        lengthPos, lengthEncSize));
        }
        return result;
    }

    // ---- low-level reading --------------------------------------------------

    private byte ReadByte() => _data[_pos++];

    /// <summary>
    /// Read a PSB variable-length unsigned integer array (types 13–20).
    /// Layout: [type_count_byte] [count_LE] [type_entry_byte] [entries_LE×count]
    /// </summary>
    private uint[] ReadUintArray()
    {
        byte cType = ReadByte();
        if (cType < 13 || cType > 20)
            throw new InvalidDataException($"ReadUintArray: unexpected type {cType} at pos {_pos - 1}.");
        int countSize = cType - 12;
        int count = (int)ReadUintN(countSize);

        byte eType = ReadByte();
        if (eType < 13 || eType > 20)
            throw new InvalidDataException($"ReadUintArray (entries): unexpected type {eType} at pos {_pos - 1}.");
        int entrySize = eType - 12;

        var arr = new uint[count];
        for (int i = 0; i < count; i++)
            arr[i] = ReadUintN(entrySize);
        return arr;
    }

    /// <summary>Read a PSB integer token (types 4–12) and return its value + encoded byte count.</summary>
    private (uint value, int encSize) ReadUintEncoded()
    {
        int start = _pos;
        byte t = ReadByte();
        if (t == 4) return (0u, 1);
        if (t is >= 5 and <= 12)
        {
            int bytes = t - 5 + 1;
            uint v = ReadUintN(bytes);
            return (v, 1 + bytes);
        }
        throw new InvalidDataException($"ReadUintEncoded: unexpected type {t} at pos {start}.");
    }

    private uint ReadUintN(int n)
    {
        uint v = 0;
        for (int i = 0; i < n; i++)
            v |= (uint)ReadByte() << (8 * i);
        return v;
    }

    // ---- names trie decoding ------------------------------------------------

    private void DecodeNames()
    {
        _pos = _offsetNames;
        var offsets = ReadUintArray();
        var jumps   = ReadUintArray();
        var starts  = ReadUintArray();

        _names = new string[starts.Length];
        for (int i = 0; i < starts.Length; i++)
            _names[i] = WalkTrie(offsets, jumps, starts[i]);
    }

    /// <summary>
    /// Walk the trie backwards from <paramref name="startJumpIdx"/> to reconstruct
    /// the name string.  Characters are collected in reverse then reversed.
    /// </summary>
    private static string WalkTrie(uint[] offsets, uint[] jumps, uint startJumpIdx)
    {
        // Collect characters from end-to-start
        var chars = new Stack<char>();
        uint b = startJumpIdx;
        while (b != 0)
        {
            uint parent  = jumps[b];       // parent's jump index
            uint parentOff = offsets[parent]; // parent's offset
            uint ch = b - parentOff;       // this character's code
            chars.Push((char)ch);
            b = parent;
        }
        // Stack<T>.ToArray() returns top-to-bottom == first-pushed-last (LIFO),
        // but since we pushed from last-char to first-char, the top IS the first char.
        return new string(chars.ToArray());
    }
}
