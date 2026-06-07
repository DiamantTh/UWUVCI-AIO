using Microsoft.VisualStudio.TestTools.UnitTesting;
using UWUVCI.Services;

namespace UWUVCI.Tests;

// ---------------------------------------------------------------------------
// MArchive cipher tests (GBA Wii U VC PSB.M format)
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class MArchiveTests
{
    // ---- MDF magic detection -----------------------------------------------

    [TestMethod]
    public void IsMdf_ValidMagic_ReturnsTrue()
    {
        var data = new byte[] { 0x6D, 0x64, 0x66, 0x00, 0x00, 0x10, 0x00, 0x00 };
        Assert.IsTrue(MArchiveService.IsMdf(data));
    }

    [TestMethod]
    public void IsMdf_InvalidMagic_ReturnsFalse()
    {
        var data = new byte[] { 0x50, 0x53, 0x42, 0x00, 0x00, 0x00, 0x00, 0x00 }; // "PSB\0"
        Assert.IsFalse(MArchiveService.IsMdf(data));
    }

    [TestMethod]
    public void IsMdf_TooShort_ReturnsFalse()
    {
        Assert.IsFalse(MArchiveService.IsMdf(new byte[] { 0x6D, 0x64, 0x66 }));
    }

    // ---- Non-MDF data passthrough ------------------------------------------

    [TestMethod]
    public void Decrypt_NonMdf_ReturnsUnchanged()
    {
        var data = new byte[] { 0x50, 0x53, 0x42, 0x00, 0x01, 0x02, 0x03, 0x04 };
        var result = MArchiveService.Decrypt(data, "test.dat");
        CollectionAssert.AreEqual(data, result);
    }

    [TestMethod]
    public void Decompress_NonMdf_ReturnsUnchanged()
    {
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        var result = MArchiveService.Decompress(data);
        CollectionAssert.AreEqual(data, result);
    }

    // ---- Encrypt→decrypt round-trip ----------------------------------------

    [TestMethod]
    public void EncryptDecrypt_RoundTrip_RestoresOriginal()
    {
        var original = new byte[256];
        for (int i = 0; i < original.Length; i++) original[i] = (byte)(i * 3 + 7);

        var encrypted = MArchiveService.CompressAndEncrypt(original, "alldata.psb.m");
        var decrypted = MArchiveService.DecryptAndDecompress(encrypted, "alldata.psb.m");

        CollectionAssert.AreEqual(original, decrypted);
    }

    [TestMethod]
    public void EncryptDecrypt_DifferentFilename_ProducesDifferentCiphertext()
    {
        var data = new byte[64];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)i;

        var enc1 = MArchiveService.CompressAndEncrypt(data, "alldata.psb.m");
        var enc2 = MArchiveService.CompressAndEncrypt(data, "system/roms/game.gba");

        // Different filenames should produce different ciphertexts
        Assert.IsTrue(enc1.Length != enc2.Length || !enc1.SequenceEqual(enc2),
            "Same input with different filenames should yield different ciphertext.");
    }

    [TestMethod]
    public void EncryptDecrypt_LargerPayload_RoundTrip()
    {
        // 4 KiB payload
        var original = new byte[4096];
        var rng = new Random(42);
        rng.NextBytes(original);

        var encrypted = MArchiveService.CompressAndEncrypt(original, "system/roms/test.gba");
        Assert.IsTrue(MArchiveService.IsMdf(encrypted), "Output should be MDF-wrapped.");

        var restored = MArchiveService.DecryptAndDecompress(encrypted, "system/roms/test.gba");
        CollectionAssert.AreEqual(original, restored);
    }

    [TestMethod]
    public void Decrypt_IsSymmetric_EncryptEqualsDecrypt()
    {
        // XOR is its own inverse: decrypt(encrypt(data)) == data
        // and encrypt(decrypt(data)) == data
        var original = new byte[128];
        new Random(99).NextBytes(original);

        var once = MArchiveService.CompressAndEncrypt(original, "test.dat");
        // Applying Decrypt to the encrypted output and re-encrypting should not
        // match (compression changes output), but XOR part is symmetric.
        // Verify directly: Decrypt(Decrypt(raw, f), f) leaves header, restores body.
        var mdfRaw = new byte[once.Length];
        once.CopyTo(mdfRaw, 0);
        var decOnce = MArchiveService.Decrypt(mdfRaw, "test.dat");
        var decTwice = MArchiveService.Decrypt(decOnce, "test.dat");
        CollectionAssert.AreEqual(mdfRaw, decTwice,
            "Double-decrypt should restore original (XOR symmetric).");
    }

    // ---- MDF header integrity ----------------------------------------------

    [TestMethod]
    public void CompressAndEncrypt_OutputStartsWithMdfMagic()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var out_ = MArchiveService.CompressAndEncrypt(data, "any.bin");
        Assert.IsTrue(MArchiveService.IsMdf(out_));
    }

    [TestMethod]
    public void CompressAndEncrypt_DecompressedSizeInHeader()
    {
        var data = new byte[1000];
        new Random(1).NextBytes(data);
        var enc = MArchiveService.CompressAndEncrypt(data, "test.bin");

        // Decrypt first to read the unobfuscated header
        var dec = MArchiveService.Decrypt(enc, "test.bin");
        uint storedSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(dec.AsSpan(4));
        Assert.AreEqual((uint)data.Length, storedSize);
    }
}

