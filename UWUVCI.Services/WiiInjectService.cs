using System.Text;
using System.Xml;
using UWUVCI.Core.Pipeline;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Implements the standard Wii injection pipeline:
/// <list type="number">
///   <item>Pre-converts the ROM to a plain ISO if needed (NKit/WBFS → wit copy).</item>
///   <item>Patches reserved_flag2 in meta.xml from the game-ID bytes.</item>
///   <item>Extracts the ISO via <c>wit extract</c> into a TEMP directory.</item>
///   <item>Optionally patches main.dol (caller-supplied callback).</item>
///   <item>Optionally applies a video-mode patch via wii-vmc.</item>
///   <item>Promotes TEMP → TempBase.</item>
///   <item>Calls <see cref="WitNfsService"/> to produce the final NFS payload.</item>
/// </list>
/// Also provides helpers for Homebrew and Forwarder flows.
/// </summary>
public static class WiiInjectService
{
    // ---- Standard Wii --------------------------------------------------

    public static async Task InjectStandardAsync(
        string        toolsPath,
        string        tempPath,
        string        baseRomPath,
        string        romPath,
        WiiInjectOptions opt,
        IPlatformInfo platform,
        IToolRunner   runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opt);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(platform);

        Directory.CreateDirectory(tempPath);
        await RunStandardPipelineAsync(toolsPath, tempPath, baseRomPath, romPath, opt, platform, runner, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task RunStandardPipelineAsync(
        string toolsPath, string tempPath, string baseRomPath, string romPath,
        WiiInjectOptions opt, IPlatformInfo platform, IToolRunner runner,
        CancellationToken ct)
    {
        var (preIso, isSourceDirect, sourceMiB) = await PreparePreIsoAsync(toolsPath, tempPath, romPath, opt, platform, runner, ct).ConfigureAwait(false);

        UpdateMetaReservedFlag(baseRomPath, ToHostPath(platform, preIso));

        var tempDir = Path.Combine(tempPath, "TEMP");
        await WitExtractToTempAsync(toolsPath, preIso, tempDir, opt, runner, ct).ConfigureAwait(false);

        ApplyOptionalDolPatch(tempDir, opt);

        if (opt.PatchVideo)
            await ApplyVideoPatchAsync(toolsPath, tempDir, opt, runner, ct).ConfigureAwait(false);

        PromoteTempDirToTempBase(tempDir, tempPath);

        if (!isSourceDirect)
            TryDelete(preIso);

        var nfsOptions = new NfsInjectOptions
        {
            Debug       = opt.Debug,
            Kind        = InjectKind.WiiStandard,
            Passthrough = opt.Passthrough,
            Index       = opt.Index,
            LR          = opt.LR,
            SourceMiB   = sourceMiB,
            Progress    = opt.Progress,
        };

        await WitNfsService.BuildIsoExtractTicketsAndInjectAsync(
            toolsPath, tempPath, baseRomPath, nfsOptions, platform, runner, ct).ConfigureAwait(false);
    }

    // ---- Steps ---------------------------------------------------------

    internal static async Task<(string preIso, bool isSourceDirect, double? sourceMiB)> PreparePreIsoAsync(
        string toolsPath, string tempPath, string romPath,
        WiiInjectOptions opt, IPlatformInfo platform, IToolRunner runner, CancellationToken ct)
    {
        var ext = (Path.GetExtension(romPath) ?? "").ToLowerInvariant();
        bool isNkit = romPath.Contains("nkit", StringComparison.OrdinalIgnoreCase);

        if (ext == ".iso" && !isNkit && !opt.ForceNkitConvert)
        {
            // Re-use source ISO directly (no copy needed).
            return (romPath, true, null);
        }

        var preIso  = Path.Combine(tempPath, "pre.iso");
        var witArgs = $"copy --source \"{romPath}\" --dest \"{preIso}\" -I";
        var result  = await runner.RunAsync("wit", witArgs, cancellationToken: ct).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException($"wit copy (pre-ISO) failed (exit {result.ExitCode}): {result.StandardError}");

        var preIsoHost = ToHostPath(platform, preIso);
        if (!WineFence.WaitForVisibility(preIsoHost))
            throw new IOException($"pre.iso not visible after wit copy: {preIsoHost}");
        WineFence.WaitForStableSize(preIsoHost);

        var miB = (double?)new FileInfo(preIsoHost).Length / (1024.0 * 1024.0);
        return (preIso, false, miB);
    }

    internal static void UpdateMetaReservedFlag(string baseRomPath, string isoHostPath)
    {
        if (!File.Exists(isoHostPath))
            throw new FileNotFoundException("ISO missing before meta.xml edit.", isoHostPath);

        var buf = new byte[4];
        using (var fs = new FileStream(isoHostPath, FileMode.Open, FileAccess.Read))
            _ = fs.Read(buf, 0, 4);

        var hex     = Convert.ToHexString(buf).ToLowerInvariant();
        var metaXml = Path.Combine(baseRomPath, "meta", "meta.xml");

        var doc = new XmlDocument();
        doc.Load(metaXml);
        var node = doc.SelectSingleNode("menu/reserved_flag2")
                   ?? throw new InvalidDataException("meta.xml is missing <reserved_flag2> node.");
        node.InnerText = hex;
        doc.Save(metaXml);
    }

    internal static async Task WitExtractToTempAsync(
        string toolsPath, string preIso, string tempDir,
        WiiInjectOptions opt, IToolRunner runner, CancellationToken ct)
    {
        var psel   = opt.DontTrim ? "raw" : "whole";
        var args   = $"extract \"{preIso}\" --DEST \"{tempDir}\" --psel {psel} -vv1";
        var result = await runner.RunAsync("wit", args, cancellationToken: ct).ConfigureAwait(false);

        if (!result.Success)
            throw new InvalidOperationException($"wit extract failed (exit {result.ExitCode}): {result.StandardError}");

        if (!WineFence.WaitForVisibility(tempDir, requireNonEmpty: false, isDirectory: true))
            throw new IOException($"wit extract completed but TEMP directory not visible: {tempDir}");

        // Drop UPDATE partition – occasionally leaves behind problematic files.
        var updateDir = Path.Combine(tempDir, "UPDATE");
        try { if (Directory.Exists(updateDir)) Directory.Delete(updateDir, recursive: true); } catch { /* best-effort */ }
    }

    internal static void ApplyOptionalDolPatch(string tempDir, WiiInjectOptions opt)
    {
        if (opt.PatchDolCallback is null) return;
        var dol = Directory.GetFiles(tempDir, "main.dol", SearchOption.AllDirectories).FirstOrDefault();
        if (dol is null) return;
        try { opt.PatchDolCallback(dol); } catch { /* non-fatal */ }
    }

    internal static async Task ApplyVideoPatchAsync(
        string toolsPath, string tempDir, WiiInjectOptions opt,
        IToolRunner runner, CancellationToken ct)
    {
        // wii-vmc requires interactive stdin; we pre-answer 'a' (apply), then region.
        // Run in the sys directory so it finds main.dol by name.
        var sysDir = Path.Combine(tempDir, "DATA", "sys");
        Directory.CreateDirectory(sysDir);

        // wii-vmc sends prompts over stdout and reads from stdin.
        // We handle this via the process' standard streams directly since
        // IToolRunner.RunAsync doesn't support interactive stdin.
        // Use a fire-and-forget Process here (WPF-free path).
        var vmcPath = Path.Combine(toolsPath, "wii-vmc.exe");
        if (!File.Exists(vmcPath))
            throw new FileNotFoundException("wii-vmc.exe not found.", vmcPath);

        await Task.Run(() =>
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName               = vmcPath;
            proc.StartInfo.Arguments              = "main.dol";
            proc.StartInfo.WorkingDirectory       = sysDir;
            proc.StartInfo.UseShellExecute        = false;
            proc.StartInfo.CreateNoWindow         = true;
            proc.StartInfo.RedirectStandardInput  = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();
            Thread.Sleep(1000);
            proc.StandardInput.WriteLine("a");
            Thread.Sleep(2000);
            proc.StandardInput.WriteLine(opt.ToPal ? "1" : "2");
            Thread.Sleep(2000);
            proc.StandardInput.WriteLine();
            proc.WaitForExit();
        }, ct).ConfigureAwait(false);
    }

