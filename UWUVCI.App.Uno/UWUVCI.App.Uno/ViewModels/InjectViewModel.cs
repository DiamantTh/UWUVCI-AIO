using UWUVCI.Core.Models;
using UWUVCI.Services;

namespace UWUVCI.App.Uno.ViewModels;

/// <summary>
/// Drives the Inject page.
/// Holds user selections and exposes an async inject command.
/// </summary>
public sealed class InjectViewModel : ObservableObject
{
    // ---- console selection ------------------------------------------------

    private GameConsole _selectedConsole = GameConsole.NES;

    public GameConsole SelectedConsole
    {
        get => _selectedConsole;
        set
        {
            SetField(ref _selectedConsole, value);
            OnPropertyChanged(nameof(ConsoleLabel));
            OnPropertyChanged(nameof(IsGcnSelected));
            OnPropertyChanged(nameof(IsWiiSelected));
            OnPropertyChanged(nameof(IsN64Selected));
            OnPropertyChanged(nameof(IsNesSelected));
            OnPropertyChanged(nameof(IsGbaSelected));
        }
    }

    public string ConsoleLabel => _selectedConsole.ToString();

    public IReadOnlyList<GameConsole> AvailableConsoles { get; } =
        Enum.GetValues<GameConsole>().ToArray();

    // ---- console visibility helpers ----------------------------------------

    public bool IsGcnSelected => _selectedConsole == GameConsole.GCN;
    public bool IsWiiSelected => _selectedConsole == GameConsole.WII;
    public bool IsN64Selected => _selectedConsole == GameConsole.N64;
    public bool IsNesSelected => _selectedConsole == GameConsole.NES;
    public bool IsGbaSelected => _selectedConsole == GameConsole.GBA;

    // ---- paths ------------------------------------------------------------

    private string _romPath = string.Empty;
    public string RomPath
    {
        get => _romPath;
        set { SetField(ref _romPath, value); OnPropertyChanged(nameof(CanInject)); }
    }

    private string _baseRomPath = string.Empty;
    public string BaseRomPath
    {
        get => _baseRomPath;
        set { SetField(ref _baseRomPath, value); OnPropertyChanged(nameof(CanInject)); }
    }

    /// <summary>When true, <see cref="BaseRomPath"/> is used directly as a custom base directory.</summary>
    public bool IsCustomBaseRom { get; set; } = true;

    // ---- common options ---------------------------------------------------

    private bool _debug;
    public bool Debug { get => _debug; set => SetField(ref _debug, value); }

    // ---- GCN options ------------------------------------------------------

    private bool _force4by3;
    public bool Force4by3 { get => _force4by3; set => SetField(ref _force4by3, value); }

    // ---- Wii options -------------------------------------------------------

    private bool _patchVideo;
    public bool PatchVideo { get => _patchVideo; set => SetField(ref _patchVideo, value); }

    private bool _regionFrii;
    public bool RegionFrii { get => _regionFrii; set => SetField(ref _regionFrii, value); }

    private bool _toPal;
    public bool ToPal { get => _toPal; set => SetField(ref _toPal, value); }

    private bool _forceNkitConvert;
    public bool ForceNkitConvert { get => _forceNkitConvert; set => SetField(ref _forceNkitConvert, value); }

    private bool _passthrough;
    public bool Passthrough { get => _passthrough; set => SetField(ref _passthrough, value); }

    private int _controllerIndex;
    public int ControllerIndex { get => _controllerIndex; set => SetField(ref _controllerIndex, value); }
    public IReadOnlyList<int> AvailableControllerIndices { get; } = [0, 1, 2, 3, 4];

    private bool _remapLR;
    public bool RemapLR { get => _remapLR; set => SetField(ref _remapLR, value); }

    // ---- N64 options -------------------------------------------------------

    private bool _wideScreen;
    public bool WideScreen { get => _wideScreen; set => SetField(ref _wideScreen, value); }

    private bool _n64DarkFilter;
    public bool N64DarkFilter { get => _n64DarkFilter; set => SetField(ref _n64DarkFilter, value); }

    // ---- NES options -------------------------------------------------------

    private string _nesPalette = "Default (Base RPX)";
    public string NesPalette { get => _nesPalette; set => SetField(ref _nesPalette, value); }
    public IReadOnlyList<string> AvailableNesPalettes { get; } = NesPalettePatcher.AvailablePalettes;

    // ---- GBA options -------------------------------------------------------

    private bool _gbaDarkFilter;
    public bool GbaDarkFilter { get => _gbaDarkFilter; set => SetField(ref _gbaDarkFilter, value); }

