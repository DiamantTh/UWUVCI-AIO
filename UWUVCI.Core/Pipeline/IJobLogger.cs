namespace UWUVCI.Core.Pipeline;

/// <summary>
/// Structured logger for injection jobs.
/// Implementations may write to a file, in-memory list, or the console.
/// </summary>
public interface IJobLogger
{
    void Log(string message);
    void LogError(string message, Exception? ex = null);
}
