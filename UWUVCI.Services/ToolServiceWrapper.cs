using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Abstracts tool execution for cross-platform compatibility.
/// Windows-only tools (N64Converter, RetroInject) are wrapped here
/// to provide consistent IToolRunner interface on all platforms.
/// </summary>
public interface IToolService
{
    /// <summary>
    /// Gets the tool name (e.g., "N64Converter", "RetroInject").
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// Runs the tool with given arguments on the current platform.
    /// </summary>
    Task<int> RunAsync(string arguments, string? workingDirectory = null, CancellationToken ct = default);

    /// <summary>
    /// Checks if the tool is available on the current platform.
    /// </summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Tool service wrapper that delegates to IToolRunner for cross-platform execution.
/// For Windows-only tools, ensures same behavior on Linux via service abstraction.
/// </summary>
public class ToolServiceWrapper : IToolService
{
    private readonly IToolRunner _toolRunner;
    private readonly string _toolName;

    public ToolServiceWrapper(IToolRunner toolRunner, string toolName)
    {
        _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
        _toolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
    }

    public string ToolName => _toolName;

    public bool IsAvailable => _toolRunner.CanRun(_toolName);

    public async Task<int> RunAsync(string arguments, string? workingDirectory = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(arguments))
            throw new ArgumentNullException(nameof(arguments));

        var result = await _toolRunner.RunAsync(
            _toolName,
            arguments,
            workingDirectory: workingDirectory,
            cancellationToken: ct
        ).ConfigureAwait(false);

        return result.ExitCode;
    }
}

/// <summary>
/// Factory for creating tool service wrappers.
/// Manages tool availability and instantiation.
/// </summary>
public interface IToolServiceFactory
{
    /// <summary>
    /// Creates a tool service wrapper for the given tool name.
    /// </summary>
    IToolService CreateToolService(string toolName);

    /// <summary>
    /// Gets all available tool names for the current platform.
    /// </summary>
    IReadOnlyList<string> AvailableTools { get; }
}

public class ToolServiceFactory : IToolServiceFactory
{
    private readonly IToolRunner _toolRunner;
    private readonly IToolResolver _toolResolver;

    public ToolServiceFactory(IToolRunner toolRunner, IToolResolver toolResolver)
    {
        _toolRunner = toolRunner ?? throw new ArgumentNullException(nameof(toolRunner));
        _toolResolver = toolResolver ?? throw new ArgumentNullException(nameof(toolResolver));
    }

    public IToolService CreateToolService(string toolName)
    {
        if (string.IsNullOrEmpty(toolName))
            throw new ArgumentNullException(nameof(toolName));

        return new ToolServiceWrapper(_toolRunner, toolName);
    }

    public IReadOnlyList<string> AvailableTools
    {
        get
        {
            // Returns list of resolved tool names (from manifest)
            // This is a placeholder; actual implementation depends on IToolResolver
            return Array.Empty<string>();
        }
    }
}
