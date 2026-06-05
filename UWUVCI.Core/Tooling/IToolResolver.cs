namespace UWUVCI.Core.Tooling;

/// <summary>
/// Resolves the absolute executable path for a logical tool name.
/// </summary>
public interface IToolResolver
{
    /// <summary>
    /// Resolve the path to the executable for the given logical tool name.
    /// Returns null when the tool is not found in the tools directory.
    /// </summary>
    string? Resolve(string toolName);

    /// <summary>
    /// Returns true when the tool binary exists on disk for the current platform.
    /// </summary>
    bool IsAvailable(string toolName);
}
