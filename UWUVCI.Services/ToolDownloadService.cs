using System.Security.Cryptography;
using UWUVCI.Config.Models;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Per-tool download/verify state reported to the UI.
/// </summary>
public sealed class ToolStatus
{
    public required string Name         { get; init; }
    public required bool   IsPresent    { get; init; }
    public bool            HasUrl       { get; init; }
    public bool            HasSha256    { get; init; }
    public string?         ResolvedPath { get; init; }
}

/// <summary>
/// Progress callback shape for <see cref="ToolDownloadService.DownloadToolAsync"/>.
/// </summary>
public delegate void DownloadProgressCallback(string toolName, long bytesReceived, long? totalBytes);

/// <summary>
/// Downloads individual tool binaries from their manifest-declared URLs,
/// writes them to the tools directory, and verifies the SHA-256 checksum.
/// </summary>
public static class ToolDownloadService
{
    // ---- status query -------------------------------------------------------

    /// <summary>
    /// Scans the tools directory and returns one <see cref="ToolStatus"/> per
    /// entry in the manifest.
    /// </summary>
    public static IReadOnlyList<ToolStatus> GetStatuses(
        ToolManifestModel manifest,
        IToolResolver resolver)
    {
        var result = new List<ToolStatus>(manifest.Tools.Count);
        foreach (var entry in manifest.Tools)
        {
            var path = resolver.Resolve(entry.Name);
            result.Add(new ToolStatus
            {
                Name         = entry.Name,
                IsPresent    = path is not null,
                HasUrl       = !string.IsNullOrWhiteSpace(entry.DownloadUrl),
                HasSha256    = !string.IsNullOrWhiteSpace(entry.Sha256),
                ResolvedPath = path,
            });
        }
        return result;
    }

    // ---- download -----------------------------------------------------------

    /// <summary>
    /// Downloads the binary for <paramref name="toolName"/> from its manifest
    /// URL, saves it to <paramref name="toolsDir"/>, and verifies the SHA-256
    /// digest when the manifest provides one.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the tool or its download URL is not in the manifest.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the downloaded checksum does not match the manifest value.
    /// </exception>
    public static async Task DownloadToolAsync(
        string toolName,
        ToolManifestModel manifest,
        string toolsDir,
        bool isLinux,
        DownloadProgressCallback? progress = null,
        CancellationToken ct = default)
    {
        var entry = manifest.Tools.FirstOrDefault(
            t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Tool '{toolName}' not found in manifest.");

        if (string.IsNullOrWhiteSpace(entry.DownloadUrl))
            throw new InvalidOperationException($"Tool '{toolName}' has no download URL in manifest.");

        Directory.CreateDirectory(toolsDir);

        // Determine destination filename
        string fileName = isLinux && !entry.WindowsOnly && !string.IsNullOrWhiteSpace(entry.LinuxFileName)
            ? entry.LinuxFileName
            : entry.WindowsFileName;

        var destPath = Path.Combine(toolsDir, fileName);
        var tempPath = destPath + ".tmp";

        using var http = new System.Net.Http.HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("UWUVCI/4 (+https://github.com/DiamantTh/UWUVCI-AIO)");

        using var response = await http.GetAsync(
            entry.DownloadUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        long? totalBytes = response.Content.Headers.ContentLength;

        try
        {
            await using var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
            using var sha = SHA256.Create();
            using var csa = new CryptoStream(dest, sha, CryptoStreamMode.Write, leaveOpen: true);

            var buffer = new byte[65536];
            long received = 0;
            await using var src = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            int read;
            while ((read = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await csa.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                received += read;
                progress?.Invoke(toolName, received, totalBytes);
            }

            csa.FlushFinalBlock();

            // Verify checksum
            if (!string.IsNullOrWhiteSpace(entry.Sha256))
            {
                var expected = entry.Sha256!.Replace(" ", "").ToUpperInvariant();
                var actual   = Convert.ToHexString(sha.Hash!);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"SHA-256 mismatch for '{toolName}': expected {expected}, got {actual}.");
                }
            }
        }
        catch
        {
            // Clean up partial download
            try { File.Delete(tempPath); } catch { /* best effort */ }
            throw;
        }

        // Atomic replace
        if (File.Exists(destPath))
            File.Delete(destPath);
        File.Move(tempPath, destPath);

        // Mark executable on Unix
        if (isLinux)
            SetExecutable(destPath);
    }

    // ---- helpers -----------------------------------------------------------

    private static void SetExecutable(string path)
    {
        try
        {
            // chmod +x via System.IO.UnixFileMode (net7+)
            File.SetUnixFileMode(path,
                File.GetUnixFileMode(path)
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute);
        }
        catch
        {
            /* best effort – irrelevant on Windows */
        }
    }
}
