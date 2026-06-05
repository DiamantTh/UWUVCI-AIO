namespace UWUVCI.Core.Pipeline;

/// <summary>
/// Represents one discrete step in the injection pipeline.
/// Steps are composed by an orchestrator; each step should do one thing and
/// update context state or throw on unrecoverable errors.
/// </summary>
public interface IInjectStep
{
    /// <summary>Short human-readable name used in log output.</summary>
    string Name { get; }

    /// <summary>
    /// Execute the step.
    /// Progress and log calls should use <see cref="InjectionStepContext.Logger"/>
    /// and <see cref="InjectionStepContext.Progress"/> rather than static helpers.
    /// </summary>
    Task ExecuteAsync(InjectionStepContext ctx, CancellationToken ct = default);
}

/// <summary>
/// Mutable state shared between pipeline steps during one injection job.
/// </summary>
public sealed class InjectionStepContext
{
    public required InjectionContext Input { get; init; }

    /// <summary>Convenience shortcut.</summary>
    public IJobLogger Logger => Input.Logger;

    /// <summary>Convenience shortcut.</summary>
    public IProgressReporter Progress => Input.Progress;

    // ---- mutable step-output state ----------------------------------------

    /// <summary>Absolute path to the base-rom working copy after CopyBase.</summary>
    public string? BaseRomPath { get; set; }

    /// <summary>Absolute path to the temp image directory.</summary>
    public string? ImgPath { get; set; }

    /// <summary>
    /// Title product code written to meta.xml and used for output folder naming.
    /// </summary>
    public string? ProductCode { get; set; }

    /// <summary>Accumulated errors collected by steps (non-fatal or post-mortem).</summary>
    public List<string> Errors { get; } = [];
}
