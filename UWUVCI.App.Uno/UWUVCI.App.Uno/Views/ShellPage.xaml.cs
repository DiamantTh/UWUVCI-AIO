using Microsoft.UI.Xaml.Data;
using UWUVCI.App.Uno.ViewModels;

namespace UWUVCI.App.Uno.Views;

/// <summary>Returns a highlighted background brush when a nav item is selected.</summary>
public sealed class NavSelectedBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool selected = value is bool b && b;
        return selected
            ? Application.Current.Resources["AppAccentBrush"] as SolidColorBrush
                ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Shell;
        this.InitializeComponent();
        ContentFrame.Navigate(ViewModel.Selected!.PageType);
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NavigationItem item)
        {
            ViewModel.Selected = item;
            ContentFrame.Navigate(item.PageType);
        }
    }
}