    private bool _pokePatch;
    public bool PokePatch { get => _pokePatch; set => SetField(ref _pokePatch, value); }

    // ---- state ------------------------------------------------------------

    private bool   _isBusy;
    private int    _progress;
    private string _statusMessage = string.Empty;
    private string _lastError     = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        private set { SetField(ref _isBusy, value); OnPropertyChanged(nameof(CanInject)); }
    }

    public int Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string LastError
    {
        get => _lastError;
        set => SetField(ref _lastError, value);
    }

    public bool CanInject =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(RomPath)
        && !string.IsNullOrWhiteSpace(BaseRomPath);

    public bool HasError => !string.IsNullOrWhiteSpace(LastError);

    // ---- inject -----------------------------------------------------------

    private CancellationTokenSource? _cts;

    /// <summary>
    /// Start injection.  The actual pipeline implementation is injected at
    /// app startup via <see cref="InjectPipelineFactory"/> so the ViewModel
    /// stays UI-free and testable.
    /// </summary>
    public async Task InjectAsync()
    {
        if (!CanInject) return;

        LastError     = string.Empty;
        IsBusy        = true;
        Progress      = 0;
        StatusMessage = "Starting…";

        _cts = new CancellationTokenSource();

        try
        {
            var pipeline = InjectPipelineFactory?.Invoke();
            if (pipeline is null)
            {
                LastError = "No inject pipeline registered.";
                return;
            }

            var cfg = new GameConfig
            {
                Console        = SelectedConsole,
                CustomBasePath = IsCustomBaseRom ? BaseRomPath : null,
                BaseRom        = IsCustomBaseRom ? null : new GameBaseRef
                {
                    Name = System.IO.Path.GetFileName(BaseRomPath),
                },
                Force4by3  = Force4by3,
                PokePatch  = PokePatch,
                NesPalette = NesPalette,
                N64Config  = IsN64Selected ? new EmulatorConfig
                {
                    WideScreen = WideScreen,
                    DarkFilter = N64DarkFilter,
                } : null,
                GbaConfig  = IsGbaSelected ? new EmulatorConfig
                {
                    DarkFilter = GbaDarkFilter,
                } : null,
            };

            var ctx = new UWUVCI.Core.Pipeline.InjectionContext
            {
                Config           = cfg,
                RomPath          = RomPath,
                ToolsPath        = ToolsPathProvider?.Invoke() ?? string.Empty,
                TempPath         = TempPathProvider?.Invoke()  ?? System.IO.Path.GetTempPath(),
                OutPath          = OutPathProvider?.Invoke()   ?? System.IO.Path.GetTempPath(),
                Debug            = Debug,
                PatchVideo       = PatchVideo,
                RegionFrii       = RegionFrii,
                ToPal            = ToPal,
                ForceNkitConvert = ForceNkitConvert,
                Passthrough      = Passthrough,
                ControllerIndex  = ControllerIndex,
                RemapLR          = RemapLR,
                Progress         = new LambdaProgressReporter((p, m) => { Progress = p; StatusMessage = m; }),
                Logger           = new UWUVCI.Core.Pipeline.NullJobLogger(),
            };

            var result = await pipeline.InjectAsync(ctx, _cts.Token);

            if (result.IsSuccess)
            {
                Progress      = 100;
                StatusMessage = "Injection complete.";
            }
            else if (result.IsCancelled)
            {
                StatusMessage = "Cancelled.";
            }
            else
            {
                LastError     = string.Join("\n", result.Errors);
                StatusMessage = "Injection failed.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            LastError     = ex.Message;
            StatusMessage = "Injection failed.";
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Cancel() => _cts?.Cancel();

    // ---- factories (set by App.xaml.cs before navigation) -----------------

    public Func<UWUVCI.Core.Pipeline.IInjectPipeline?>? InjectPipelineFactory { get; set; }
    public Func<string>? ToolsPathProvider { get; set; }
    public Func<string>? TempPathProvider  { get; set; }
    public Func<string>? OutPathProvider   { get; set; }
}

/// <summary>Adapter so lambdas can be passed as <see cref="UWUVCI.Core.Pipeline.IProgressReporter"/>.</summary>
file sealed class LambdaProgressReporter : UWUVCI.Core.Pipeline.IProgressReporter
{
    private readonly Action<int, string> _action;
    public LambdaProgressReporter(Action<int, string> action) => _action = action;
    public void Report(int percent, string message) => _action(percent, message);
}
