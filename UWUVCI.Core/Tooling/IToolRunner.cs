namespace UWUVCI.Core.Tooling;

/// <summary>
/// Result of a tool execution.
/// </summary>
public sealed class ToolResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Runs external tools (processes). Abstracted so UI and injection code
/// never call Process.Start directly, and WASM can swap in a NoOpToolRunner.
/// </summary>
public interface IToolRunner
{
    /// <summary>
    /// Execute a tool by its logical name (resolved via <see cref="IToolResolver"/>).
    /// </summary>
    /// <param name="toolName">Logical tool name (e.g. "wit", "nfs2iso2nfs").</param>
    /// <param name="arguments">Argument string passed verbatim to the process.</param>
    /// <param name="workingDirectory">Working directory; null = current directory.</param>
    /// <param name="cancellationToken">Cancellation support.</param>
    Task<ToolResult> RunAsync(
        string toolName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if this runner can execute the named tool on the current platform.
    /// Use this before calling RunAsync to surface capability errors early.
    /// </summary>
    bool CanRun(string toolName);
}
