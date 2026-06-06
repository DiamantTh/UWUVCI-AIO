using System.Buffers.Binary;
using System.IO.Compression;

namespace UWUVCI.Services;

/// <summary>
/// Native replacement for wiiurpxtool: compress or decompress Wii U RPX/RPL
/// (ELF-based executable) files.
/// Ported from wiiurpxtool.cpp by CBH (https://github.com/0CBH0/wiiurpxtool,
/// GPL-3.0). All values are big-endian (PowerPC).
/// </summary>
public static class WiiURpxService
{
    // ---- RPX/RPL ELF constants ---------------------------------------------
    private const uint   ElfMagic       = 0x7F454C46u;
    private const ushort EtRpx          = 0xFE01;
    private const uint   ShfRplZlib     = 0x08000000u;
    private const uint   ShtRplCrcs     = 0x80000003u;
    private const uint   ShtRplFileinfo = 0x80000004u;
    private const int    SectionAlign   = 0x40;

    // ---- Public API --------------------------------------------------------

    /// <summary>Decompress all zlib-compressed RPX sections in-place.</summary>
    public static void Decompress(string path) => ProcessInPlace(path, compress: false);

    /// <summary>Compress RPX sections with zlib in-place.</summary>
    public static void Compress(string path) => ProcessInPlace(path, compress: true);

    /// <summary>Decompress RPX from <paramref name="inputPath"/> to <paramref name="outputPath"/>.</summary>
    public static void Decompress(string inputPath, string outputPath) =>
        File.WriteAllBytes(outputPath, ProcessRpx(File.ReadAllBytes(inputPath), compress: false));

    /// <summary>Compress RPX from <paramref name="inputPath"/> to <paramref name="outputPath"/>.</summary>
    public static void Compress(string inputPath, string outputPath) =>
        File.WriteAllBytes(outputPath, ProcessRpx(File.ReadAllBytes(inputPath), compress: true));

    // ---- core implementation -----------------------------------------------

    private static void ProcessInPlace(string path, bool compress)
    {
        var result = ProcessRpx(File.ReadAllBytes(path), compress);
        var tmp    = path + ".rpxtmp";
        File.WriteAllBytes(tmp, result);
        File.Move(tmp, path, overwrite: true);
    }