    // ---- Homebrew / Forwarder helpers (shared) --------------------------

    internal static string PrepareTempBase(string toolsPath, string tempPath)
    {
        var tempBase = Path.Combine(tempPath, "TempBase");
        if (Directory.Exists(tempBase))
            Directory.Delete(tempBase, recursive: true);
        Directory.CreateDirectory(tempBase);
        var baseDir = BaseExtractor.GetOrExtractBase(toolsPath, "BASE.zip");
        IOHelpers.MoveOrCopyDirectory(baseDir, tempBase);
        return tempBase;
    }

    internal static void CopyDolToBase(string tempBase, string dolSource) =>
        File.Copy(dolSource, Path.Combine(tempBase, "sys", "main.dol"), overwrite: true);

    internal static void SetupForwarderTitle(string tempBase, string wadPath)
    {
        var buf = new byte[4];
        using (var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read))
        {
            fs.Seek(0xC20, SeekOrigin.Begin);
            _ = fs.Read(buf, 0, 4);
        }
        var titleId = Encoding.ASCII.GetString(buf);
        File.WriteAllLines(Path.Combine(tempBase, "files", "title.txt"), [titleId]);
    }

    internal static void CopyForwarderDol(string toolsPath, string tempBase) =>
        File.Copy(Path.Combine(toolsPath, "forwarder.dol"), Path.Combine(tempBase, "sys", "main.dol"), overwrite: true);

    // ---- Private -------------------------------------------------------

    private static void PromoteTempDirToTempBase(string tempDir, string tempPath)
    {
        if (!Directory.Exists(tempDir)) return;
        var tempBase = Path.Combine(tempPath, "TempBase");
        IOHelpers.MoveOrCopyDirectory(tempDir, tempBase);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
    }

    private static string ToHostPath(IPlatformInfo platform, string path) =>
        platform.IsWineLike ? platform.ToHostPath(path) : path;
}
