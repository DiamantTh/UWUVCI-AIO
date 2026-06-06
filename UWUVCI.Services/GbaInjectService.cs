using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>Options for a GBA injection.</summary>
public sealed class GbaInjectOptions
{
    /// <summary>Remove the dark-filter overlay (requires MArchiveBatchTool and PSB toolchain).</summary>
    public bool DarkFilter   { get; set; }
    /// <summary>Apply the PokePatch (unofficial savegame patch for GBA Pokémon titles).</summary>
    public bool PokePatch    { get; set; }
    public bool Debug        { get; set; }
}

/// <summary>
/// Injects a GBA ROM into a Wii U Virtual Console base using the psb tool.
/// GB/GBC ROMs are first wrapped via the Goomba loader.
/// An optional dark-filter removal modifies alldata.psb.m via MArchiveBatchTool.
/// </summary>
public static class GbaInjectService
{
    public static async Task InjectAsync(
        string        toolsPath,
        string        tempPath,
        string        baseRomPath,
        string        romPath,
        GbaInjectOptions opt,
        IToolRunner   runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opt);
        ArgumentNullException.ThrowIfNull(runner);

        Directory.CreateDirectory(tempPath);

        string workingRom    = romPath;
        bool   deleteTempRom = false;

        try
        {
            // 1) GB/GBC → GBA via Goomba
            var ext = (Path.GetExtension(romPath) ?? "").ToLowerInvariant();
            if (ext != ".gba")
            {
                workingRom    = await WrapWithGoombaAsync(toolsPath, tempPath, romPath, cancellationToken).ConfigureAwait(false);
                deleteTempRom = true;
            }

            // 2) Optional PokePatch
            if (opt.PokePatch)
            {
                var patched = Path.Combine(tempPath, "rom_pp.gba");
                File.Copy(workingRom, patched, overwrite: true);
                await ApplyPokePatchAsync(toolsPath, patched, runner, cancellationToken).ConfigureAwait(false);
                if (deleteTempRom && workingRom != romPath)
                    try { File.Delete(workingRom); } catch { /* best-effort */ }
                workingRom    = patched;
                deleteTempRom = true;
            }

            // 3) Inject into alldata.psb.m via psb tool
            var alldata = Path.Combine(baseRomPath, "content", "alldata.psb.m");
            var psbResult = await runner.RunAsync(
                "psb",
                $"\"{alldata}\" \"{workingRom}\" \"{alldata}\"",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!psbResult.Success)
                throw new InvalidOperationException(
                    $"psb injection failed (exit {psbResult.ExitCode}): {psbResult.StandardError}");

            // 4) Dark-filter removal (MArchiveBatchTool chain)
            if (opt.DarkFilter)
                await RemoveDarkFilterAsync(toolsPath, baseRomPath, runner, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (deleteTempRom && !string.Equals(workingRom, romPath, StringComparison.OrdinalIgnoreCase))
                try { if (File.Exists(workingRom)) File.Delete(workingRom); } catch { /* best-effort */ }

            // Clean Goomba temp files
            foreach (var f in new[] { "goombamenu.gba", "goombaPadded.gba" }.Select(n => Path.Combine(tempPath, n)))
                try { if (File.Exists(f)) File.Delete(f); } catch { /* best-effort */ }
        }
    }

    // ---- Goomba wrap (GB/GBC → GBA) ----------------------------------------

    private static async Task<string> WrapWithGoombaAsync(
        string toolsPath, string tempPath, string romPath, CancellationToken ct)
    {
        var goombaGba    = Path.Combine(toolsPath, "goomba.gba");
        var menuPath     = Path.Combine(tempPath,  "goombamenu.gba");
        var paddedPath   = Path.Combine(tempPath,  "goombaPadded.gba");
        const long target = 32L * 1024 * 1024; // 32 MiB

        // Concatenate goomba.gba + ROM
        await using (var dst = new FileStream(menuPath, FileMode.Create, FileAccess.Write))
        {
            await using var src1 = new FileStream(goombaGba, FileMode.Open, FileAccess.Read);
            await src1.CopyToAsync(dst, ct).ConfigureAwait(false);
            await using var src2 = new FileStream(romPath, FileMode.Open, FileAccess.Read);
            await src2.CopyToAsync(dst, ct).ConfigureAwait(false);
        }

        // Pad to 32 MiB
        await using (var f = new FileStream(paddedPath, FileMode.Create, FileAccess.Write))
        {
            await using var src = new FileStream(menuPath, FileMode.Open, FileAccess.Read);
            await src.CopyToAsync(f, ct).ConfigureAwait(false);
            if (f.Length < target)
            {
                f.Seek(target - 1, SeekOrigin.Begin);
                f.WriteByte(0);
            }
        }

        return paddedPath;
    }

