namespace UWUVCI.Core.Pipeline;

/// <summary>IJobLogger that accumulates messages in memory – useful in tests.</summary>
public sealed class CapturingJobLogger : IJobLogger
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Log(string message)      => _entries.Add($"[INFO]  {message}");
    public void LogError(string message, Exception? ex = null)
        => _entries.Add(ex is null ? $"[ERROR] {message}" : $"[ERROR] {message}: {ex.Message}");
}
