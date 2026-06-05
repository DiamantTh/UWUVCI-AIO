namespace UWUVCI.Core.Pipeline;

/// <summary>IProgressReporter that accumulates reports in memory – useful in tests.</summary>
public sealed class CapturingProgressReporter : IProgressReporter
{
    private readonly List<(int Percent, string Message)> _reports = [];

    public IReadOnlyList<(int Percent, string Message)> Reports => _reports;

    public int LastPercent => _reports.Count == 0 ? 0 : _reports[^1].Percent;

    public void Report(int percent, string message) => _reports.Add((percent, message));
}
