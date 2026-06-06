using System.Security.Cryptography;

namespace UWUVCI.Services;

/// <summary>
/// Native replacement for nfs2iso2nfs: encrypts a Wii/GCN game.iso to the
/// NFS (Nintendo File System) split-file format used by Wii U Virtual Console,
/// and decrypts it back.
///
/// NFS format:
///   - Files: hif_000000.nfs, hif_000001.nfs, ...
///   - First file starts with a 0x200-byte EGGS header, then encrypted ISO data.
///   - Subsequent files contain only encrypted ISO data.
///   - Encryption: AES-128-CBC, key from <c>code/*.key</c>, IV = all zeros.
///   - The CBC state is continuous across all files (single stream).
/// </summary>
public static class NfsConverter
{
    // Each NFS file holds this many bytes of encrypted ISO data (except the last).
    // Matches the chunk size used by nfs2iso2nfs.
    private const int ChunkSize  = 0xFA00000; // 261,095,424 bytes ≈ 249 MiB
    private const int HeaderSize = 0x200;

    private static readonly byte[] NfsMagic   = { 0x45, 0x47, 0x47, 0x53 }; // "EGGS"
    private static readonly byte[] NfsVersion = { 0x00, 0x00, 0x00, 0x01 };

    // ---- Public API --------------------------------------------------------

    /// <summary>
    /// Encrypts <paramref name="isoPath"/> to NFS files in <paramref name="contentDir"/>.
    /// The AES key is read from <paramref name="keyPath"/> (must be exactly 16 bytes).
    /// Existing hif_*.nfs files in <paramref name="contentDir"/> are deleted first.
    /// </summary>
    public static void EncryptIsoToNfs(string isoPath, string contentDir, string keyPath)
    {
        byte[] key = ReadKey(keyPath);
        byte[] iso = File.ReadAllBytes(isoPath);

        // Pad ISO to AES block boundary (16 bytes)
        int padded = (iso.Length + 15) & ~15;
        if (padded != iso.Length)
            Array.Resize(ref iso, padded);

        // AES-128-CBC encrypt (single stream, IV = 0)
        byte[] encrypted = AesCbcEncrypt(iso, key);

        // Split into chunks and write NFS files
        int   numChunks = (encrypted.Length + ChunkSize - 1) / ChunkSize;
        int   lastChunkSize = encrypted.Length - (numChunks - 1) * ChunkSize;

        // Remove stale NFS files
        foreach (var f in Directory.EnumerateFiles(contentDir, "hif_*.nfs"))
            File.Delete(f);

        for (int i = 0; i < numChunks; i++)
        {
            int offset    = i * ChunkSize;
            int chunkLen  = (i < numChunks - 1) ? ChunkSize : lastChunkSize;
            string nfsPath = Path.Combine(contentDir, $"hif_{i:D6}.nfs");

            using var nfs = new FileStream(nfsPath, FileMode.Create, FileAccess.Write);

            if (i == 0)
            {
                // Write EGGS header (0x200 bytes)
                nfs.Write(NfsMagic,   0, 4);
                nfs.Write(NfsVersion, 0, 4);
                WriteU32BE(nfs, (uint)numChunks);
                WriteU32BE(nfs, (uint)lastChunkSize);
                // Pad header to 0x200
                for (int p = 16; p < HeaderSize; p++) nfs.WriteByte(0);
            }

            nfs.Write(encrypted, offset, chunkLen);
        }
    }

    /// <summary>
    /// Decrypts NFS files in <paramref name="contentDir"/> to <paramref name="outputIsoPath"/>.
    /// The AES key is read from <paramref name="keyPath"/>.
    /// </summary>
    public static void DecryptNfsToIso(string contentDir, string keyPath, string outputIsoPath)
    {
        byte[] key = ReadKey(keyPath);

        // Discover and sort NFS files
        var nfsFiles = Directory.GetFiles(contentDir, "hif_*.nfs")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (nfsFiles.Length == 0)
            throw new FileNotFoundException("No hif_*.nfs files found.", contentDir);

        // Read EGGS header from first file
        using var first = new FileStream(nfsFiles[0], FileMode.Open, FileAccess.Read);

        var header = new byte[HeaderSize];
        _ = first.Read(header, 0, HeaderSize);

        if (header[0] != 0x45 || header[1] != 0x47 || header[2] != 0x47 || header[3] != 0x53)
            throw new InvalidDataException("Invalid NFS header: missing EGGS magic.");

        uint numFiles      = ReadU32BE(header, 8);
        uint lastFileSize  = ReadU32BE(header, 12);

        if (numFiles != nfsFiles.Length)
            throw new InvalidDataException(
                $"NFS header says {numFiles} files but {nfsFiles.Length} found.");

        // Assemble all encrypted data (skip header in first file)
        long totalEncrypted = (long)(numFiles - 1) * ChunkSize + lastFileSize;
        var  encrypted      = new byte[totalEncrypted];
        long writePos       = 0;

        // First file: already past header from above
        using (var ms = new MemoryStream())
        {
            first.CopyTo(ms);
            var chunk = ms.ToArray();
            Array.Copy(chunk, 0, encrypted, writePos, Math.Min(chunk.Length, totalEncrypted));
            writePos += chunk.Length;
        }

        // Remaining files
        for (int i = 1; i < nfsFiles.Length; i++)
        {
            var chunk = File.ReadAllBytes(nfsFiles[i]);
            long copyLen = Math.Min(chunk.Length, totalEncrypted - writePos);
            Array.Copy(chunk, 0, encrypted, writePos, (int)copyLen);
            writePos += (int)copyLen;
        }

        // AES-128-CBC decrypt (IV = 0)
        byte[] iso = AesCbcDecrypt(encrypted, key);
        File.WriteAllBytes(outputIsoPath, iso);
    }

    // ---- Helpers -----------------------------------------------------------

    private static byte[] ReadKey(string keyPath)
    {
        var key = File.ReadAllBytes(keyPath);
        if (key.Length != 16)
            throw new InvalidDataException(
                $"NFS key file must be exactly 16 bytes (AES-128), got {key.Length}: {keyPath}");
        return key;
    }

    /// <summary>
    /// Finds the first *.key file in the <c>code/</c> subdirectory relative to
    /// <paramref name="baseRomPath"/>.
    /// </summary>
    public static string FindKeyFile(string baseRomPath)
    {
        var codeDir = Path.Combine(baseRomPath, "code");
        var key     = Directory.GetFiles(codeDir, "*.key").FirstOrDefault()
                      ?? throw new FileNotFoundException(
                             "No *.key file found in base title code/ directory.", codeDir);
        return key;
    }

    private static byte[] AesCbcEncrypt(byte[] data, byte[] key)
    {
        using var aes     = Aes.Create();
        aes.Key     = key;
        aes.IV      = new byte[16]; // all zeros
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var enc = aes.CreateEncryptor();
        return enc.TransformFinalBlock(data, 0, data.Length);
    }

    private static byte[] AesCbcDecrypt(byte[] data, byte[] key)
    {
        using var aes     = Aes.Create();
        aes.Key     = key;
        aes.IV      = new byte[16]; // all zeros
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(data, 0, data.Length);
    }

    private static void WriteU32BE(Stream s, uint value)
    {
        s.WriteByte((byte)(value >> 24));
        s.WriteByte((byte)(value >> 16));
        s.WriteByte((byte)(value >>  8));
        s.WriteByte((byte)(value >>  0));
    }

    private static uint ReadU32BE(byte[] d, int o) =>
        ((uint)d[o] << 24) | ((uint)d[o+1] << 16) | ((uint)d[o+2] << 8) | d[o+3];
}
