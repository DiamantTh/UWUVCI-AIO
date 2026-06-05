using Microsoft.VisualStudio.TestTools.UnitTesting;
using UWUVCI.Core.Models;
using UWUVCI.Core.Pipeline;

namespace UWUVCI.Tests;

// ---- GameConsole enum -------------------------------------------------------

[TestClass]
public class GameConsoleTests
{
    [TestMethod]
    [TestCategory("Injection")]
    public void GameConsole_Values_MatchLegacy()
    {
        // Values must stay stable – they are persisted in config files.
        Assert.AreEqual(0, (int)GameConsole.NDS);
        Assert.AreEqual(1, (int)GameConsole.N64);
        Assert.AreEqual(2, (int)GameConsole.GBA);
        Assert.AreEqual(3, (int)GameConsole.NES);
        Assert.AreEqual(4, (int)GameConsole.SNES);
        Assert.AreEqual(5, (int)GameConsole.TG16);
        Assert.AreEqual(6, (int)GameConsole.MSX);
        Assert.AreEqual(7, (int)GameConsole.WII);
        Assert.AreEqual(8, (int)GameConsole.GCN);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void Region_Values_MatchLegacy()
    {
        Assert.AreEqual(0, (int)Region.EU);
        Assert.AreEqual(1, (int)Region.US);
        Assert.AreEqual(2, (int)Region.JP);
    }
}

// ---- GameConfig / ImageAsset ------------------------------------------------

[TestClass]
public class GameConfigTests
{
    [TestMethod]
    [TestCategory("Injection")]
    public void GameConfig_DefaultValues_AreCorrect()
    {
        var cfg = new GameConfig();
        Assert.AreEqual(WiiTrimMode.Trim, cfg.WiiTrimMode);
        Assert.AreEqual("Default (Base RPX)", cfg.NesPalette);
        Assert.IsNull(cfg.BaseRom);
        Assert.IsNull(cfg.GameName);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void GameConfig_Clone_IsShallowCopy()
    {
        var cfg = new GameConfig
        {
            Console  = GameConsole.N64,
            GameName = "Star Fox 64",
        };
        var clone = cfg.Clone();

        Assert.AreEqual(GameConsole.N64, clone.Console);
        Assert.AreEqual("Star Fox 64",   clone.GameName);
        Assert.IsFalse(ReferenceEquals(cfg, clone));
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void GameConfig_BootImages_ReturnsAllFourKeys()
    {
        var cfg = new GameConfig
        {
            IconTex    = new ImageAsset { ImgPath = "icon.tga" },
            BootTvTex  = new ImageAsset { ImgPath = "tv.tga" },
        };

        var images = cfg.BootImages;
        Assert.AreEqual(4, images.Count);
        Assert.AreEqual("iconTex",    images[0].Key);
        Assert.AreEqual("bootTvTex",  images[1].Key);
        Assert.AreEqual("bootDrcTex", images[2].Key);
        Assert.AreEqual("bootLogoTex",images[3].Key);
        Assert.IsNotNull(images[0].Asset);
        Assert.IsNull(images[2].Asset);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void ImageAsset_HasContent_Path()
    {
        var a = new ImageAsset { ImgPath = "foo.png" };
        Assert.IsTrue(a.HasContent);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void ImageAsset_HasContent_Bytes()
    {
        var a = new ImageAsset { ImgBin = [1, 2, 3] };
        Assert.IsTrue(a.HasContent);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void ImageAsset_HasContent_Empty()
    {
        var a = new ImageAsset();
        Assert.IsFalse(a.HasContent);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void GameBaseRef_IsCustom_WhenNameIsCustom()
    {
        var b = new GameBaseRef { Name = "Custom" };
        Assert.IsTrue(b.IsCustom);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void GameBaseRef_IsNotCustom_ForCatalogueEntry()
    {
        var b = new GameBaseRef { Name = "Super Metroid", Region = "EU" };
        Assert.IsFalse(b.IsCustom);
    }
}

// ---- Pipeline abstractions --------------------------------------------------

[TestClass]
public class PipelineAbstractionTests
{
    [TestMethod]
    [TestCategory("Injection")]
    public void NullProgressReporter_DoesNotThrow()
    {
        var r = NullProgressReporter.Instance;
        r.Report(0,   "start");
        r.Report(50,  "half");
        r.Report(100, "done");
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void NullJobLogger_DoesNotThrow()
    {
        var l = NullJobLogger.Instance;
        l.Log("hello");
        l.LogError("oops", new Exception("test"));
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void CapturingProgressReporter_AccumulatesReports()
    {
        var r = new CapturingProgressReporter();
        r.Report(10, "a");
        r.Report(50, "b");
        r.Report(100, "done");

        Assert.AreEqual(3,     r.Reports.Count);
        Assert.AreEqual(100,   r.LastPercent);
        Assert.AreEqual("b",   r.Reports[1].Message);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void CapturingJobLogger_AccumulatesEntries()
    {
        var l = new CapturingJobLogger();
        l.Log("step 1");
        l.LogError("boom", new Exception("bad"));

        Assert.AreEqual(2, l.Entries.Count);
        StringAssert.Contains(l.Entries[0], "[INFO]");
        StringAssert.Contains(l.Entries[1], "[ERROR]");
        StringAssert.Contains(l.Entries[1], "bad");
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void InjectionResult_Ok_SetsOutputPath()
    {
        var r = InjectionResult.Ok("/out/MyGame");
        Assert.IsTrue(r.IsSuccess);
        Assert.AreEqual("/out/MyGame", r.OutputPath);
        Assert.AreEqual(0, r.Errors.Count);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void InjectionResult_Fail_SetsErrors()
    {
        var r = InjectionResult.Fail("Missing base", "Invalid ROM");
        Assert.IsTrue(r.IsFailure);
        Assert.AreEqual(2, r.Errors.Count);
        Assert.IsNull(r.OutputPath);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void InjectionResult_Cancelled_SetsCancelledFlag()
    {
        var r = InjectionResult.WasCancelled();
        Assert.IsTrue(r.IsCancelled);
        Assert.IsFalse(r.IsSuccess);
        Assert.IsFalse(r.IsFailure);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void InjectionContext_DefaultsToNullImplementations()
    {
        var ctx = new InjectionContext
        {
            Config    = new GameConfig(),
            RomPath   = "/roms/game.rom",
            ToolsPath = "/tools",
            TempPath  = "/tmp/uwuvci",
            OutPath   = "/out",
        };

        // Should not throw – null-safe defaults wired up
        ctx.Progress.Report(5, "test");
        ctx.Logger.Log("test");
        Assert.IsInstanceOfType<NullProgressReporter>(ctx.Progress);
        Assert.IsInstanceOfType<NullJobLogger>(ctx.Logger);
    }
}

// ---- InjectOptions ----------------------------------------------------------

[TestClass]
public class InjectOptionsTests
{
    [TestMethod]
    [TestCategory("Injection")]
    public void InjectKind_Values_AreStable()
    {
        Assert.AreEqual(0, (int)InjectKind.WiiStandard);
        Assert.AreEqual(1, (int)InjectKind.WiiHomebrew);
        Assert.AreEqual(2, (int)InjectKind.WiiForwarder);
        Assert.AreEqual(3, (int)InjectKind.GCN);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void WiiInjectOptions_DefaultPassthroughFalse()
    {
        var opt = new WiiInjectOptions();
        Assert.IsFalse(opt.Passthrough);
    }

    [TestMethod]
    [TestCategory("Injection")]
    public void GcnInjectOptions_DefaultPassthroughTrue()
    {
        var opt = new GcnInjectOptions();
        Assert.IsTrue(opt.Passthrough);
    }
}

// ---- IInjectStep via fake ---------------------------------------------------

[TestClass]
public class InjectStepTests
{
    [TestMethod]
    [TestCategory("Injection")]
    public async Task InjectionStepContext_CollectsErrors()
    {
        var progress = new CapturingProgressReporter();
        var logger   = new CapturingJobLogger();

        var input = new InjectionContext
        {
            Config    = new GameConfig { Console = GameConsole.NES },
            RomPath   = "/roms/game.nes",
            ToolsPath = "/tools",
            TempPath  = "/tmp",
            OutPath   = "/out",
            Progress  = progress,
            Logger    = logger,
        };

        var stepCtx = new InjectionStepContext { Input = input };
        stepCtx.Errors.Add("simulated error");
        stepCtx.ProductCode = "WNES";

        Assert.AreEqual(1, stepCtx.Errors.Count);
        Assert.AreEqual("WNES", stepCtx.ProductCode);
        Assert.AreSame(progress, stepCtx.Progress);
        Assert.AreSame(logger,   stepCtx.Logger);

        await Task.CompletedTask; // satisfy async test signature
    }
}
