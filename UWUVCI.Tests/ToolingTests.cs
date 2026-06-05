using Microsoft.VisualStudio.TestTools.UnitTesting;
using UWUVCI.Config.Models;
using UWUVCI.Core.Tooling;
using UWUVCI.Tooling;

namespace UWUVCI.Tests;

// ---------------------------------------------------------------------------
// Fake IPlatformInfo helpers
// ---------------------------------------------------------------------------

file sealed class FakeLinuxPlatform : IPlatformInfo
{
    public bool IsNativeWindows => false;
    public bool IsLinux         => true;
    public bool IsWineLike      => false;
    public string? WineFlavor   => null;

    public string ToWindowsPath(string p) => p;
    public string ToHostPath(string p)    => p;
}

file sealed class FakeWindowsPlatform : IPlatformInfo
{
    public bool IsNativeWindows => true;
    public bool IsLinux         => false;
    public bool IsWineLike      => false;
    public string? WineFlavor   => null;

    public string ToWindowsPath(string p) => p;
    public string ToHostPath(string p)    => p;
}

file sealed class FakeWinePlatform : IPlatformInfo
{
    public bool IsNativeWindows => false;
    public bool IsLinux         => false;
    public bool IsWineLike      => true;
    public string? WineFlavor   => "Wine";

    public string ToWindowsPath(string hostPath)
    {
        if (string.IsNullOrEmpty(hostPath)) return hostPath;
        if (hostPath[0] == '/')
            return @"Z:\" + hostPath.TrimStart('/').Replace('/', '\\');
        return hostPath;
    }

    public string ToHostPath(string winPath)
    {
        if (string.IsNullOrEmpty(winPath)) return winPath;
        if (winPath.StartsWith(@"Z:\", StringComparison.OrdinalIgnoreCase) ||
            winPath.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase))
            return "/" + winPath.Substring(3).Replace('\\', '/');
        return winPath;
    }
}

// ---------------------------------------------------------------------------
// PlatformInfo path-mapping tests
// ---------------------------------------------------------------------------

[TestClass]
public class PlatformInfoPathTests
{
    private static IPlatformInfo Wine => new FakeWinePlatform();

