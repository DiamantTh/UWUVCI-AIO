namespace UWUVCI.Services;

/// <summary>
/// File/directory I/O utilities shared across injection services.
/// </summary>
public static class IOHelpers
{
    /// <summary>
    /// Moves <paramref name="sourceDir"/> to <paramref name="destDir"/>, falling
    /// back to a synchronous recursive copy if the atomic move fails
    /// (e.g. across file-system boundaries).
    /// </summary>
    public static void MoveOrCopyDirectory(string sourceDir, string destDir)
    {
        if (Directory.Exists(destDir))
            Directory.Delete(destDir, recursive: true);

        try
        {
            Directory.Move(sourceDir, destDir);
        }
        catch
        {
            CopyDirectory(sourceDir, destDir);
            try { Directory.Delete(sourceDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    /// <summary>Overwrites <paramref name="dest"/> with <paramref name="src"/>, deleting dest first if present.</summary>
    public static void MoveOverwrite(string src, string dest)
    {
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(src, dest);
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel    = file[src.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