// ---------------------------------------------------------------------------
// MT19937 tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class Mt19937Tests
{
    // Reference output from mt19937ar.c with init_genrand(19650218) → init_by_array([0x123, 0x234, 0x345, 0x456])
    // First 4 known outputs from the original C reference implementation:
    private static readonly uint[] KnownOutput = [
        1067595299u, 955945823u, 477289528u, 4107218783u
    ];

    [TestMethod]
    public void InitByArray_KnownTestVector_MatchesReferenceOutput()
    {
        var mt = new Mt19937();
        mt.InitByArray([0x123u, 0x234u, 0x345u, 0x456u]);

        for (int i = 0; i < KnownOutput.Length; i++)
        {
            uint actual = mt.NextUInt32();
            Assert.AreEqual(KnownOutput[i], actual,
                $"Output[{i}] mismatch: expected {KnownOutput[i]}, got {actual}");
        }
    }

    [TestMethod]
    public void NextUInt32_ProducesNonZeroValues()
    {
        var mt = new Mt19937();
        mt.InitByArray([0xDEADBEEFu]);
        bool hasNonZero = false;
        for (int i = 0; i < 100; i++)
            if (mt.NextUInt32() != 0) { hasNonZero = true; break; }
        Assert.IsTrue(hasNonZero);
    }

    [TestMethod]
    public void InitByArray_DifferentSeed_ProducesDifferentOutput()
    {
        var mt1 = new Mt19937();
        mt1.InitByArray([1u, 2u, 3u, 4u]);

        var mt2 = new Mt19937();
        mt2.InitByArray([5u, 6u, 7u, 8u]);

        Assert.AreNotEqual(mt1.NextUInt32(), mt2.NextUInt32());
    }
}

// ---------------------------------------------------------------------------
// GbaPsbInjector helpers tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class GbaPsbInjectorTests
{
    // ---- UintEncodedSize ---------------------------------------------------

    [TestMethod]
    public void UintEncodedSize_Zero_Returns1()
        => Assert.AreEqual(1, GbaPsbInjector.UintEncodedSize(0u));

    [TestMethod]
    public void UintEncodedSize_1_Returns2()
        => Assert.AreEqual(2, GbaPsbInjector.UintEncodedSize(1u));

    [TestMethod]
    public void UintEncodedSize_127_Returns2()
        => Assert.AreEqual(2, GbaPsbInjector.UintEncodedSize(127u)); // 127 < 128 = 2^7

    [TestMethod]
    public void UintEncodedSize_128_Returns3()
        => Assert.AreEqual(3, GbaPsbInjector.UintEncodedSize(128u)); // 128 >= 128, needs 2 value bytes

    [TestMethod]
    public void UintEncodedSize_32MiB_Returns5()
    {
        uint v = 32u * 1024u * 1024u; // 0x02000000
        // 0x02000000 >= 0x800000 (2^23) but < 0x80000000 (2^31) → 4 value bytes + type = 5
        Assert.AreEqual(5, GbaPsbInjector.UintEncodedSize(v));
    }

    [TestMethod]
    public void UintEncodedSize_TypicalCompressedRomSize_Returns5()
    {
        // 15 MiB compressed = 0x00F00000; >= 2^23 → needs 4 bytes
        uint v = 15u * 1024u * 1024u;
        Assert.AreEqual(5, GbaPsbInjector.UintEncodedSize(v));
    }
}
