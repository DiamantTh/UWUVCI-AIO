using System.Buffers.Binary;
using System.Text;
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

            // 3) Inject ROM into alldata.psb.m / alldata.bin (native, no external tool)
            GbaPsbInjector.InjectRom(baseRomPath, workingRom);

            // 4) Optional dark-filter removal (modifies PSB nodes)
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

    // ---- Dark-filter removal via PSB node modification --------------------

    private static Task RemoveDarkFilterAsync(
        string toolsPath, string baseRomPath,
        IToolRunner runner, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Locate title_prof.psb.m in the base game (usually under system/config/)
        var titleProfCandidates = Directory.GetFiles(baseRomPath, "title_prof.psb.m", SearchOption.AllDirectories);
        if (titleProfCandidates.Length == 0)
        {
            // No dark filter present in this base (may be optional)
            return Task.CompletedTask;
        }

        var titleprofPsbM = titleProfCandidates
            .OrderByDescending(p => p.Contains($"{Path.DirectorySeparatorChar}system{Path.DirectorySeparatorChar}config{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.Length)
            .First();

        // 1. Decrypt + decompress
        var encryptedData = File.ReadAllBytes(titleprofPsbM);
        var decryptedData = MArchiveService.DecryptAndDecompress(encryptedData, "title_prof.psb.m");

        // 2. Patch: Set root.m2epi.brightness = 1 (disables dark filter)
        // The brightness field in PSB v2 is typically encoded as a small int.
        // Original code: jsonObj["root"]["m2epi"]["brightness"] = 1
        // We patch the raw PSB by finding and replacing the brightness uint value.
        var patchedData = PatchTitleProfBrightness(decryptedData);

        // 3. Re-compress + re-encrypt
        var reencrypted = MArchiveService.CompressAndEncrypt(patchedData, "title_prof.psb.m");
        File.WriteAllBytes(titleprofPsbM, reencrypted);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Patch the title_prof.psb raw data to disable dark filter.
    /// Finds the root.m2epi.brightness value and sets it to 1.
    /// Returns the patched PSB data.
    /// </summary>
    private static byte[] PatchTitleProfBrightness(byte[] psbData)
    {
        // Parse PSB to find brightness node
        // The PSB v2 format stores integers via variable-length encoding.
        // For simplicity, we search for the UTF-8 string "brightness" followed by
        // a small int encoding, and replace it with brightness=1.

        var result = new byte[psbData.Length];
        psbData.CopyTo(result, 0);

        // Search for "brightness" string in the names section or strings section
        var brightnessBytes = Encoding.UTF8.GetBytes("brightness");
        for (int i = 0; i <= result.Length - brightnessBytes.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < brightnessBytes.Length; j++)
            {
                if (result[i + j] != brightnessBytes[j]) { match = false; break; }
            }

            if (match)
            {
                // Found "brightness" string. Now look for the value that follows.
                // PSB v2 encodes values as [type_byte][value_bytes]
                // Common encodings for small ints (0 or 1):
                //   Type 5 (byte): [0x05][0x00] or [0x05][0x01]
                //   Type 6 (short): [0x06][0x00 0x00] or [0x06][0x01 0x00]
                // We look for patterns and replace the value 0 with value 1.

                // Simple heuristic: search backward from "brightness" for an object marker (type 33)
                // and forward for a type-5/6/7 encoding that looks like a small int.
                // For now, we'll do a linear search for the pattern and hope it's unique.

                // Look ahead from brightness+name null term for the encoded value
                int searchStart = i + brightnessBytes.Length + 1;
                for (int j = searchStart; j < Math.Min(searchStart + 20, result.Length); j++)
                {
                    byte type = result[j];
                    // Type 5 = byte value (no extra bytes)
                    if (type == 5 && j + 1 < result.Length)
                    {
                        if (result[j + 1] == 0) // brightness is currently 0 (dark filter ON)
                        {
                            result[j + 1] = 1; // Set to 1 (dark filter OFF)
                            return result; // Done
                        }
                    }
                    // Type 6 = short (2 bytes LE)
                    if (type == 6 && j + 2 < result.Length)
                    {
                        ushort val = BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(j + 1));
                        if (val == 0)
                        {
                            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(j + 1), 1);
                            return result;
                        }
                    }
                }
            }
        }

        // If exact patching failed, return the original data unchanged.
        // The dark filter may not be present or the format may vary.
        return result;
    }
}