    // ---- PokePatch (in-place) -----------------------------------------------

    private static async Task ApplyPokePatchAsync(
        string toolsPath, string patchedRom, IToolRunner runner, CancellationToken ct)
    {
        var result = await runner.RunAsync("pokepatch", $"\"{patchedRom}\"", cancellationToken: ct).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"pokepatch failed (exit {result.ExitCode}): {result.StandardError}");
    }

    // ---- Dark-filter removal via MArchiveBatchTool + PSB chain -------------

    private static async Task RemoveDarkFilterAsync(
        string toolsPath, string baseRomPath,
        IToolRunner runner, CancellationToken ct)
    {
        var alldata      = Path.Combine(baseRomPath, "content", "alldata.psb.m");
        var extractedDir = alldata + "_extracted";
        const string key = "MX8wgGEJ2+M47";
        const int    len = 80;

        async Task MArchive(string args)
        {
            var r = await runner.RunAsync("MArchiveBatchTool", args, cancellationToken: ct).ConfigureAwait(false);
            if (!r.Success)
                throw new InvalidOperationException($"MArchiveBatchTool {args} failed: {r.StandardError}");
        }

        // Extract archive
        await MArchive($"archive extract \"{alldata}\" --codec zlib --seed {key} --keyLength {len}").ConfigureAwait(false);

        var titleProfs = Directory.GetFiles(extractedDir, "title_prof.psb.m", SearchOption.AllDirectories);
        if (titleProfs.Length == 0)
        {
            // Non-fatal: base might not have this file
            try { if (Directory.Exists(extractedDir)) Directory.Delete(extractedDir, recursive: true); } catch { /* best-effort */ }
            return;
        }

        var titleprofPsbM = titleProfs[0];
        var configDir     = Path.GetDirectoryName(titleprofPsbM)!;
        var titleprofPsb  = Path.Combine(configDir, "title_prof.psb");
        var titleprofJson = titleprofPsb + ".json";

        await MArchive($"m unpack \"{titleprofPsbM}\" zlib {key} {len}").ConfigureAwait(false);
        await MArchive($"psb deserialize \"{titleprofPsb}\"").ConfigureAwait(false);

        // Patch brightness = 1 via simple string replace (avoids Newtonsoft dependency in Core)
        var json = await File.ReadAllTextAsync(titleprofJson, ct).ConfigureAwait(false);
        json = System.Text.RegularExpressions.Regex.Replace(
            json,
            @"(""brightness""\s*:\s*)\d+",
            "${1}1");
        await File.WriteAllTextAsync(titleprofJson, json, ct).ConfigureAwait(false);

        await MArchive($"psb serialize \"{titleprofJson}\"").ConfigureAwait(false);
        await MArchive($"m pack \"{titleprofPsb}\" zlib {key} {len}").ConfigureAwait(false);

        var builtDir = Path.Combine(baseRomPath, "content", "alldata");
        await MArchive($"archive build --codec zlib --seed {key} --keyLength {len} \"{extractedDir}\" \"{builtDir}\"").ConfigureAwait(false);

        // Cleanup
        try
        {
            if (Directory.Exists(extractedDir)) Directory.Delete(extractedDir, recursive: true);
            var alldataPsb = Path.Combine(baseRomPath, "content", "alldata.psb");
            if (File.Exists(alldataPsb)) File.Delete(alldataPsb);
        }
        catch { /* best-effort */ }
    }
}
