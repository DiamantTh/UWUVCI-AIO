using UWUVCI.App.Uno.ViewModels;

namespace UWUVCI.App.Uno.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = App.Settings;

    public SettingsPage()
    {
        this.InitializeComponent();
        SyncPlatformModeBox();
    }

    // ---- platform mode combo -------------------------------------------

    private void SyncPlatformModeBox()
    {
        PlatformModeBox.SelectedIndex = ViewModel.NativeWindows switch
        {
            true  => 1,
            false => 2,
            null  => 0,
        };
    }

    private void PlatformMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.NativeWindows = PlatformModeBox.SelectedIndex switch
        {
            1 => true,
            2 => false,
            _ => null,
        };
    }

    // ---- browse buttons ------------------------------------------------

    private async void BrowseOutPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null) ViewModel.OutPath = path;
    }

    private async void BrowseBasePath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null) ViewModel.BasePath = path;
    }

    private async void BrowseToolsPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null) ViewModel.ToolsPath = path;
    }

    private static async Task<string?> PickFolderAsync()
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.FileTypeFilter.Add("*");
            // WinUI3 / Uno desktop: initialise with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch
        {
            // Picker not available on all platforms
            return null;
        }
    }

    // ---- save ----------------------------------------------------------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Save();  // fires SettingsSaved → App.OnSettingsSaved → ApplyTheme
        SaveStatus.Text       = "Settings saved.";
        SaveStatus.Visibility = Visibility.Visible;
    }
}
