using UWUVCI.Core.Tooling;

namespace UWUVCI.Tooling;

/// <summary>
/// No-op implementation of <see cref="IToolRunner"/> for environments where
/// process execution is unavailable (e.g., future WASM target).
/// Every call returns a clear capability-unavailable error result.
/// </summary>
public sealed class NoOpToolRunner : IToolRunner
{
    public static readonly NoOpToolRunner Instance = new();

    public bool CanRun(string toolName) => false;

    public Task<ToolResult> RunAsync(
        string toolName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolResult
        {
            ExitCode      = -1,
            StandardError = $"Tool execution is not available on this platform (tool: '{toolName}').",
        });
    }
}
