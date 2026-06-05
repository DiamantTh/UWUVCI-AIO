namespace UWUVCI.Core.Pipeline;

/// <summary>
/// Receives structured progress updates from an injection job.
/// Implementations may forward to a UI progress bar or a test accumulator.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Report the current progress.
    /// </summary>
    /// <param name="percent">0–100.</param>
    /// <param name="message">Human-readable status message.</param>
    void Report(int percent, string message);
}
