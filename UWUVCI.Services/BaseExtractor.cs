using System.IO.Compression;
using System.Security.Cryptography;

namespace UWUVCI.Services;

/// <summary>
/// Extracts and caches the BASE.zip archive used as the Wii U title base.
/// Cache key is the MD5 of the zip so a changed archive auto-re-extracts.
/// Uses AppData paths (never writes to the tools directory).
/// </summary>
public static class BaseExtractor
{
    /// <summary>
    /// Returns the path to the extracted BASE directory.
    /// Caches under <see cref="UWUVCI.Core.Runtime.AppDataPaths.CacheDir"/>.
    /// </summary>
    public static string GetOrExtractBase(string toolsPath, string baseZipName = "BASE.zip")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsPath);

        var zipPath = Path.Combine(toolsPath, baseZipName);
        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"BASE archive not found: {zipPath}", zipPath);

        var cacheRoot = UWUVCI.Core.Runtime.AppDataPaths.CacheDir;
        Directory.CreateDirectory(cacheRoot);

        var hash     = ComputeMd5(zipPath);
        var keyName  = Path.GetFileNameWithoutExtension(baseZipName) + "_" + hash;
        var cacheDir = Path.Combine(cacheRoot, keyName);
        var marker   = Path.Combine(cacheDir, ".ok");
        var baseDir  = Path.Combine(cacheDir, "BASE");

        if (Directory.Exists(baseDir) && File.Exists(marker))
            return baseDir;

        // (Re-)extract
        if (Directory.Exists(cacheDir))
            Directory.Delete(cacheDir, recursive: true);

        Directory.CreateDirectory(cacheDir);
        ZipFile.ExtractToDirectory(zipPath, cacheDir);

        if (!Directory.Exists(baseDir))
            throw new DirectoryNotFoundException(
                $"Expected 'BASE' folder not found after extracting {baseZipName}.");

        File.WriteAllText(marker, DateTime.UtcNow.ToString("o"));
        return baseDir;
    }

    /// <summary>Removes all cached BASE extractions.</summary>
    public static bool ClearCache()
    {
        try
        {
            var cacheRoot = UWUVCI.Core.Runtime.AppDataPaths.CacheDir;
            if (!Directory.Exists(cacheRoot)) return true;

            foreach (var dir in Directory.GetDirectories(cacheRoot))
                Directory.Delete(dir, recursive: true);

            return true;
        }
        catch { return false; }
    }

    private static string ComputeMd5(string path)
    {
        using var md5 = MD5.Create();
        using var fs  = File.OpenRead(path);
        return Convert.ToHexString(md5.ComputeHash(fs)).ToLowerInvariant();
    }
}
