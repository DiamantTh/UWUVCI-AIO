using System.Xml;
using UWUVCI.Core.Pipeline;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Core Wii + GCN injection pipeline:
/// <list type="number">
///   <item>Copies game image into content/game.iso via <c>wit copy</c> (or uses a pre-built ISO).</item>
///   <item>Extracts TIK + TMD via <see cref="WitTicketExtractionService"/>.</item>
///   <item>Patches the GCN reserved_flag2 in meta.xml if required.</item>
///   <item>Moves TIK/TMD into the base title <c>code/</c> directory.</item>
///   <item>Runs <c>nfs2iso2nfs -enc</c> to produce the final NFS payload.</item>
/// </list>
/// </summary>
public static class WitNfsService
{
    /// <summary>
    /// Runs the full wit→ticket→nfs2iso2nfs pipeline for a single Wii/GCN inject.
    /// </summary>
    public static async Task BuildIsoExtractTicketsAndInjectAsync(
        string          toolsPath,
        string          tempPath,
        string          baseRomPath,
        NfsInjectOptions options,
        IPlatformInfo    platform,
        IToolRunner      runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(platform);

        bool debug = options.Debug;

        var tempBaseDir  = Path.Combine(tempPath, "TempBase");
        var tikTmdDir    = Path.Combine(tempPath, "TIKTMD");
        var contentDir   = Path.Combine(baseRomPath, "content");
        Directory.CreateDirectory(contentDir);
        Directory.CreateDirectory(Path.Combine(baseRomPath, "code"));
        Directory.CreateDirectory(Path.Combine(baseRomPath, "meta"));

        // ---- 1) game.iso -------------------------------------------------------
        var gameIso = Path.Combine(contentDir, "game.iso");
        options.Progress?.Invoke(10, "Copying game image…");

        if (!string.IsNullOrWhiteSpace(options.ExistingGameIsoPath))
        {
            var src = ToHostPath(platform, options.ExistingGameIsoPath);
            if (!File.Exists(src))
                throw new FileNotFoundException("Existing game ISO not found.", src);
            File.Copy(src, gameIso, overwrite: true);
        }
        else
        {
            var alignArg  = BuildWitAlignArg(baseRomPath, tempPath);
            var witArgs   = $"copy \"{tempBaseDir}\" --DEST \"{gameIso}\" -ovv --links --iso{(string.IsNullOrWhiteSpace(alignArg) ? "" : " " + alignArg)}";
            var witResult = await runner.RunAsync("wit", witArgs, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!witResult.Success)
                throw new InvalidOperationException($"wit copy failed (exit {witResult.ExitCode}): {witResult.StandardError}");
        }

        // Wine fence
        var gameIsoHost = ToHostPath(platform, gameIso);
        if (!WineFence.WaitForVisibility(gameIsoHost))
            throw new IOException($"game.iso not visible to host after wit copy: {gameIsoHost}");
        WineFence.WaitForStableSize(gameIsoHost);

        // ---- 2) Extract TIK/TMD -----------------------------------------------
        options.Progress?.Invoke(30, "Extracting tickets…");
        await WitTicketExtractionService.ExtractTicketsAsync(
            toolsPath, gameIso, tikTmdDir, platform, runner, cancellationToken).ConfigureAwait(false);

        // ---- 3) GCN reserved_flag2 patch -------------------------------------
        if (options.Kind == InjectKind.GCN)
            PatchGcnMetaXml(gameIsoHost, Path.Combine(baseRomPath, "meta", "meta.xml"));

        // ---- 4) Clean TempBase -----------------------------------------------
        try { if (Directory.Exists(tempBaseDir)) Directory.Delete(tempBaseDir, recursive: true); } catch { /* best-effort */ }

        // ---- 5) Promote TIK/TMD into code/ -----------------------------------
        options.Progress?.Invoke(50, "Replacing TIK and TMD…");
        var codeDir = Path.Combine(baseRomPath, "code");
        foreach (var f in Directory.GetFiles(codeDir, "rvlt.*"))
            try { File.Delete(f); } catch { /* best-effort */ }

        File.Copy(Path.Combine(tikTmdDir, "tmd.bin"),    Path.Combine(codeDir, "rvlt.tmd"), overwrite: true);
        File.Copy(Path.Combine(tikTmdDir, "ticket.bin"), Path.Combine(codeDir, "rvlt.tik"), overwrite: true);
        try { if (Directory.Exists(tikTmdDir)) Directory.Delete(tikTmdDir, recursive: true); } catch { /* best-effort */ }

        // ---- 6) Remove stale NFS files ---------------------------------------
        foreach (var nfs in Directory.GetFiles(contentDir, "*.nfs"))
            try { File.Delete(nfs); } catch { /* best-effort */ }

        // ---- 7) NFS encryption (native AES-128-CBC replacement for nfs2iso2nfs) --------
        options.Progress?.Invoke(60, "Injecting ROM…");
        var keyPath = NfsConverter.FindKeyFile(baseRomPath);
        await Task.Run(() =>
            NfsConverter.EncryptIsoToNfs(gameIso, contentDir, keyPath),
            cancellationToken).ConfigureAwait(false);

        // Remove working ISO
        try { if (File.Exists(gameIso)) File.Delete(gameIso); } catch { /* best-effort */ }

        options.Progress?.Invoke(80, "Injection complete");
    }

    // ---- helpers -----------------------------------------------------------

    private static void PatchGcnMetaXml(string isoHostPath, string metaXmlPath)
    {
        if (!File.Exists(isoHostPath) || !File.Exists(metaXmlPath)) return;

        var buf = new byte[4];
        using (var fs = new FileStream(isoHostPath, FileMode.Open, FileAccess.Read))
            _ = fs.Read(buf, 0, 4);

        var hex    = Convert.ToHexString(buf).ToLowerInvariant();

        var doc = new XmlDocument();
        doc.Load(metaXmlPath);
        var node = doc.SelectSingleNode("menu/reserved_flag2");
        if (node is null) return;
        node.InnerText = hex;
        doc.Save(metaXmlPath);
    }

    private static string BuildWitAlignArg(string baseRomPath, string tempPath)
    {
        foreach (var candidate in new[] { Path.Combine(baseRomPath, "align-files.txt"), Path.Combine(tempPath, "align-files.txt") })
            if (File.Exists(candidate))
                return $"--align-files \"{candidate}\"";
        return string.Empty;
    }

    private static string ToHostPath(IPlatformInfo platform, string path) =>
        platform.IsWineLike ? platform.ToHostPath(path) : path;
}
