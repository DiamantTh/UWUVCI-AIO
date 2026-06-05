namespace UWUVCI.Core.Pipeline;

/// <summary>No-op progress reporter for tests and headless use.</summary>
public sealed class NullProgressReporter : IProgressReporter
{
    public static readonly NullProgressReporter Instance = new();
    public void Report(int percent, string message) { }
}
