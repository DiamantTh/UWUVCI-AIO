using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace UWUVCI.Services;

/// <summary>
/// MArchive M_ cipher implementation for GBA Wii U Virtual Console files.
///
/// Implements the MDF container format used by the Wii U GBA emulator:
///   [4 bytes magic "mdf\0"] [4 bytes LE decompressed-size] [XOR-ciphered zlib data]
///
/// The XOR key is derived from a fixed seed + filename via MD5 → MT19937.
/// Algorithm documented from inject_gba by Andrew Dalgleish (GPL-compatible).
/// </summary>
internal static class MArchiveService
{
    /// Fixed seed from m2engage.elf (GBA Wii U VC emulator binary).
    private static readonly byte[] FixedSeed = Encoding.Latin1.GetBytes("MX8wgGEJ2+M47");
    private const int KeyLength = 0x50; // 80 bytes
    private static ReadOnlySpan<byte> MdfMagic => [0x6D, 0x64, 0x66, 0x00]; // "mdf\0"

    /// <summary>Returns true if <paramref name="data"/> starts with the MDF magic.</summary>
    public static bool IsMdf(byte[] data)
        => data.Length >= 8 && data.AsSpan(0, 4).SequenceEqual(MdfMagic);

    /// <summary>
    /// XOR-decrypt an MDF file in-place (symmetric operation: decrypt == encrypt).
    /// Returns a new byte array with the header unchanged and bytes [8..] XORed.
    /// If the data does not start with the MDF magic, returns the data unchanged.
    /// </summary>
    public static byte[] Decrypt(byte[] mdfData, string filename)
    {
        if (!IsMdf(mdfData)) return mdfData;

        var result = new byte[mdfData.Length];
        mdfData.CopyTo(result, 0);

        Span<byte> key = stackalloc byte[KeyLength];
        BuildKey(filename, key);

        for (int i = 8; i < result.Length; i++)
            result[i] ^= key[(i - 8) % KeyLength];

        return result;
    }

    /// <summary>
    /// Decompress the zlib payload from decrypted MDF data.
    /// Skips the 8-byte MDF header and inflates the remaining bytes.
    /// If data does not start with MDF magic, returns it unchanged (assumed raw).
    /// </summary>
    public static byte[] Decompress(byte[] decryptedMdf)
    {
        if (!IsMdf(decryptedMdf)) return decryptedMdf;

        using var src = new MemoryStream(decryptedMdf, 8, decryptedMdf.Length - 8);
        using var zlib = new ZLibStream(src, CompressionMode.Decompress);
        using var dst = new MemoryStream();
        zlib.CopyTo(dst);
        return dst.ToArray();
    }

    /// <summary>Decrypt + decompress an MDF file in a single call.</summary>
    public static byte[] DecryptAndDecompress(byte[] mdfData, string filename)
        => Decompress(Decrypt(mdfData, filename));

    /// <summary>
    /// Zlib-compress then MDF-encrypt raw bytes for writing.
    /// The <paramref name="filename"/> (basename, lowercased) is used for key derivation.
    /// </summary>
    public static byte[] CompressAndEncrypt(byte[] rawData, string filename)
    {
        // Compress with maximum compression (matches inject_gba level=9)
        byte[] compressed;
        using (var msComp = new MemoryStream())
        {
            using (var zlib = new ZLibStream(msComp, CompressionLevel.SmallestSize, leaveOpen: true))
                zlib.Write(rawData, 0, rawData.Length);
            compressed = msComp.ToArray();
        }

        // Build MDF: magic(4) + decompressed_size_LE(4) + compressed_data
        var mdf = new byte[8 + compressed.Length];
        MdfMagic.CopyTo(mdf);
        BinaryPrimitives.WriteUInt32LittleEndian(mdf.AsSpan(4), (uint)rawData.Length);
        compressed.CopyTo(mdf, 8);

        // XOR-encrypt bytes[8..]
        Span<byte> key = stackalloc byte[KeyLength];
        BuildKey(filename, key);
        for (int i = 8; i < mdf.Length; i++)
            mdf[i] ^= key[(i - 8) % KeyLength];

        return mdf;
    }

