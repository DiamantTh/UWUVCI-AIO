using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Uno.Resizetizer;
using UWUVCI.App.Uno.ViewModels;
using UWUVCI.App.Uno.Views;
using UWUVCI.Config.Loaders;
using UWUVCI.Config.Models;
using UWUVCI.Services;
using UWUVCI.Tooling;
using AppPaths = UWUVCI.Core.Runtime.AppDataPaths;

namespace UWUVCI.App.Uno;

public partial class App : Application
{
    // ---- singletons accessible from pages ----------------------------------

    /// <summary>Shared window reference used by file/folder pickers.</summary>
    internal static Window MainWindow { get; private set; } = null!;

    internal static ShellViewModel   Shell    { get; } = new();
    internal static SettingsViewModel Settings { get; } = new(ResolveSettingsPath());
    internal static InjectViewModel   Inject   { get; } = new();
    internal static ToolsViewModel    Tools    { get; } = new();

    // ---- theme state -------------------------------------------------------

    private static string _currentTheme = "Dark";

    // ---- app startup -------------------------------------------------------

    public App() => this.InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();
#if DEBUG
        MainWindow.UseStudio();
#endif

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
            rootFrame.Navigate(typeof(ShellPage), args.Arguments);

        MainWindow.Title = "UWUVCI";
        MainWindow.SetWindowIcon();
        MainWindow.Activate();

        // Apply saved theme on launch
        ApplyTheme(Settings.Theme);

        // Subscribe to live settings changes
        SettingsViewModel.SettingsSaved += OnSettingsSaved;

        // Wire up the inject pipeline so InjectViewModel can actually run
        WireInjectPipeline();

        // Initialise the tools view model with the manifest
        Tools.Initialise(LoadManifest(), ResolveToolsDir(), new PlatformInfo(Settings.NativeWindows));
    }

    internal static string ResolveToolsDir()
        => Settings.ToolsPath is { Length: > 0 } tp ? tp : AppPaths.ToolsDir;

    internal static ToolManifestModel LoadManifest()
    {
        var toolsDir = ResolveToolsDir();

        // 1. User-placed tools.toml in the tools directory takes precedence
        var userToml = System.IO.Path.Combine(toolsDir, "tools.toml");
        if (System.IO.File.Exists(userToml))
        {
            try { return ToolManifestLoader.LoadFromFile(userToml); } catch { /* fall through */ }
        }

        // 2. Fall back to the bundled embedded tools.toml
        try
        {
            var asm  = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                          .FirstOrDefault(n => n.EndsWith("tools.toml", StringComparison.OrdinalIgnoreCase));
            if (name is not null)
            {
                using var stream = asm.GetManifestResourceStream(name)!;
                using var reader = new System.IO.StreamReader(stream);
                return ToolManifestLoader.LoadFromString(reader.ReadToEnd());
            }
        }
        catch { /* best effort */ }

        return new ToolManifestModel();
    }

    private static void WireInjectPipeline()
    {
        var platform  = new PlatformInfo(Settings.NativeWindows);
        var toolsDir  = ResolveToolsDir();
        var manifest  = LoadManifest();

        var resolver     = new ManifestToolResolver(manifest, toolsDir, platform);
        var runner       = new ProcessToolRunner(resolver, platform);
        var orchestrator = new InjectOrchestrator(runner, platform);

        Inject.InjectPipelineFactory = () => orchestrator;
        Inject.ToolsPathProvider     = () => toolsDir;
        Inject.TempPathProvider      = () => AppPaths.TempDir;
        Inject.OutPathProvider       = () =>
            Settings.OutPath is { Length: > 0 } op
                ? op
                : AppPaths.OutputDir;
    }

    private void OnSettingsSaved(AppSettingsModel model)
    {
        ApplyTheme(model.Theme ?? "Dark");
    }

    // ---- theme switching ---------------------------------------------------

    /// <summary>
    /// Swaps the theme ResourceDictionary in Application.Resources at runtime.
    /// No restart required.
    /// </summary>
    internal static void ApplyTheme(string theme)
    {
        var normalized = theme?.Trim() ?? "Dark";
        if (string.Equals(normalized, _currentTheme, StringComparison.OrdinalIgnoreCase))
            return;

        var uri = normalized.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? new Uri("ms-appx:///Themes/Theme.Light.xaml")
            : new Uri("ms-appx:///Themes/Theme.Dark.xaml");

        var merged = Application.Current.Resources.MergedDictionaries;

        // Remove the previous theme dict (last one added by convention)
        for (int i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.OriginalString ?? "";
            if (src.Contains("Theme.Dark") || src.Contains("Theme.Light"))
            {
                merged.RemoveAt(i);
                break;
            }
        }

        merged.Add(new ResourceDictionary { Source = uri });
        _currentTheme = normalized;
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        => throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");

    // ---- helpers -----------------------------------------------------------

    private static string ResolveSettingsPath() => AppPaths.SettingsFile;

    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
