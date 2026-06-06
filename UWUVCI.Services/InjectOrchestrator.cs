using UWUVCI.Core.Models;
using UWUVCI.Core.Pipeline;
using UWUVCI.Core.Runtime;
using UWUVCI.Core.Tooling;

namespace UWUVCI.Services;

/// <summary>
/// Central injection orchestrator.
/// Implements <see cref="IInjectPipeline"/> and dispatches to the correct
/// console-specific service based on <see cref="InjectionContext.Config.Console"/>.
/// </summary>
public sealed class InjectOrchestrator : IInjectPipeline
{
    private readonly IToolRunner  _runner;
    private readonly IPlatformInfo _platform;

    public InjectOrchestrator(IToolRunner runner, IPlatformInfo platform)
    {
        _runner   = runner;
        _platform = platform;
    }

    public async Task<InjectionResult> InjectAsync(
        InjectionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            context.Progress.Report(5, $"Preparing {context.Config.Console} injection…");

            // Ensure the base ROM path is resolved
            var baseRomPath = ResolveBaseRomPath(context);

            // Ensure directories exist
            Directory.CreateDirectory(context.TempPath);
            Directory.CreateDirectory(context.OutPath);

            string? outputPath = await DispatchAsync(context, baseRomPath, ct).ConfigureAwait(false);

            context.Progress.Report(100, "Done.");
            return InjectionResult.Ok(outputPath ?? context.OutPath);
        }
        catch (OperationCanceledException)
        {
            return InjectionResult.WasCancelled();
        }
        catch (Exception ex)
        {
            return InjectionResult.Fail(ex.Message);
        }
    }

    // ---- dispatch ----------------------------------------------------------

    private async Task<string?> DispatchAsync(
        InjectionContext ctx,
        string baseRomPath,
        CancellationToken ct)
    {
        switch (ctx.Config.Console)
        {
            case GameConsole.GCN:
                return await InjectGcnAsync(ctx, baseRomPath, ct).ConfigureAwait(false);

            case GameConsole.WII:
                return await InjectWiiAsync(ctx, baseRomPath, ct).ConfigureAwait(false);

            case GameConsole.N64:
                return await InjectN64Async(ctx, baseRomPath, ct).ConfigureAwait(false);

            case GameConsole.NES:
            case GameConsole.SNES:
                return await InjectNesSnesAsync(ctx, baseRomPath, ct).ConfigureAwait(false);

            case GameConsole.GBA:
                return await InjectGbaAsync(ctx, baseRomPath, ct).ConfigureAwait(false);

            case GameConsole.NDS:
                return await InjectNdsAsync(ctx, baseRomPath, ct).ConfigureAwait(false);

            case GameConsole.TG16:
                return await InjectTg16Async(ctx, baseRomPath, ct).ConfigureAwait(false);

            case GameConsole.MSX:
                await MsxInjectService.InjectAsync(baseRomPath, ctx.RomPath, ct).ConfigureAwait(false);
                return ctx.OutPath;

            default:
                throw new NotSupportedException($"Console {ctx.Config.Console} is not yet supported.");
        }
    }

    // ---- per-console wrappers ----------------------------------------------

    private async Task<string?> InjectGcnAsync(InjectionContext ctx, string baseRomPath, CancellationToken ct)
    {
        ctx.Progress.Report(10, "Injecting GameCube…");
        var opt = new GcnInjectOptions
        {
            Debug    = ctx.Debug,
            Force43  = ctx.Config.Force4by3,
            Log      = msg => ctx.Logger.Log(msg),
        };
        await GCNInjectService.InjectAsync(
            ctx.ToolsPath, ctx.TempPath, baseRomPath, ctx.RomPath,
            opt, _platform, _runner, ct).ConfigureAwait(false);
        return ctx.OutPath;
    }

    private async Task<string?> InjectWiiAsync(InjectionContext ctx, string baseRomPath, CancellationToken ct)
    {
        ctx.Progress.Report(10, "Injecting Wii…");
        var opt = new WiiInjectOptions
        {
            Debug            = ctx.Debug,
            ForceNkitConvert = ctx.ForceNkitConvert,
            PatchVideo       = ctx.PatchVideo,
            ToPal            = ctx.ToPal,
            Passthrough      = ctx.Passthrough,
            Index            = ctx.ControllerIndex,
            LR               = ctx.RemapLR,
            Log              = msg => ctx.Logger.Log(msg),
            Progress         = (p, m) => ctx.Progress.Report((int)(10 + p * 0.8), m),
        };
        await WiiInjectService.InjectStandardAsync(
            ctx.ToolsPath, ctx.TempPath, baseRomPath, ctx.RomPath,
            opt, _platform, _runner, ct).ConfigureAwait(false);
        return ctx.OutPath;
    }

    private async Task<string?> InjectN64Async(InjectionContext ctx, string baseRomPath, CancellationToken ct)
    {
        ctx.Progress.Report(10, "Injecting N64…");
        var n64cfg = ctx.Config.N64Config;
        var opt = new N64InjectOptions
        {
            Debug      = ctx.Debug,
            WideScreen = n64cfg?.WideScreen ?? false,
            DarkFilter = n64cfg?.DarkFilter ?? false,
            IniPath    = n64cfg?.IniPath,
            IniBin     = n64cfg?.IniBin,
        };
        await N64InjectService.InjectAsync(
            ctx.ToolsPath, baseRomPath, ctx.RomPath,
            opt, _runner, ct).ConfigureAwait(false);
        return ctx.OutPath;
    }

    private async Task<string?> InjectNesSnesAsync(InjectionContext ctx, string baseRomPath, CancellationToken ct)
    {
        bool isNes = ctx.Config.Console == GameConsole.NES;
        ctx.Progress.Report(10, isNes ? "Injecting NES…" : "Injecting SNES…");
        var opt = new NesSnesInjectOptions
        {
            IsNes             = isNes,
            Debug             = ctx.Debug,
            NesPaletteName    = ctx.Config.NesPalette,
            DefaultPaletteName = "Default (Base RPX)",
        };
        await NesSnesInjectService.InjectAsync(
            ctx.ToolsPath, baseRomPath, ctx.RomPath,
            opt, _runner, ct).ConfigureAwait(false);
        return ctx.OutPath;
    }

    private async Task<string?> InjectGbaAsync(InjectionContext ctx, string baseRomPath, CancellationToken ct)
    {
        ctx.Progress.Report(10, "Injecting GBA…");
        var gbacfg = ctx.Config.GbaConfig;
        var opt = new GbaInjectOptions
        {
            Debug      = ctx.Debug,
            DarkFilter = gbacfg?.DarkFilter ?? false,
            PokePatch  = ctx.Config.PokePatch,
        };
        await GbaInjectService.InjectAsync(
            ctx.ToolsPath, ctx.TempPath, baseRomPath, ctx.RomPath,
            opt, _runner, ct).ConfigureAwait(false);
        return ctx.OutPath;
    }

    private async Task<string?> InjectNdsAsync(InjectionContext ctx, string baseRomPath, CancellationToken ct)
    {
        ctx.Progress.Report(10, "Injecting NDS…");
        var opt = new NdsInjectOptions
        {
            Debug = ctx.Debug,
        };
        await NdsInjectService.InjectAsync(
            ctx.ToolsPath, ctx.TempPath, baseRomPath, ctx.RomPath,
            opt, _runner, ct).ConfigureAwait(false);
        return ctx.OutPath;
    }

    private async Task<string?> InjectTg16Async(InjectionContext ctx, string baseRomPath, CancellationToken ct)
    {
        ctx.Progress.Report(10, "Injecting TurboGrafx…");
        await Tg16InjectService.InjectAsync(
            ctx.ToolsPath, ctx.TempPath, baseRomPath, ctx.RomPath,
            _runner, ct).ConfigureAwait(false);
        return ctx.OutPath;
    }

    // ---- helpers -----------------------------------------------------------

    private static string ResolveBaseRomPath(InjectionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.Config.CustomBasePath))
            return ctx.Config.CustomBasePath;

        if (ctx.Config.BaseRom is { IsCustom: false } baseRef)
        {
            var candidate = Path.Combine(AppDataPaths.BasesDir, baseRef.Name);
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException(
            "No base ROM path found. Set GameConfig.CustomBasePath or select a downloaded base.");
    }
}
