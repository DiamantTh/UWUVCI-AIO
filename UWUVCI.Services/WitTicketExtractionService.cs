using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Extracts the TIK (ticket.bin) and TMD (tmd.bin) from a Wii ISO using the
/// bundled <c>wit</c> tool. These files are required for the NFS injection step.
/// </summary>
public static class WitTicketExtractionService
{
    /// <summary>
    /// Extracts ticket.bin and tmd.bin from <paramref name="gameIso"/> into
    /// <paramref name="tikTmdDir"/> using <c>wit extract --psel data</c>.
    /// </summary>
    public static async Task ExtractTicketsAsync(
        string      toolsPath,
        string      gameIso,
        string      tikTmdDir,
        IPlatformInfo platform,
        IToolRunner runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameIso);
        ArgumentException.ThrowIfNullOrWhiteSpace(tikTmdDir);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(platform);

        if (Directory.Exists(tikTmdDir))
            Directory.Delete(tikTmdDir, recursive: true);

        var witArgs = $"extract \"{gameIso}\" --psel data --files +tmd.bin --files +ticket.bin --DEST \"{tikTmdDir}\" -vv1";
        var result  = await runner.RunAsync("wit", witArgs, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException(
                $"wit extract (tickets) failed (exit {result.ExitCode}): {result.StandardError}");

        // Convert tool-side paths to host paths for fence check on Wine
        var tmdPath = ToHostPath(platform, Path.Combine(tikTmdDir, "tmd.bin"));
        var tikPath = ToHostPath(platform, Path.Combine(tikTmdDir, "ticket.bin"));

        if (!WineFence.WaitForVisibility(tmdPath) || !WineFence.WaitForVisibility(tikPath))
            throw new IOException($"wit extract completed but ticket files not visible: {tmdPath}, {tikPath}");

        if (new FileInfo(tmdPath).Length == 0 || new FileInfo(tikPath).Length == 0)
            throw new InvalidDataException("wit extract produced empty ticket.bin or tmd.bin.");
    }

    private static string ToHostPath(IPlatformInfo platform, string path) =>
        platform.IsWineLike ? platform.ToHostPath(path) : path;
}
