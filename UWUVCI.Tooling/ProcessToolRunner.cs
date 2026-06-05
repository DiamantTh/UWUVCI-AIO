using System.Diagnostics;
using System.Text;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Tooling;

/// <summary>
/// Runs external tools as real child processes.
/// On Linux/Wine: .exe files are automatically prefixed with "wine".
/// Wine wait delay is configurable for slow Wine startup scenarios.
/// </summary>
public sealed class ProcessToolRunner : IToolRunner
{
    private readonly IToolResolver _resolver;
    private readonly IPlatformInfo _platform;
    private readonly int _wineWaitDelayMs;

    public ProcessToolRunner(
        IToolResolver resolver,
        IPlatformInfo platform,
        int wineWaitDelayMs = 0)
    {
        _resolver = resolver;
        _platform = platform;
        _wineWaitDelayMs = wineWaitDelayMs;
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

        string exe;
        string args;

        bool isWindowsExe = toolPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                         || toolPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

        if (_platform.IsWineLike && isWindowsExe)
        {
            // Run via Wine
            exe  = "wine";
            args = Quote(toolPath) + (string.IsNullOrEmpty(arguments) ? "" : " " + arguments);
        }
        else if (_platform.IsLinux && isWindowsExe)
        {
            // Linux but no Wine detected – best effort (may fail)
            exe  = "wine";
            args = Quote(toolPath) + (string.IsNullOrEmpty(arguments) ? "" : " " + arguments);
        }
        else
        {
            exe  = toolPath;
            args = arguments;
        }

        var psi = new ProcessStartInfo(exe, args)
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

        if (_platform.IsWineLike && _wineWaitDelayMs > 0)
            await Task.Delay(_wineWaitDelayMs, cancellationToken).ConfigureAwait(false);

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
