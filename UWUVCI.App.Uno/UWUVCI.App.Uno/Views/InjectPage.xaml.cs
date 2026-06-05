using Microsoft.UI.Xaml.Data;
using UWUVCI.App.Uno.ViewModels;

namespace UWUVCI.App.Uno.Views;

/// <summary>Converts bool → Visibility; set Invert=true for the opposite sense.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool visible = value is bool b && b;
        if (Invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public sealed partial class InjectPage : Page
{
    public InjectViewModel ViewModel { get; } = App.Inject;

    public InjectPage() => this.InitializeComponent();

    private async void BrowseRom_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            if (file is not null) ViewModel.RomPath = file.Path;
        }
        catch { /* picker unavailable on this platform */ }
    }

    private async void Inject_Click(object sender, RoutedEventArgs e)
        => await ViewModel.InjectAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => ViewModel.Cancel();
}
