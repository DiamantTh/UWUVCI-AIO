namespace UWUVCI.Core.Pipeline;

/// <summary>
/// Injects a ROM into a Wii U base and produces a ready-to-install output folder.
/// Implementations live in the Tooling / Services layer; the Core layer only
/// defines this contract so that higher layers (UI, tests) can depend on the
/// abstraction without pulling in tool-execution details.
/// </summary>
public interface IInjectPipeline
{
    /// <summary>
    /// Run the injection pipeline to completion.
    /// </summary>
    /// <param name="context">All inputs required for injection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success, failure, or cancellation.</returns>
    Task<InjectionResult> InjectAsync(InjectionContext context, CancellationToken ct = default);
}
