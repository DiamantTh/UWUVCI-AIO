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

            // 3) Inject into alldata.psb.m
            // psb.exe was Windows-only. Native C# implementation requires a
            // PSB.M format reader/writer (MArchive M-encryption + PSB binary
            // parser). Not yet implemented – contribution welcome.
            // Dark-filter removal (MArchiveBatchTool) also blocked on this.
            // See REWRITE_PLAN.md §Phase-18 for the specification needed.
            throw new PlatformNotSupportedException(
                "GBA injection requires a native PSB.M implementation that is " +
                "not yet available. The psb.exe tool was Windows-only; " +
                "see REWRITE_PLAN.md §Phase-18 for the format specification.");
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
    // Ported directly from legacy Injection.cs PokePatch() native implementation.
    // Searches for the byte pattern D0 88 8D 83 42 in the GBA ROM and zeroes
    // the 3-4 bytes that follow it (savegame compatibility fix for Pokémon titles).

    private static Task ApplyPokePatchAsync(
        string toolsPath, string patchedRom, IToolRunner runner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        GbaPokePatch.Apply(patchedRom);
        return Task.CompletedTask;
    }

    // ---- Dark-filter removal via MArchiveBatchTool + PSB chain -------------

    private static Task RemoveDarkFilterAsync(
        string toolsPath, string baseRomPath,
        IToolRunner runner, CancellationToken ct)
    {
        // MArchiveBatchTool.exe was Windows-only.
        // Native C# implementation requires MArchive zlib/stream-cipher archive
        // format + PSB binary format support. Not yet implemented.
        // See REWRITE_PLAN.md §Phase-18 for the format specification.
        throw new PlatformNotSupportedException(
            "GBA dark-filter removal requires a native MArchive/PSB implementation " +
            "that is not yet available. The MArchiveBatchTool.exe was Windows-only; " +
            "see REWRITE_PLAN.md §Phase-18 for the format specification.");
    }
}
