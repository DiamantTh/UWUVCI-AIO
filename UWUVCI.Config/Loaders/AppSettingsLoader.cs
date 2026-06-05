using Tomlyn;
using Tomlyn.Model;
using UWUVCI.Config.Models;

namespace UWUVCI.Config.Loaders;

/// <summary>
/// Loads and saves <see cref="AppSettingsModel"/> as TOML.
/// CKey is stored as-is; callers must ensure it is never written to logs.
/// </summary>
public static class AppSettingsLoader
{
    public static AppSettingsModel LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return new AppSettingsModel();

        return LoadFromString(File.ReadAllText(path));
    }

    public static AppSettingsModel LoadFromString(string toml)
    {
        if (string.IsNullOrWhiteSpace(toml))
            return new AppSettingsModel();

        var doc = TomlSerializer.Deserialize<TomlTable>(toml) ?? new TomlTable();
        var m   = new AppSettingsModel();

        m.PathsSet            = GetBool(doc, "PathsSet",            m.PathsSet);
        m.BasePath            = GetStr(doc,  "BasePath",            m.BasePath);
        m.OutPath             = GetStr(doc,  "OutPath",             m.OutPath);
        m.ToolsPath           = GetStr(doc,  "ToolsPath",           m.ToolsPath);
        m.TempPath            = GetStr(doc,  "TempPath",            m.TempPath);
        m.CKey                = GetStr(doc,  "CKey",                m.CKey);
        m.SetBaseOnce         = GetBool(doc, "SetBaseOnce",         m.SetBaseOnce);
        m.SetOutOnce          = GetBool(doc, "SetOutOnce",          m.SetOutOnce);
        m.dont                = GetBool(doc, "dont",                m.dont);
        m.ndsw                = GetBool(doc, "ndsw",                m.ndsw);
        m.snesw               = GetBool(doc, "snesw",               m.snesw);
        m.gczw                = GetBool(doc, "gczw",                m.gczw);
        m.NativeWindows       = GetBoolNullable(doc, "NativeWindows", m.NativeWindows);
        m.UnixWaitDelayMs     = GetInt(doc,  "UnixWaitDelayMs",     m.UnixWaitDelayMs);
        m.FileCopyParallelism = GetInt(doc,  "FileCopyParallelism", m.FileCopyParallelism);
        m.Theme               = GetStr(doc,  "Theme",               m.Theme);
        m.Ancast              = GetStr(doc,  "Ancast",              m.Ancast);
        m.IsFirstLaunch       = GetBool(doc, "IsFirstLaunch",       m.IsFirstLaunch);
        m.LastVersionSeen     = GetStr(doc,  "LastVersionSeen",     m.LastVersionSeen);
        m.ForceTutorialOnNextLaunch = GetBool(doc, "ForceTutorialOnNextLaunch", m.ForceTutorialOnNextLaunch);
        m.HasAcknowledgedTutorial   = GetBool(doc, "HasAcknowledgedTutorial",   m.HasAcknowledgedTutorial);

        return m;
    }

    public static string SaveToString(AppSettingsModel m)
    {
        var lines = new List<string>
        {
            $"PathsSet = {ToToml(m.PathsSet)}",
            $"BasePath = {ToToml(m.BasePath)}",
            $"OutPath = {ToToml(m.OutPath)}",
            $"ToolsPath = {ToToml(m.ToolsPath)}",
            $"TempPath = {ToToml(m.TempPath)}",
            $"CKey = {ToToml(m.CKey)}",
            $"SetBaseOnce = {ToToml(m.SetBaseOnce)}",
            $"SetOutOnce = {ToToml(m.SetOutOnce)}",
            $"dont = {ToToml(m.dont)}",
            $"ndsw = {ToToml(m.ndsw)}",
            $"snesw = {ToToml(m.snesw)}",
            $"gczw = {ToToml(m.gczw)}",
            $"NativeWindows = {ToTomlNullable(m.NativeWindows)}",
            $"UnixWaitDelayMs = {m.UnixWaitDelayMs}",
            $"FileCopyParallelism = {m.FileCopyParallelism}",
            $"Theme = {ToToml(m.Theme)}",
            $"Ancast = {ToToml(m.Ancast)}",
            $"IsFirstLaunch = {ToToml(m.IsFirstLaunch)}",
            $"LastVersionSeen = {ToToml(m.LastVersionSeen)}",
            $"ForceTutorialOnNextLaunch = {ToToml(m.ForceTutorialOnNextLaunch)}",
            $"HasAcknowledgedTutorial = {ToToml(m.HasAcknowledgedTutorial)}",
        };
        return string.Join("\n", lines) + "\n";
    }

    public static void SaveToFile(AppSettingsModel m, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, SaveToString(m));
    }

    // --- helpers ---

    private static string GetStr(TomlTable doc, string key, string fallback)
        => doc.TryGetValue(key, out var v) && v is string s ? s : fallback;

    private static bool GetBool(TomlTable doc, string key, bool fallback)
        => doc.TryGetValue(key, out var v) && v is bool b ? b : fallback;

    private static bool? GetBoolNullable(TomlTable doc, string key, bool? fallback)
    {
        if (!doc.TryGetValue(key, out var v)) return fallback;
        if (v is bool b) return b;
        if (v is string s && s.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        return fallback;
    }

    private static int GetInt(TomlTable doc, string key, int fallback)
        => doc.TryGetValue(key, out var v) && v is long l ? (int)l : fallback;

    private static string ToToml(string s)
        => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string ToToml(bool b) => b ? "true" : "false";

    private static string ToTomlNullable(bool? b)
        => b.HasValue ? ToToml(b.Value) : "\"null\"";
}
