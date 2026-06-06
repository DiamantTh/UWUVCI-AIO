using System.Diagnostics;
using System.Text;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Tooling;

/// <summary>
/// Runs external tools as real child processes.
/// All remaining external tools (wit) are native Linux/Windows binaries;
/// Wine wrapping is no longer required.
/// </summary>
public sealed class ProcessToolRunner : IToolRunner
{
    private readonly IToolResolver _resolver;

    public ProcessToolRunner(
        IToolResolver resolver,
        IPlatformInfo platform,    // kept for API compatibility
        int wineWaitDelayMs = 0)   // kept for API compatibility
    {
        _resolver = resolver;
    }

    public bool CanRun(string toolName) => _resolver.IsAvailable(toolName);

    public async Task<ToolResult> RunAsync(
        string toolName,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var toolPath = _resolver.Resolve(toolName)
            ?? throw new InvalidOperationException($"Tool '{toolName}' not found.");

        var psi = new ProcessStartInfo(toolPath, arguments)
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            WorkingDirectory       = workingDirectory ?? Directory.GetCurrentDirectory(),
        };

        var stdoutBuf = new StringBuilder();
        var stderrBuf = new StringBuilder();

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuf.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderrBuf.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new ToolResult
        {
            ExitCode       = process.ExitCode,
            StandardOutput = stdoutBuf.ToString(),
            StandardError  = stderrBuf.ToString(),
        };
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";
}