    [TestMethod]
    [TestCategory("Tooling")]
    public void ToWindowsPath_PosixRoot_MapsToZ()
    {
        var result = Wine.ToWindowsPath("/home/user/tools");
        Assert.AreEqual(@"Z:\home\user\tools", result);
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void ToHostPath_ZDrive_MapsToRoot()
    {
        var result = Wine.ToHostPath(@"Z:\home\user\tools");
        Assert.AreEqual("/home/user/tools", result);
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void ToHostPath_ZDriveForwardSlash_MapsToRoot()
    {
        var result = Wine.ToHostPath("Z:/home/user/tools");
        Assert.AreEqual("/home/user/tools", result);
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void ToWindowsPath_AlreadyWindowsPath_ReturnedUnchanged()
    {
        var result = Wine.ToWindowsPath(@"C:\already\windows");
        Assert.AreEqual(@"C:\already\windows", result);
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void ToWindowsPath_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual("", Wine.ToWindowsPath(""));
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void ToHostPath_EmptyString_ReturnsEmpty()
    {
        Assert.AreEqual("", Wine.ToHostPath(""));
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void Roundtrip_PosixPath_Preserved()
    {
        var original = "/home/thomas/.uwuvci/tools";
        var winPath  = Wine.ToWindowsPath(original);
        var back     = Wine.ToHostPath(winPath);
        Assert.AreEqual(original, back);
    }
}

// ---------------------------------------------------------------------------
// PlatformInfo (real) – only safe probes that do not depend on OS
// ---------------------------------------------------------------------------

[TestClass]
public class PlatformInfoRealTests
{
    [TestMethod]
    [TestCategory("Tooling")]
    public void NativeWindowsOverrideTrue_IsNativeWindows()
    {
        var p = new PlatformInfo(nativeWindowsOverride: true);
        Assert.IsTrue(p.IsNativeWindows);
        Assert.IsFalse(p.IsWineLike);
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void NativeWindowsOverrideFalse_IsWineLike()
    {
        var p = new PlatformInfo(nativeWindowsOverride: false);
        Assert.IsFalse(p.IsNativeWindows);
        Assert.IsTrue(p.IsWineLike);
    }
}

// ---------------------------------------------------------------------------
// ManifestToolResolver tests
// ---------------------------------------------------------------------------

[TestClass]
public class ManifestToolResolverTests
{
    private static ToolManifestModel MakeManifest() => new()
    {
        Tools = new List<ToolEntryModel>
        {
            new() { Name = "wit",     WindowsFileName = "wit.exe",     LinuxFileName = "wit-linux",  WindowsOnly = false },
            new() { Name = "nfs",     WindowsFileName = "nfs.exe",     LinuxFileName = "",            WindowsOnly = true  },
            new() { Name = "cygwin1", WindowsFileName = "cygwin1.dll", LinuxFileName = "",            WindowsOnly = true  },
        }
    };

    private static string CreateFakeToolsDir(params string[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "uwuvci_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var f in files)
            File.WriteAllText(Path.Combine(dir, f), "fake");
        return dir;
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void Resolve_Linux_PrefersNativeBinary()
    {
        var dir = CreateFakeToolsDir("wit-linux", "wit.exe");
        try
        {
            var resolver = new ManifestToolResolver(MakeManifest(), dir, new FakeLinuxPlatform());
            var result   = resolver.Resolve("wit");
            StringAssert.EndsWith(result, "wit-linux");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void Resolve_Linux_FallsBackToExeWhenNoNative()
    {
        var dir = CreateFakeToolsDir("wit.exe");
        try
        {
            var resolver = new ManifestToolResolver(MakeManifest(), dir, new FakeLinuxPlatform());
            var result   = resolver.Resolve("wit");
            StringAssert.EndsWith(result, "wit.exe");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void Resolve_Windows_UsesExe()
    {
        var dir = CreateFakeToolsDir("wit.exe");
        try
        {
            var resolver = new ManifestToolResolver(MakeManifest(), dir, new FakeWindowsPlatform());
            var result   = resolver.Resolve("wit");
            StringAssert.EndsWith(result, "wit.exe");
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void Resolve_UnknownTool_ReturnsNull()
    {
        var dir = CreateFakeToolsDir();
        try
        {
            var resolver = new ManifestToolResolver(MakeManifest(), dir, new FakeLinuxPlatform());
            Assert.IsNull(resolver.Resolve("nonexistent_tool"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void IsAvailable_ExistingTool_True()
    {
        var dir = CreateFakeToolsDir("wit-linux");
        try
        {
            var resolver = new ManifestToolResolver(MakeManifest(), dir, new FakeLinuxPlatform());
            Assert.IsTrue(resolver.IsAvailable("wit"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public void IsAvailable_MissingTool_False()
    {
        var dir = CreateFakeToolsDir();
        try
        {
            var resolver = new ManifestToolResolver(MakeManifest(), dir, new FakeLinuxPlatform());
            Assert.IsFalse(resolver.IsAvailable("wit"));
        }
        finally { Directory.Delete(dir, true); }
    }
}

// ---------------------------------------------------------------------------
// NoOpToolRunner tests
// ---------------------------------------------------------------------------

[TestClass]
public class NoOpToolRunnerTests
{
    [TestMethod]
    [TestCategory("Tooling")]
    public void CanRun_AlwaysFalse()
    {
        Assert.IsFalse(NoOpToolRunner.Instance.CanRun("any_tool"));
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public async Task RunAsync_ReturnsNonZeroExitCode()
    {
        var result = await NoOpToolRunner.Instance.RunAsync("any_tool", "");
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    [TestCategory("Tooling")]
    public async Task RunAsync_ErrorMessageContainsToolName()
    {
        var result = await NoOpToolRunner.Instance.RunAsync("my_tool", "");
        StringAssert.Contains(result.StandardError, "my_tool");
    }
}
