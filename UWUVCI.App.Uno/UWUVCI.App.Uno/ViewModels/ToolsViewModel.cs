using UWUVCI.Config.Models;
using UWUVCI.Core.Tooling;
using UWUVCI.Services;
using UWUVCI.Tooling;

namespace UWUVCI.App.Uno.ViewModels;

/// <summary>
/// Represents one row in the Tools page list.
/// </summary>
public sealed class ToolRowViewModel : ObservableObject
{
    private bool   _isPresent;
    private bool   _isDownloading;
    private int    _downloadProgress;
    private string _statusText = string.Empty;
    private string _errorText  = string.Empty;

    public string  Name             { get; init; } = string.Empty;
    public bool    HasDownloadUrl   { get; init; }

    public bool IsPresent
    {
        get => _isPresent;
        set { SetField(ref _isPresent, value); OnPropertyChanged(nameof(StatusIcon)); }
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set { SetField(ref _isDownloading, value); OnPropertyChanged(nameof(CanDownload)); }
    }

    public int DownloadProgress
    {
        get => _downloadProgress;
        set => SetField(ref _downloadProgress, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        set { SetField(ref _errorText, value); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError   => !string.IsNullOrWhiteSpace(_errorText);
    public bool CanDownload => HasDownloadUrl && !IsDownloading;

    /// <summary>Simple text badge: ✔ / ✖ / ↓</summary>
    public string StatusIcon => IsPresent ? "✔" : (IsDownloading ? "↓" : "✖");
}

/// <summary>
/// Drives the Tools page. Displays per-tool present/missing status and
/// supports downloading tools that have a manifest URL.
/// </summary>
public sealed class ToolsViewModel : ObservableObject
{
    private ToolManifestModel? _manifest;
    private string             _toolsDir = string.Empty;
    private PlatformInfo?      _platform;
    private IToolResolver?     _resolver;

    // ---- public state -------------------------------------------------------

    public System.Collections.ObjectModel.ObservableCollection<ToolRowViewModel> Rows { get; } = [];

    private bool   _isBusy;
    private string _statusMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool HasTools => Rows.Count > 0;

    // ---- initialisation (called from App.xaml.cs) ---------------------------

    public void Initialise(ToolManifestModel manifest, string toolsDir, PlatformInfo platform)
    {
        _manifest = manifest;
        _toolsDir = toolsDir;
        _platform = platform;
        _resolver = new ManifestToolResolver(manifest, toolsDir, platform);

        Refresh();
    }

    // ---- commands -----------------------------------------------------------

    /// <summary>Re-reads disk state for all tools.</summary>
    public void Refresh()
    {
        if (_manifest is null || _resolver is null) return;

        var statuses = ToolDownloadService.GetStatuses(_manifest, _resolver);

        // Update existing rows or rebuild
        if (Rows.Count == statuses.Count)
        {
            for (int i = 0; i < statuses.Count; i++)
            {
                var row = Rows[i];
                var s   = statuses[i];
                row.IsPresent  = s.IsPresent;
                row.StatusText = s.IsPresent ? "Present" : "Missing";
                if (!row.IsDownloading) row.ErrorText = string.Empty;
            }
        }
        else
        {
            Rows.Clear();
            foreach (var s in statuses)
            {
                Rows.Add(new ToolRowViewModel
                {
                    Name           = s.Name,
                    HasDownloadUrl = s.HasUrl,
                    IsPresent      = s.IsPresent,
                    StatusText     = s.IsPresent ? "Present" : "Missing",
                });
            }
        }

        OnPropertyChanged(nameof(HasTools));
        StatusMessage = $"Tools directory: {_toolsDir}";
    }

    /// <summary>Download a single tool by name.</summary>
    public async Task DownloadToolAsync(string toolName, CancellationToken ct = default)
    {
        if (_manifest is null || _platform is null) return;

        var row = Rows.FirstOrDefault(r => r.Name == toolName);
        if (row is null) return;

        row.IsDownloading     = true;
        row.ErrorText         = string.Empty;
        row.DownloadProgress  = 0;
        row.StatusText        = "Downloading…";

        try
        {
            await ToolDownloadService.DownloadToolAsync(
                toolName,
                _manifest,
                _toolsDir,
                isLinux: _platform.IsLinux,
                progress: (_, received, total) =>
                {
                    if (total.HasValue && total.Value > 0)
                        row.DownloadProgress = (int)(received * 100 / total.Value);
                },
                ct: ct).ConfigureAwait(false);

            row.IsPresent    = true;
            row.StatusText   = "Present";
        }
        catch (OperationCanceledException)
        {
            row.StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            row.ErrorText  = ex.Message;
            row.StatusText = "Download failed.";
        }
        finally
        {
            row.IsDownloading    = false;
            row.DownloadProgress = 0;
        }
    }

    /// <summary>Download all missing tools that have a URL.</summary>
    public async Task DownloadAllMissingAsync(CancellationToken ct = default)
    {
        var missing = Rows.Where(r => !r.IsPresent && r.HasDownloadUrl).ToList();
        IsBusy = true;
        try
        {
            foreach (var row in missing)
            {
                if (ct.IsCancellationRequested) break;
                await DownloadToolAsync(row.Name, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
