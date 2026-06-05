namespace UWUVCI.Core.Pipeline;

/// <summary>
/// Discriminated union that signals how an injection job should finish.
/// </summary>
public enum InjectionOutcome
{
    /// <summary>Pipeline ran to completion and produced output.</summary>
    Success = 0,

    /// <summary>
    /// Pipeline failed with a recoverable or user-fixable error.
    /// The <see cref="InjectionResult.Errors"/> list has details.
    /// </summary>
    Failure = 1,

    /// <summary>Pipeline was cancelled via <see cref="System.Threading.CancellationToken"/>.</summary>
    Cancelled = 2,
}

/// <summary>
/// Result returned by <see cref="IInjectPipeline.InjectAsync"/>.
/// All properties are set by the pipeline implementation; UI code must not modify them.
/// </summary>
public sealed class InjectionResult
{
    public InjectionOutcome Outcome { get; init; }

    /// <summary>Absolute path to the finished inject folder, or null on failure.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Human-readable errors. Empty on success.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    // ---- factory helpers ---------------------------------------------------

    public static InjectionResult Ok(string outputPath) =>
        new() { Outcome = InjectionOutcome.Success, OutputPath = outputPath };

    public static InjectionResult Fail(params string[] errors) =>
        new() { Outcome = InjectionOutcome.Failure, Errors = errors };

    public static InjectionResult Fail(IReadOnlyList<string> errors) =>
        new() { Outcome = InjectionOutcome.Failure, Errors = errors };

    public static InjectionResult WasCancelled() =>
        new() { Outcome = InjectionOutcome.Cancelled };

    public bool IsSuccess   => Outcome == InjectionOutcome.Success;
    public bool IsFailure   => Outcome == InjectionOutcome.Failure;
    public bool IsCancelled => Outcome == InjectionOutcome.Cancelled;
}
