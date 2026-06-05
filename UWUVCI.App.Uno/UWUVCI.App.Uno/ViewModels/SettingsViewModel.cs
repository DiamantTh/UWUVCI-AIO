using UWUVCI.Config.Loaders;
using UWUVCI.Config.Models;

namespace UWUVCI.App.Uno.ViewModels;

/// <summary>
/// Drives the Settings page.
/// Loads/saves AppSettingsModel via AppSettingsLoader.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly string _settingsPath;
    private AppSettingsModel _model;

    // ---- properties bound to Settings page ---------------------------------

    public string OutPath
    {
        get => _model.OutPath ?? string.Empty;
        set { _model.OutPath = value; OnPropertyChanged(); }
    }

    public string BasePath
    {
        get => _model.BasePath ?? string.Empty;
        set { _model.BasePath = value; OnPropertyChanged(); }
    }

    public string ToolsPath
    {
        get => _model.ToolsPath ?? string.Empty;
        set { _model.ToolsPath = value; OnPropertyChanged(); }
    }

    public string Theme
    {
        get => _model.Theme ?? "Dark";
        set { _model.Theme = value; OnPropertyChanged(); }
    }

    public bool? NativeWindows
    {
        get => _model.NativeWindows;
        set { _model.NativeWindows = value; OnPropertyChanged(); }
    }

    public bool IsFirstLaunch => _model.IsFirstLaunch;

    // ---- commands / actions ------------------------------------------------

    public SettingsViewModel(string settingsPath)
    {
        _settingsPath = settingsPath;
        _model = File.Exists(settingsPath)
            ? AppSettingsLoader.LoadFromFile(settingsPath)
            : new AppSettingsModel();
    }

    /// <summary>Persist the current model to disk.</summary>
    public void Save() => AppSettingsLoader.SaveToFile(_model, _settingsPath);

    /// <summary>Reload from disk (discards unsaved changes).</summary>
    public void Reload()
    {
        _model = File.Exists(_settingsPath)
            ? AppSettingsLoader.LoadFromFile(_settingsPath)
            : new AppSettingsModel();
        OnPropertyChanged(string.Empty); // refresh all bindings
    }
}
