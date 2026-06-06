using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Converts GameCube/Wii images between formats (ISO, NKit, WBFS) using
/// the bundled ConvertToIso / ConvertToNKit external tools.
/// Replaces the legacy NKitService; works on Windows (native) and Linux (Wine).
/// </summary>
public static class NKitService
{
    /// <summary>
    /// Converts <paramref name="sourcePath"/> to a plain ISO.
    /// Returns the absolute path to the produced file in <paramref name="toolsPath"/>.
    /// </summary>
    public static async Task<string> ConvertToIsoAsync(
        string      toolsPath,
        string      sourcePath,
        string      outputFileName,
        IToolRunner runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);
        ArgumentNullException.ThrowIfNull(runner);

        CleanOldOutputs(toolsPath, Path.GetFileNameWithoutExtension(outputFileName));

        var result = await runner.RunAsync(
            "ConvertToIso",
            $"\"{sourcePath}\" \"{outputFileName}\"",
            workingDirectory: toolsPath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(
                $"ConvertToIso failed (exit {result.ExitCode}): {result.StandardError}");

        return ResolveOutput(toolsPath, outputFileName);
    }

    /// <summary>
    /// Converts <paramref name="sourcePath"/> to NKit format.
    /// Returns the absolute path to the produced file in <paramref name="toolsPath"/>.
    /// </summary>
    public static async Task<string> ConvertToNKitAsync(
        string      toolsPath,
        string      sourcePath,
        string      outputFileName,
        IToolRunner runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);
        ArgumentNullException.ThrowIfNull(runner);

        CleanOldOutputs(toolsPath, Path.GetFileNameWithoutExtension(outputFileName));

        var result = await runner.RunAsync(
            "ConvertToNKit",
            $"\"{sourcePath}\" \"{outputFileName}\"",
            workingDirectory: toolsPath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(
                $"ConvertToNKit failed (exit {result.ExitCode}): {result.StandardError}");

        return ResolveOutput(toolsPath, outputFileName);
    }

    // ---- helpers -----------------------------------------------------------

    private static string ResolveOutput(string toolsPath, string outputFileName)
    {
        var dest = Path.Combine(toolsPath, outputFileName);
        if (File.Exists(dest)) return dest;

        // Tool may have written with a slightly different name; pick newest match.
        var produced = PickNewest(Directory.GetFiles(toolsPath,
            Path.GetFileNameWithoutExtension(outputFileName) + "*", SearchOption.TopDirectoryOnly));

        if (produced is null)
            throw new FileNotFoundException(
                $"NKit tool did not produce expected output '{outputFileName}'.", outputFileName);

        if (!string.Equals(produced, dest, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(produced, dest);
        }

        return dest;
    }

    private static void CleanOldOutputs(string dir, string baseName)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, baseName + "*", SearchOption.TopDirectoryOnly))
                try { File.Delete(f); } catch { /* best-effort */ }
        }
        catch { /* best-effort */ }
    }

    private static string? PickNewest(string[] files)
    {
        if (files is null || files.Length == 0) return null;
        return files
            .Select(f => (path: f, time: File.GetLastWriteTimeUtc(f)))
            .OrderByDescending(x => x.time)
            .Select(x => x.path)
            .FirstOrDefault();
    }
}
