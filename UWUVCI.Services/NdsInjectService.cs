using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>Options for an NDS injection.</summary>
public sealed class NdsInjectOptions
{
    /// <summary>Apply the DSLayout screen patch (adds Phantom Hourglass-style layout overlays).</summary>
    public bool DSLayout       { get; set; }
    /// <summary>Use "Phantom Hourglass" screen layout variant (only relevant when DSLayout = true).</summary>
    public bool STLayout       { get; set; }
    /// <summary>Override the 3D renderer scale factor (1 = native, 2 = doubled). Default 1.</summary>
    public int  RendererScale  { get; set; } = 1;
    /// <summary>Display brightness level (0-100, default 80).</summary>
    public int  Brightness     { get; set; } = 80;
    /// <summary>Pixel art upscaler algorithm index (0 = none).</summary>
    public int  PixelArtUpscaler { get; set; }
    public bool Debug          { get; set; }
}

/// <summary>
/// Injects an NDS ROM into a Wii U Virtual Console base.
/// The base ships the NDS ROM as a named entry inside content/0010/rom.zip;
/// this service replaces that entry and optionally applies layout + config patches.
/// </summary>
public static class NdsInjectService
{
    private const string RomZipRelative = "content/0010/rom.zip";

    public static async Task InjectAsync(
        string        toolsPath,
        string        tempPath,
        string        baseRomPath,
        string        romPath,
        NdsInjectOptions opt,
        IToolRunner   runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opt);

        Directory.CreateDirectory(tempPath);

        var romZip = Path.Combine(baseRomPath, RomZipRelative);
        if (!File.Exists(romZip))
            throw new FileNotFoundException("NDS base ROM archive not found.", romZip);

        // 1) Find the NDS ROM entry name inside the zip (it contains "WUP")
        var entryName = GetRomEntryName(romZip)
            ?? throw new InvalidOperationException($"No WUP-prefixed entry found in {romZip}");

        // 2) Extract ROM entry to tempPath so we can re-zip later
        var extractedRom = Path.Combine(tempPath, entryName);
        if (File.Exists(extractedRom)) File.Delete(extractedRom);

        // 3) Delete old zip and create new one with the injected ROM
        File.Delete(romZip);

        // Copy inject ROM to tempPath/<entryName>
        File.Copy(romPath, extractedRom, overwrite: true);

        // 4) Optional: DSLayout screen patch
        if (opt.DSLayout)
        {
            await ApplyDSLayoutAsync(toolsPath, tempPath, baseRomPath,
                opt.STLayout, cancellationToken).ConfigureAwait(false);
        }

        // 5) Optional: configuration_cafe.json patch
        bool needsCafeConfig = opt.RendererScale != 1
                               || opt.Brightness != 80
                               || opt.PixelArtUpscaler != 0;
        if (needsCafeConfig)
        {
            var cfgPath = Path.Combine(baseRomPath, "content", "0010", "configuration_cafe.json");
            if (File.Exists(cfgPath))
                await UpdateConfigurationCafeJsonAsync(cfgPath, opt, cancellationToken).ConfigureAwait(false);
        }

        // 6) Re-compress ROM into rom.zip
        RecompressRom(romZip, extractedRom, Path.GetFileName(extractedRom));

        // 7) Cleanup temp extracted ROM
        try { if (File.Exists(extractedRom)) File.Delete(extractedRom); } catch { /* best-effort */ }
    }

    // ---- helpers -----------------------------------------------------------

    internal static string? GetRomEntryName(string romZip)
    {
        using var archive = ZipFile.OpenRead(romZip);
        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Contains("WUP", StringComparison.OrdinalIgnoreCase))
                return entry.Name;
        }
        return null;
    }

    private static async Task ApplyDSLayoutAsync(
        string toolsPath, string tempPath, string baseRomPath,
        bool stLayout, CancellationToken ct)
    {
        var layoutZip  = Path.Combine(toolsPath, "DSLayoutScreens.zip");
        if (!File.Exists(layoutZip)) return;

        var layoutDir = Path.Combine(tempPath, "DSLayoutScreens");
        if (Directory.Exists(layoutDir))
            Directory.Delete(layoutDir, recursive: true);

        ZipFile.ExtractToDirectory(layoutZip, layoutDir);

        var variant = stLayout ? "Phatnom Hourglass" : "All";
        var srcDir  = Path.Combine(layoutDir, variant);
        if (!Directory.Exists(srcDir)) return;

        // Copy all extracted files into baseRomPath (overwrite)
        foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(srcDir, file);
            var dest     = Path.Combine(baseRomPath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        // Cleanup layout temp
        try { Directory.Delete(layoutDir, recursive: true); } catch { /* best-effort */ }
    }

    private static async Task UpdateConfigurationCafeJsonAsync(
        string cfgPath, NdsInjectOptions opt, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(cfgPath, ct).ConfigureAwait(false);
        var root = JsonNode.Parse(json)!;

        var cfg = root["configuration"] ?? (root["configuration"] = new JsonObject());

        // 3D renderer scale
        var rendering = cfg["3DRendering"] ?? (cfg["3DRendering"] = new JsonObject());
        rendering["RenderScale"] = opt.RendererScale;

        // Display settings
        var display = cfg["Display"] ?? (cfg["Display"] = new JsonObject());
        display["Brightness"]      = opt.Brightness;
        display["PixelArtUpscaler"] = opt.PixelArtUpscaler;

        await File.WriteAllTextAsync(cfgPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            ct).ConfigureAwait(false);
    }

    private static void RecompressRom(string romZip, string sourcePath, string entryFileName)
    {
        using var archive = ZipFile.Open(romZip, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(sourcePath, entryFileName);
    }
}
