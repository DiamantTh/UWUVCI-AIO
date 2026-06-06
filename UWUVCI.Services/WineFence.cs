namespace UWUVCI.Services;

/// <summary>
/// Helpers that fence on Wine/cross-filesystem file visibility.
/// On Windows these are near-instant; on Linux/Wine they poll until the
/// file system notifies the host that the file exists and is stable.
/// </summary>
public static class WineFence
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for <paramref name="path"/> to
    /// exist and be non-empty (if <paramref name="requireNonEmpty"/> is true).
    /// Returns true when the file becomes visible; false on timeout.
    /// </summary>
    public static bool WaitForVisibility(
        string path,
        bool   requireNonEmpty    = true,
        int    timeoutMs          = 15_000,
        int    pollIntervalMs     = 250,
        bool   isDirectory        = false)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var poll     = TimeSpan.FromMilliseconds(pollIntervalMs);

        while (DateTime.UtcNow < deadline)
        {
            if (isDirectory)
            {
                if (Directory.Exists(path)) return true;
            }
            else
            {
                if (File.Exists(path) && (!requireNonEmpty || new FileInfo(path).Length > 0))
                    return true;
            }
            Thread.Sleep(poll);
        }
        return false;
    }

    /// <summary>
    /// Waits until the file size has been stable for one full poll interval,
    /// indicating the writing process has finished flushing.
    /// </summary>
    public static void WaitForStableSize(
        string path,
        int    timeoutMs      = 30_000,
        int    pollIntervalMs = 500)
    {
        if (!File.Exists(path)) return;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var poll     = TimeSpan.FromMilliseconds(pollIntervalMs);
        long prev    = -1;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                long cur = new FileInfo(path).Length;
                if (cur == prev && cur > 0) return;
                prev = cur;
            }
            catch { /* file might be locked, keep waiting */ }
            Thread.Sleep(poll);
        }
    }
}
