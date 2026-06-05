namespace UWUVCI.Core.Pipeline;

/// <summary>
/// Console-specific inject options passed to tool-level services.
/// Mirrors the legacy InjectOptions.cs in the old Services directory,
/// but lives in Core so that it carries no WPF / tool-runner references.
/// </summary>
public enum InjectKind
{
    WiiStandard  = 0,
    WiiHomebrew  = 1,
    WiiForwarder = 2,
    GCN          = 3,
}

public sealed class NfsInjectOptions
{
    public bool        Debug               { get; set; }
    public InjectKind  Kind                { get; set; }
    public bool        Passthrough         { get; set; }
    public int         Index               { get; set; }
    public bool        LR                  { get; set; }
    /// <summary>Optional reference size of the original ISO/NKit/WBFS in MiB.</summary>
    public double?     SourceMiB           { get; set; }
    /// <summary>Re-use an existing ISO directly, skip wit copy step.</summary>
    public string?     ExistingGameIsoPath { get; set; }
    /// <summary>Progress callback: (percent 0-100, message).</summary>
    public Action<int, string>? Progress   { get; set; }
}

public sealed class WiiInjectOptions
{
    public bool        Debug               { get; set; }
    public bool        DontTrim            { get; set; }
    public bool        SkipIsoModifications{ get; set; }
    public bool        PatchVideo          { get; set; }
    public bool        ToPal               { get; set; }
    public int         Index               { get; set; }
    public bool        LR                  { get; set; }
    public bool        ForceNkitConvert    { get; set; }
    public bool        Passthrough         { get; set; }
    public Action<string>?       Log               { get; set; }
    /// <summary>Called with the full path to main.dol after extract, before repack.</summary>
    public Func<string, bool>?   PatchDolCallback  { get; set; }
    public Action<int, string>?  Progress          { get; set; }
}

public sealed class GcnInjectOptions
{
    public bool        Debug               { get; set; }
    public bool        DontTrim            { get; set; }
    public string?     Disc2Path           { get; set; }
    public bool        Force43             { get; set; }
    public bool        Passthrough         { get; set; } = true;
    public int         Index               { get; set; }
    public bool        LR                  { get; set; }
    public Action<string>?     Log                 { get; set; }
    public Func<string, bool>? PatchDolCallback    { get; set; }
}