    private static byte[] ProcessRpx(byte[] input, bool compress)
    {
        if (input.Length < 52)
            throw new InvalidDataException("File too short to be an RPX.");
        if (ReadU32BE(input, 0) != ElfMagic)
            throw new InvalidDataException("Not an ELF file (bad magic).");
        if (ReadU16BE(input, 16) != EtRpx)
            throw new InvalidDataException("Not an RPX/RPL file (e_type != 0xFE01).");

        uint   e_shoff     = ReadU32BE(input, 32);
        ushort e_shentsize = ReadU16BE(input, 46);
        ushort e_shnum     = ReadU16BE(input, 48);

        if (e_shentsize < 40)
            throw new InvalidDataException("Invalid ELF section header entry size.");

        // Position where section DATA begins in both input and output (fixed layout)
        long shdrDataOffset = (long)e_shoff + (long)e_shnum * e_shentsize;

        // Read all section headers (mutable — we will update sh_offset, sh_size, sh_flags)
        var shdrs = new ShdrEntry[e_shnum];
        for (int i = 0; i < e_shnum; i++)
        {
            long off = (long)e_shoff + (long)i * e_shentsize;
            shdrs[i] = new ShdrEntry
            {
                sh_name      = ReadU32BE(input, (int)off),
                sh_type      = ReadU32BE(input, (int)off +  4),
                sh_flags     = ReadU32BE(input, (int)off +  8),
                sh_addr      = ReadU32BE(input, (int)off + 12),
                sh_offset    = ReadU32BE(input, (int)off + 16),  // original input offset
                sh_size      = ReadU32BE(input, (int)off + 20),
                sh_link      = ReadU32BE(input, (int)off + 24),
                sh_info      = ReadU32BE(input, (int)off + 28),
                sh_addralign = ReadU32BE(input, (int)off + 32),
                sh_entsize   = ReadU32BE(input, (int)off + 36),
            };
        }

        // Sort by original file offset; skip null section (sh_offset == 0)
        var sortedIdx = Enumerable.Range(0, e_shnum)
            .Where(i => shdrs[i].sh_offset != 0)
            .OrderBy(i => shdrs[i].sh_offset)
            .ToArray();

        var  crcs          = new uint[e_shnum];
        long crcDataOffset = -1;

        // Output layout:
        //   [0 .. 64)               : ELF header (52 standard + 12 RPX-specific zeros)
        //   [64 .. e_shoff)         : padding zeros
        //   [e_shoff .. shdrDataOffset) : section header table placeholder (written last)
        //   [shdrDataOffset .. end) : section data (0x40-byte aligned)
        using var ms = new MemoryStream();

        // Write ELF header: standard 52 bytes from input, then 12 RPX-specific zero bytes
        ms.Write(input, 0, 52);
        WriteU32BE(ms, 0); WriteU32BE(ms, 0); WriteU32BE(ms, 0);

        // Pad to section header table start
        while (ms.Length < (long)e_shoff) ms.WriteByte(0);

        // Placeholder for section header table (zeroed; overwritten at end)
        for (int i = 0; i < e_shnum * e_shentsize; i++) ms.WriteByte(0);

        if (ms.Length != shdrDataOffset)
            throw new InvalidDataException(
                $"Section data start mismatch: expected {shdrDataOffset}, got {ms.Length}.");

        // Process each section in original file order
        foreach (int idx in sortedIdx)
        {
            // Save original input location before we update the section header
            uint inputOff  = shdrs[idx].sh_offset;
            uint inputSize = shdrs[idx].sh_size;

            if ((long)inputOff + inputSize > input.Length)
                throw new InvalidDataException($"Section {idx} data extends past end of file.");

            bool isZlib     = (shdrs[idx].sh_flags & ShfRplZlib) != 0;
            bool isCrcs     = shdrs[idx].sh_type == ShtRplCrcs;
            bool isFileinfo = shdrs[idx].sh_type == ShtRplFileinfo;

            // Update section header: new output offset starts here
            shdrs[idx].sh_offset = (uint)ms.Length;

            if (!compress)
            {
                // DECOMPRESS: inflate SHF_RPL_ZLIB sections
                if (isZlib)
                {
                    // [4 bytes BE = decompressed size] [zlib data]
                    uint uncompressedSize = ReadU32BE(input, (int)inputOff);
                    var  decompressed     = ZlibInflate(input, (int)inputOff + 4,
                                                        (int)inputSize - 4, (int)uncompressedSize);
                    crcs[idx]             = Crc32Rpx(decompressed, 0, decompressed.Length);
                    ms.Write(decompressed, 0, decompressed.Length);
                    shdrs[idx].sh_size   = (uint)decompressed.Length;
                    shdrs[idx].sh_flags &= ~ShfRplZlib;
                }
                else
                {
                    // Copy as-is; CRC computed on raw data
                    crcs[idx] = Crc32Rpx(input, (int)inputOff, (int)inputSize);
                    ms.Write(input, (int)inputOff, (int)inputSize);
                    if (isCrcs) crcs[idx] = 0;  // CRC section self-CRC is always 0
                }
            }
            else
            {
                // COMPRESS: deflate ordinary sections; skip CRCS/FILEINFO and already-zlib
                var raw = new ReadOnlySpan<byte>(input, (int)inputOff, (int)inputSize);
                crcs[idx] = Crc32Rpx(input, (int)inputOff, (int)inputSize);  // CRC of uncompressed

                if (!isZlib && !isCrcs && !isFileinfo)
                {
                    byte[] compressed = ZlibDeflate(raw.ToArray());
                    if (compressed.Length + 4 < raw.Length)
                    {
                        // Compressed form is smaller: write [BE uncompressed size][zlib data]
                        WriteU32BE(ms, (uint)raw.Length);
                        ms.Write(compressed, 0, compressed.Length);
                        shdrs[idx].sh_size   = (uint)(compressed.Length + 4);
                        shdrs[idx].sh_flags |= ShfRplZlib;
                    }
                    else
                    {
                        // Compression not beneficial; write raw
                        ms.Write(raw);
                    }
                }
                else
                {
                    // CRCS / FILEINFO / already-compressed: copy verbatim
                    ms.Write(raw);
                    if (isCrcs) crcs[idx] = 0;
                }
            }

            // Track CRC section's new output offset (needed to patch CRCs at end)
            if (isCrcs) crcDataOffset = (long)shdrs[idx].sh_offset;

            // Pad to 0x40-byte alignment between sections
            while (ms.Length % SectionAlign != 0) ms.WriteByte(0);
        }

        // Materialise the output
        var output = ms.ToArray();

        // Write updated section headers back at e_shoff
        for (int i = 0; i < e_shnum; i++)
        {
            int off = (int)e_shoff + i * e_shentsize;
            PatchU32BE(output, off,       shdrs[i].sh_name);
            PatchU32BE(output, off +  4,  shdrs[i].sh_type);
            PatchU32BE(output, off +  8,  shdrs[i].sh_flags);
            PatchU32BE(output, off + 12,  shdrs[i].sh_addr);
            PatchU32BE(output, off + 16,  shdrs[i].sh_offset);
            PatchU32BE(output, off + 20,  shdrs[i].sh_size);
            PatchU32BE(output, off + 24,  shdrs[i].sh_link);
            PatchU32BE(output, off + 28,  shdrs[i].sh_info);
            PatchU32BE(output, off + 32,  shdrs[i].sh_addralign);
            PatchU32BE(output, off + 36,  shdrs[i].sh_entsize);
        }

        // Write freshly-computed CRCs into the CRC section
        if (crcDataOffset >= 0)
        {
            for (int i = 0; i < e_shnum; i++)
                PatchU32BE(output, (int)crcDataOffset + i * 4, crcs[i]);
        }

        return output;
    }

