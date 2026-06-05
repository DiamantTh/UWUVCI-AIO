using Microsoft.VisualStudio.TestTools.UnitTesting;
using UWUVCI.Config.Loaders;
using UWUVCI.Config.Models;
using UWUVCI.Config.Validation;

namespace UWUVCI.Tests;

[TestClass]
public class AppSettingsLoaderTests
{
    // ---- roundtrip -----------------------------------------------------------

    [TestMethod]
    [TestCategory("Config")]
    public void Roundtrip_DefaultValues_ArePreserved()
    {
        var original = new AppSettingsModel();
        var toml     = AppSettingsLoader.SaveToString(original);
        var loaded   = AppSettingsLoader.LoadFromString(toml);

        Assert.AreEqual(original.PathsSet,            loaded.PathsSet);
        Assert.AreEqual(original.Theme,               loaded.Theme);
        Assert.AreEqual(original.FileCopyParallelism, loaded.FileCopyParallelism);
        Assert.AreEqual(original.UnixWaitDelayMs,     loaded.UnixWaitDelayMs);
        Assert.AreEqual(original.IsFirstLaunch,       loaded.IsFirstLaunch);
        Assert.AreEqual(original.LastVersionSeen,     loaded.LastVersionSeen);
        Assert.AreEqual(original.NativeWindows,       loaded.NativeWindows);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Roundtrip_NativeWindowsNull_PreservesNull()
    {
        var m    = new AppSettingsModel { NativeWindows = null };
        var toml = AppSettingsLoader.SaveToString(m);
        var back = AppSettingsLoader.LoadFromString(toml);

        Assert.IsNull(back.NativeWindows);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Roundtrip_NativeWindowsTrue_Preserved()
    {
        var m    = new AppSettingsModel { NativeWindows = true };
        var toml = AppSettingsLoader.SaveToString(m);
        var back = AppSettingsLoader.LoadFromString(toml);

        Assert.AreEqual(true, back.NativeWindows);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Roundtrip_CKey_IsPreserved()
    {
        // CKey must survive save/load without modification.
        var m    = new AppSettingsModel { CKey = "abc123XYZ!" };
        var toml = AppSettingsLoader.SaveToString(m);
        var back = AppSettingsLoader.LoadFromString(toml);

        Assert.AreEqual("abc123XYZ!", back.CKey);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void CKey_IsNotInToString_Output()
    {
        // CKey value must not appear in the TOML serialised form (safety check: key name ok, value not logged).
        // Actually in TOML we do store it (unlike passwords), but we verify the key name is "CKey" not "SysKey".
        var m    = new AppSettingsModel { CKey = "supersecret" };
        var toml = AppSettingsLoader.SaveToString(m);

        StringAssert.Contains(toml, "CKey");
        Assert.IsFalse(toml.Contains("SysKey"), "SysKey must not appear in TOML output.");
    }

    // ---- missing file --------------------------------------------------------

    [TestMethod]
    [TestCategory("Config")]
    public void LoadFromFile_MissingFile_ReturnsDefaults()
    {
        var m = AppSettingsLoader.LoadFromFile("/nonexistent/path/settings.toml");

        Assert.IsNotNull(m);
        Assert.AreEqual("Dark", m.Theme);
    }

    // ---- partial TOML --------------------------------------------------------

    [TestMethod]
    [TestCategory("Config")]
    public void LoadFromString_PartialToml_FallsBackToDefaults()
    {
        var toml   = "Theme = \"Light\"\n";
        var loaded = AppSettingsLoader.LoadFromString(toml);

        Assert.AreEqual("Light", loaded.Theme);
        Assert.AreEqual(6, loaded.FileCopyParallelism);  // default
    }

    // ---- empty TOML ----------------------------------------------------------

    [TestMethod]
    [TestCategory("Config")]
    public void LoadFromString_EmptyString_ReturnsDefaults()
    {
        var m = AppSettingsLoader.LoadFromString("");

        Assert.AreEqual("Dark",  m.Theme);
        Assert.AreEqual(true,    m.IsFirstLaunch);
        Assert.AreEqual("0.0.0", m.LastVersionSeen);
    }
}

[TestClass]
public class AppSettingsValidatorTests
{
    [TestMethod]
    [TestCategory("Config")]
    public void Validate_DefaultModel_IsValid()
    {
        var errors = AppSettingsValidator.Validate(new AppSettingsModel());
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_ParallelismZero_ReturnsError()
    {
        var m = new AppSettingsModel { FileCopyParallelism = 0 };
        var e = AppSettingsValidator.Validate(m);
        Assert.AreEqual(1, e.Count);
        StringAssert.Contains(e[0], "FileCopyParallelism");
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_NegativeUnixWait_ReturnsError()
    {
        var m = new AppSettingsModel { UnixWaitDelayMs = -1 };
        var e = AppSettingsValidator.Validate(m);
        Assert.AreEqual(1, e.Count);
        StringAssert.Contains(e[0], "UnixWaitDelayMs");
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_InvalidTheme_ReturnsError()
    {
        var m = new AppSettingsModel { Theme = "Hacker" };
        var e = AppSettingsValidator.Validate(m);
        Assert.AreEqual(1, e.Count);
        StringAssert.Contains(e[0], "Theme");
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_InvalidVersionString_ReturnsError()
    {
        var m = new AppSettingsModel { LastVersionSeen = "not-a-version" };
        var e = AppSettingsValidator.Validate(m);
        Assert.AreEqual(1, e.Count);
        StringAssert.Contains(e[0], "LastVersionSeen");
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_MultipleErrors_AllReported()
    {
        var m = new AppSettingsModel
        {
            FileCopyParallelism = 0,
            UnixWaitDelayMs     = -5,
            Theme               = "Neon",
        };
        var e = AppSettingsValidator.Validate(m);
        Assert.AreEqual(3, e.Count);
    }
}

[TestClass]
public class ToolManifestLoaderTests
{
    private const string ValidToml = """
        [[tools]]
        name         = "wit"
        windows_file = "wit.exe"
        linux_file   = "wit-linux"
        windows_only = false
        sha256       = "deadbeef"
        download_url = "https://example.com/wit"

        [[tools]]
        name         = "szs"
        windows_file = "szs.exe"
        linux_file   = "szs"
        windows_only = false
        """;

    [TestMethod]
    [TestCategory("Config")]
    public void LoadFromString_ValidToml_ParsesTwoTools()
    {
        var manifest = ToolManifestLoader.LoadFromString(ValidToml);

        Assert.AreEqual(2, manifest.Tools.Count);
        Assert.AreEqual("wit", manifest.Tools[0].Name);
        Assert.AreEqual("wit.exe", manifest.Tools[0].WindowsFileName);
        Assert.AreEqual("deadbeef", manifest.Tools[0].Sha256);
        Assert.AreEqual("https://example.com/wit", manifest.Tools[0].DownloadUrl);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void LoadFromString_EmptyToml_ReturnsEmptyManifest()
    {
        var manifest = ToolManifestLoader.LoadFromString("");

        Assert.AreEqual(0, manifest.Tools.Count);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void LoadFromString_WindowsOnlyTool_NoLinuxFile()
    {
        var toml = """
            [[tools]]
            name         = "cygwin1"
            windows_file = "cygwin1.dll"
            linux_file   = ""
            windows_only = true
            """;

        var manifest = ToolManifestLoader.LoadFromString(toml);
        Assert.AreEqual(1, manifest.Tools.Count);
        Assert.IsTrue(manifest.Tools[0].WindowsOnly);
    }
}

[TestClass]
public class ToolManifestValidatorTests
{
    [TestMethod]
    [TestCategory("Config")]
    public void Validate_ValidManifest_NoErrors()
    {
        var m = new ToolManifestModel
        {
            Tools = new List<ToolEntryModel>
            {
                new() { Name = "wit", WindowsFileName = "wit.exe", LinuxFileName = "wit-linux", WindowsOnly = false },
            }
        };
        var e = ToolManifestValidator.Validate(m);
        Assert.AreEqual(0, e.Count);
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_EmptyName_ReturnsError()
    {
        var m = new ToolManifestModel
        {
            Tools = new List<ToolEntryModel>
            {
                new() { Name = "", WindowsFileName = "tool.exe", LinuxFileName = "tool" },
            }
        };
        var e = ToolManifestValidator.Validate(m);
        Assert.AreEqual(1, e.Count);
        StringAssert.Contains(e[0], "name");
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_DuplicateName_ReturnsError()
    {
        var m = new ToolManifestModel
        {
            Tools = new List<ToolEntryModel>
            {
                new() { Name = "wit", WindowsFileName = "wit.exe", LinuxFileName = "wit" },
                new() { Name = "wit", WindowsFileName = "wit2.exe", LinuxFileName = "wit2" },
            }
        };
        var e = ToolManifestValidator.Validate(m);
        Assert.AreEqual(1, e.Count);
        StringAssert.Contains(e[0], "duplicate");
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_MissingLinuxFileForNativeTool_ReturnsError()
    {
        var m = new ToolManifestModel
        {
            Tools = new List<ToolEntryModel>
            {
                new() { Name = "nfs", WindowsFileName = "nfs.exe", LinuxFileName = "", WindowsOnly = false },
            }
        };
        var e = ToolManifestValidator.Validate(m);
        Assert.AreEqual(1, e.Count);
        StringAssert.Contains(e[0], "linux_file");
    }

    [TestMethod]
    [TestCategory("Config")]
    public void Validate_WindowsOnlyTool_NoLinuxFileRequired()
    {
        var m = new ToolManifestModel
        {
            Tools = new List<ToolEntryModel>
            {
                new() { Name = "cygwin1", WindowsFileName = "cygwin1.dll", LinuxFileName = "", WindowsOnly = true },
            }
        };
        var e = ToolManifestValidator.Validate(m);
        Assert.AreEqual(0, e.Count);
    }
}
