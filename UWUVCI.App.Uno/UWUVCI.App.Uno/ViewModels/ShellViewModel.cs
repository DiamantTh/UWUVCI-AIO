using System.Collections.ObjectModel;
using UWUVCI.App.Uno.Views;

namespace UWUVCI.App.Uno.ViewModels;

/// <summary>
/// Owns the navigation state: current page and the sidebar item list.
/// Lives in App.xaml.cs (singleton) so all pages can resolve it.
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private NavigationItem? _selected;

    public ObservableCollection<NavigationItem> Items { get; } =
    [
        new NavigationItem { Label = "Inject",    Glyph = "\uE896", PageType = typeof(InjectPage) },
        new NavigationItem { Label = "Settings",  Glyph = "\uE713", PageType = typeof(SettingsPage) },
        new NavigationItem { Label = "Tools",     Glyph = "\uE756", PageType = typeof(ToolsPage) },
        new NavigationItem { Label = "About",     Glyph = "\uE946", PageType = typeof(AboutPage) },
    ];

    public NavigationItem? Selected
    {
        get => _selected;
        set
        {
            if (_selected != null) _selected.IsSelected = false;
            SetField(ref _selected, value);
            if (_selected != null) _selected.IsSelected = true;
        }
    }

    public ShellViewModel() => Selected = Items[0];
}
