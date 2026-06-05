using UWUVCI.Core.Models;

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
        }
    }

    public string ConsoleLabel => _selectedConsole.ToString();

    public IReadOnlyList<GameConsole> AvailableConsoles { get; } =
        Enum.GetValues<GameConsole>().ToArray();

    // ---- paths ------------------------------------------------------------

    private string _romPath = string.Empty;
    public string RomPath
    {
        get => _romPath;
        set => SetField(ref _romPath, value);
    }

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

    public bool CanInject => !IsBusy && !string.IsNullOrWhiteSpace(RomPath);
    public bool HasError  => !string.IsNullOrWhiteSpace(LastError);

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

            var ctx = new UWUVCI.Core.Pipeline.InjectionContext
            {
                Config    = new GameConfig { Console = SelectedConsole },
                RomPath   = RomPath,
                ToolsPath = ToolsPathProvider?.Invoke() ?? string.Empty,
                TempPath  = TempPathProvider?.Invoke()  ?? System.IO.Path.GetTempPath(),
                OutPath   = OutPathProvider?.Invoke()   ?? System.IO.Path.GetTempPath(),
                Progress  = new LambdaProgressReporter((p, m) => { Progress = p; StatusMessage = m; }),
                Logger    = new UWUVCI.Core.Pipeline.NullJobLogger(),
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
