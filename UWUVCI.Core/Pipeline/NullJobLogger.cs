namespace UWUVCI.Core.Pipeline;

/// <summary>No-op logger for tests and headless use.</summary>
public sealed class NullJobLogger : IJobLogger
{
    public static readonly NullJobLogger Instance = new();
    public void Log(string message) { }
    public void LogError(string message, Exception? ex = null) { }
}
