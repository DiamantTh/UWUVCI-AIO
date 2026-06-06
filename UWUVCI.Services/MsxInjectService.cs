namespace UWUVCI.Services;

/// <summary>
/// Injects an MSX ROM into a Wii U Virtual Console base.
/// The base ships a msx.pkg whose first 0x580B3 bytes form the loader header;
/// this service replaces the payload while preserving that header.
/// </summary>
public static class MsxInjectService
{
    private const int HeaderLength = 0x580B3;
    private const string PkgRelative = "content/msx/msx.pkg";

    public static async Task InjectAsync(
        string baseRomPath,
        string romPath,
        CancellationToken cancellationToken = default)
    {
        var pkgPath = Path.Combine(baseRomPath, PkgRelative);
        if (!File.Exists(pkgPath))
            throw new FileNotFoundException("msx.pkg not found in base.", pkgPath);

        // Read the loader header (first HeaderLength bytes)
        await using var pkg = new FileStream(pkgPath, FileMode.Open, FileAccess.Read);
        long pkgLen = pkg.Length;
        int headerBytes = (int)Math.Min(HeaderLength, pkgLen);

        var header = new byte[headerBytes];
        await pkg.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        pkg.Dispose();

        // Delete the original and write header + ROM payload
        File.Delete(pkgPath);

        await using var dst = new FileStream(pkgPath, FileMode.Create, FileAccess.Write);
        await dst.WriteAsync(header, cancellationToken).ConfigureAwait(false);

        await using var rom = new FileStream(romPath, FileMode.Open, FileAccess.Read);
        await rom.CopyToAsync(dst, cancellationToken).ConfigureAwait(false);
    }
}