    // ---- zlib helpers ------------------------------------------------------
    private static byte[] ZlibInflate(byte[] data, int offset, int count, int expectedSize)
    {
        using var src = new MemoryStream(data, offset, count);
        using var zs  = new ZLibStream(src, CompressionMode.Decompress);
        using var dst = new MemoryStream(expectedSize);
        zs.CopyTo(dst);
        return dst.ToArray();
    }

    private static byte[] ZlibDeflate(byte[] data)
    {
        using var dst = new MemoryStream();
        using (var zs = new ZLibStream(dst, CompressionLevel.Optimal, leaveOpen: true))
            zs.Write(data, 0, data.Length);
        return dst.ToArray();
    }

    // ---- CRC32 (IEEE, matching zlib / wiiurpxtool) -------------------------
    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    private static uint Crc32Rpx(byte[] buf, int offset, int len)
    {
        uint crc = ~0u;
        for (int i = 0; i < len; i++)
            crc = (crc >> 8) ^ Crc32Table[(crc ^ buf[offset + i]) & 0xFF];
        return ~crc;
    }

    // ---- binary helpers ----------------------------------------------------
    private static uint   ReadU32BE(byte[] d, int o) => BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
    private static ushort ReadU16BE(byte[] d, int o) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(o));

    private static void WriteU32BE(Stream s, uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, v);
        s.Write(b);
    }

    private static void PatchU32BE(byte[] d, int o, uint v) =>
        BinaryPrimitives.WriteUInt32BigEndian(d.AsSpan(o), v);

    // ---- section header struct ---------------------------------------------
    private struct ShdrEntry
    {
        public uint sh_name, sh_type, sh_flags, sh_addr;
        public uint sh_offset, sh_size, sh_link, sh_info;
        public uint sh_addralign, sh_entsize;
    }
}
