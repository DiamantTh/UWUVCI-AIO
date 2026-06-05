using Tomlyn;
using Tomlyn.Model;
using UWUVCI.Config.Models;

namespace UWUVCI.Config.Loaders;

/// <summary>
/// Loads a <see cref="ToolManifestModel"/> from TOML.
/// Expected format:
/// <code>
/// [[tools]]
/// name            = "wit"
/// windows_file    = "wit.exe"
/// linux_file      = "wit-linux"
/// windows_only    = false
/// sha256          = "abc123..."
/// download_url    = "https://..."
/// </code>
/// </summary>
public static class ToolManifestLoader
{
    public static ToolManifestModel LoadFromFile(string path)
        => LoadFromString(File.ReadAllText(path));

    public static ToolManifestModel LoadFromString(string toml)
    {
        if (string.IsNullOrWhiteSpace(toml))
            return new ToolManifestModel();

        var doc = TomlSerializer.Deserialize<TomlTable>(toml) ?? new TomlTable();
        var manifest = new ToolManifestModel();

        if (!doc.TryGetValue("tools", out var rawTools))
            return manifest;

        if (rawTools is not TomlTableArray arr)
            return manifest;

        foreach (var table in arr)
        {
            manifest.Tools.Add(new ToolEntryModel
            {
                Name            = GetStr(table, "name"),
                WindowsFileName = GetStr(table, "windows_file"),
                LinuxFileName   = GetStr(table, "linux_file"),
                WindowsOnly     = GetBool(table, "windows_only"),
                Sha256          = GetStrNullable(table, "sha256"),
                DownloadUrl     = GetStrNullable(table, "download_url"),
            });
        }

        return manifest;
    }

    private static string GetStr(TomlTable t, string k)
        => t.TryGetValue(k, out var v) && v is string s ? s : "";

    private static string? GetStrNullable(TomlTable t, string k)
        => t.TryGetValue(k, out var v) && v is string s ? s : null;

    private static bool GetBool(TomlTable t, string k)
        => t.TryGetValue(k, out var v) && v is bool b && b;
}
