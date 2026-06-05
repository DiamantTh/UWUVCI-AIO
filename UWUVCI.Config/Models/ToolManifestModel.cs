namespace UWUVCI.Config.Models;

/// <summary>
/// Describes a single external tool entry in the tool manifest TOML.
/// </summary>
public sealed class ToolEntryModel
{
    /// <summary>Logical name used internally (e.g. "wit", "nfs2iso2nfs").</summary>
    public string Name { get; set; } = "";

    /// <summary>Filename on Windows (may be .exe or relative path).</summary>
    public string WindowsFileName { get; set; } = "";

    /// <summary>Filename on Linux (native binary name, no extension).</summary>
    public string LinuxFileName { get; set; } = "";

    /// <summary>
    /// If true the tool is a Windows .exe that runs via Wine on Linux.
    /// If false it has a native Linux binary.
    /// </summary>
    public bool WindowsOnly { get; set; } = false;

    /// <summary>Optional SHA-256 checksum for download verification (hex string).</summary>
    public string? Sha256 { get; set; } = null;

    /// <summary>Optional download URL.</summary>
    public string? DownloadUrl { get; set; } = null;
}

/// <summary>
/// Top-level TOML tool manifest: list of all required tools.
/// </summary>
public sealed class ToolManifestModel
{
    public IList<ToolEntryModel> Tools { get; set; } = new List<ToolEntryModel>();
}
