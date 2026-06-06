using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Manages Wii U Base ROM downloads and verification.
/// Mirrors ToolDownloadService pattern but for base ROMs (larger files, region-based).
/// </summary>
public class BaseDownloadService
{
    public class BaseStatus
    {
        public string Name { get; set; } = "";
        public string Region { get; set; } = "";
        public bool IsPresent { get; set; }
        public bool HasUrl { get; set; }
        public bool HasSha256 { get; set; }
        public string? ResolvedPath { get; set; }
        public string StatusText { get; set; } = "";
    }

    public delegate void DownloadProgressCallback(string baseName, long bytesReceived, long? totalBytes);

    private readonly HttpClient _httpClient;

    public BaseDownloadService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <summary>
    /// Get status of all configured bases.
    /// </summary>
    public IReadOnlyList<BaseStatus> GetStatuses(string basesDir)
    {
        var statuses = new List<BaseStatus>();

        // UWUVCI bases: USA, EUR, JPN for WiiU
        var bases = new[]
        {
            ("USA", "WiiU_USA.zip"),
            ("EUR", "WiiU_EUR.zip"),
            ("JPN", "WiiU_JPN.zip"),
        };

        foreach (var (region, filename) in bases)
        {
            var path = Path.Combine(basesDir, filename);
            statuses.Add(new BaseStatus
            {
                Name = $"Wii U Base ({region})",
                Region = region,
                IsPresent = File.Exists(path),
                ResolvedPath = path,
                StatusText = File.Exists(path) ? "✓ Present" : "✗ Missing",
                HasUrl = true,  // Will be filled from config
                HasSha256 = true, // Will be filled from config
            });
        }

        return statuses;
    }

    /// <summary>
    /// Download a base ROM with SHA-256 verification.
    /// </summary>
    public async Task<bool> DownloadBaseAsync(
        string baseName,
        string downloadUrl,
        string expectedSha256,
        string basesDir,
        DownloadProgressCallback? onProgress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(downloadUrl))
            throw new ArgumentNullException(nameof(downloadUrl));
        if (string.IsNullOrEmpty(expectedSha256))
            throw new ArgumentNullException(nameof(expectedSha256));

        Directory.CreateDirectory(basesDir);
        var targetPath = Path.Combine(basesDir, $"{baseName}.zip.tmp");
        var finalPath = Path.Combine(basesDir, $"{baseName}.zip");

        try
        {
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var contentLength = response.Content.Headers.ContentLength;

                using (var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                using (var sha256 = SHA256.Create())
                using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    long bytesRead = 0;
                    int read;

                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                        sha256.TransformBlock(buffer, 0, read, null, 0);
                        bytesRead += read;
                        onProgress?.Invoke(baseName, bytesRead, contentLength);
                    }

                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    var hash = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();

                    if (!hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(targetPath);
                        throw new InvalidOperationException($"SHA-256 mismatch for {baseName}: expected {expectedSha256}, got {hash}");
                    }
                }
            }

            // Atomic replace
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            File.Move(targetPath, finalPath, overwrite: false);

            return true;
        }
        catch
        {
            if (File.Exists(targetPath))
                File.Delete(targetPath);
            throw;
        }
    }

    /// <summary>
    /// Download all missing bases asynchronously.
    /// </summary>
    public async Task DownloadAllMissingAsync(
        string basesDir,
        Dictionary<string, (string url, string sha256)> baseConfigs,
        DownloadProgressCallback? onProgress = null,
        CancellationToken ct = default)
    {
        var statuses = GetStatuses(basesDir);
        var missing = statuses.Where(s => !s.IsPresent).ToList();

        foreach (var base_ in missing)
        {
            if (baseConfigs.TryGetValue(base_.Region, out var config))
            {
                onProgress?.Invoke(base_.Name, 0, null);
                await DownloadBaseAsync(base_.Name, config.url, config.sha256, basesDir, onProgress, ct).ConfigureAwait(false);
            }
        }
    }
}
