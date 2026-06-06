using UWUVCI.App.Uno.ViewModels;

namespace UWUVCI.App.Uno.Views;

public sealed partial class ToolsPage : Page
{
    public ToolsViewModel ViewModel { get; } = App.Tools;

    private CancellationTokenSource? _cts;

    public ToolsPage()
    {
        this.InitializeComponent();
        ViewModel.Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
        => ViewModel.Refresh();

    private async void DownloadAll_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        try   { await ViewModel.DownloadAllMissingAsync(_cts.Token); }
        catch { /* cancelled or handled inside VM */ }
    }

    private async void DownloadTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string toolName) return;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        try   { await ViewModel.DownloadToolAsync(toolName, _cts.Token); }
        catch { /* handled inside VM */ }
    }
}