    // ---- key generation ----------------------------------------------------

    private static void BuildKey(string filename, Span<byte> key)
    {
        // hash_seed = fixed_seed + basename(filename).lower() (Latin-1)
        var baseName = Encoding.Latin1.GetBytes(Path.GetFileName(filename).ToLowerInvariant());
        var seedBuf = new byte[FixedSeed.Length + baseName.Length];
        FixedSeed.CopyTo(seedBuf, 0);
        baseName.CopyTo(seedBuf, FixedSeed.Length);

        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(seedBuf, hash);

        var initKey = new uint[4];
        for (int i = 0; i < 4; i++)
            initKey[i] = BinaryPrimitives.ReadUInt32LittleEndian(hash.Slice(i * 4, 4));

        var mt = new Mt19937();
        mt.InitByArray(initKey);

        for (int i = 0; i < KeyLength; i += 4)
        {
            uint r = mt.NextUInt32();
            int n = Math.Min(4, KeyLength - i);
            for (int j = 0; j < n; j++)
                key[i + j] = (byte)(r >> (8 * j));
        }
    }
}

/// <summary>
/// MT19937 Mersenne Twister pseudo-random number generator.
/// Ported from mt19937ar.c by M. Matsumoto and T. Nishimura (2002).
/// </summary>
internal sealed class Mt19937
{
    private const int N = 624;
    private const int M = 397;
    private const uint MatrixA  = 0x9908_b0dfU;
    private const uint UpperMask = 0x8000_0000U;
    private const uint LowerMask = 0x7fff_ffffU;

    private readonly uint[] _mt = new uint[N];
    private int _mti = N + 1;

    /// <summary>
    /// Initialize the RNG from an array of seed values (the inject_gba path uses
    /// 4 uint32s derived from an MD5 hash).
    /// </summary>
    public void InitByArray(uint[] initKey)
    {
        Init(19650218U);
        int i = 1, j = 0;
        int k = Math.Max(N, initKey.Length);
        for (; k > 0; k--)
        {
            _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1664525U)) + initKey[j] + (uint)j;
            if (++i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
            if (++j >= initKey.Length) j = 0;
        }
        for (k = N - 1; k > 0; k--)
        {
            _mt[i] = (_mt[i] ^ ((_mt[i - 1] ^ (_mt[i - 1] >> 30)) * 1566083941U)) - (uint)i;
            if (++i >= N) { _mt[0] = _mt[N - 1]; i = 1; }
        }
        _mt[0] = 0x8000_0000U; // MSB = 1: assure non-zero initial array
    }

    private void Init(uint seed)
    {
        _mt[0] = seed;
        for (_mti = 1; _mti < N; _mti++)
            _mt[_mti] = 1812433253U * (_mt[_mti - 1] ^ (_mt[_mti - 1] >> 30)) + (uint)_mti;
    }

    /// <summary>Generate the next pseudo-random uint32.</summary>
    public uint NextUInt32()
    {
        if (_mti >= N) Twist();
        uint y = _mt[_mti++];
        y ^= y >> 11;
        y ^= (y << 7)  & 0x9d2c_5680U;
        y ^= (y << 15) & 0xefc6_0000U;
        y ^= y >> 18;
        return y;
    }

    private void Twist()
    {
        int kk = 0;
        for (; kk < N - M; kk++) _mt[kk] = Step(_mt, kk, kk + 1, kk + M);
        for (; kk < N - 1;  kk++) _mt[kk] = Step(_mt, kk, kk + 1, kk + M - N);
        _mt[N - 1] = Step(_mt, N - 1, 0, M - 1);
        _mti = 0;
    }

    private static uint Step(uint[] mt, int k, int k1, int km)
    {
        uint y = (mt[k] & UpperMask) | (mt[k1] & LowerMask);
        return mt[km] ^ (y >> 1) ^ ((y & 1u) != 0 ? MatrixA : 0u);
    }
}
