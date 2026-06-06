using Microsoft.VisualStudio.TestTools.UnitTesting;
using UWUVCI.Core.Pipeline;
using UWUVCI.Core.Tooling;
using UWUVCI.Services;

namespace UWUVCI.Tests;

// ---------------------------------------------------------------------------
// Minimal fakes for unit-testing services without real tool binaries
// ---------------------------------------------------------------------------

file sealed class NativePlatform : IPlatformInfo
{
    public bool    IsNativeWindows => false;
    public bool    IsLinux         => true;
    public bool    IsWineLike      => false;
    public string? WineFlavor      => null;
    public string  ToWindowsPath(string p) => p;
    public string  ToHostPath(string p)    => p;
}

/// <summary>
/// Records all RunAsync calls and returns a configurable result.
/// An optional <see cref="OnRun"/> action can create side-effect files the real tool would produce.
/// </summary>
file sealed class RecordingRunner : IToolRunner
{
    public List<(string tool, string args, string? cwd)> Calls { get; } = new();
    public ToolResult NextResult { get; set; } = new ToolResult { ExitCode = 0, StandardOutput = "", StandardError = "" };
    /// <summary>Optional side-effect action called after each RunAsync invocation.</summary>
    public Action<string, string, string?>? OnRun { get; set; }

    public bool CanRun(string toolName) => true;

    public Task<ToolResult> RunAsync(string toolName, string arguments, string? workingDirectory = null, CancellationToken ct = default)
    {
        Calls.Add((toolName, arguments, workingDirectory));
        OnRun?.Invoke(toolName, arguments, workingDirectory);
        return Task.FromResult(NextResult);
    }
}

// ---------------------------------------------------------------------------
// WineFence tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class WineFenceTests
{
    [TestMethod]
    public void WaitForVisibility_ExistingFile_ReturnsTrue()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "data");
            Assert.IsTrue(WineFence.WaitForVisibility(tmp));
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void WaitForVisibility_MissingFile_ReturnsFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        Assert.IsFalse(WineFence.WaitForVisibility(path, timeoutMs: 200));
    }

    [TestMethod]
    public void WaitForVisibility_ExistingDirectory_ReturnsTrue()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        try
        {
            Assert.IsTrue(WineFence.WaitForVisibility(tmp, isDirectory: true));
        }
        finally { Directory.Delete(tmp); }
    }

    [TestMethod]
    public void WaitForStableSize_ExistingFile_Completes()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tmp, new byte[1024]);
            // Should complete quickly without throwing
            WineFence.WaitForStableSize(tmp, timeoutMs: 2000);
            Assert.IsTrue(true);
        }
        finally { File.Delete(tmp); }
    }
}

// ---------------------------------------------------------------------------
// IOHelpers tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class IOHelpersTests
{
    [TestMethod]
    public void MoveOrCopyDirectory_MovesFiles()
    {
        var src = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dst = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "test.txt"), "hello");
        try
        {
            IOHelpers.MoveOrCopyDirectory(src, dst);
            Assert.IsTrue(File.Exists(Path.Combine(dst, "test.txt")));
            Assert.IsFalse(Directory.Exists(src));
        }
        finally
        {
            if (Directory.Exists(src)) Directory.Delete(src, true);
            if (Directory.Exists(dst)) Directory.Delete(dst, true);
        }
    }

    [TestMethod]
    public void MoveOrCopyDirectory_OverwritesExistingDest()
    {
        var src = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dst = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(src, "new.txt"), "new");
        File.WriteAllText(Path.Combine(dst, "old.txt"), "old");
        try
        {
            IOHelpers.MoveOrCopyDirectory(src, dst);
            Assert.IsTrue(File.Exists(Path.Combine(dst, "new.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(dst, "old.txt")));
        }
        finally
        {
            if (Directory.Exists(src)) Directory.Delete(src, true);
            if (Directory.Exists(dst)) Directory.Delete(dst, true);
        }
    }

    [TestMethod]
    public void MoveOverwrite_ReplacesExistingFile()
    {
        var src = Path.GetTempFileName();
        var dst = Path.GetTempFileName();
        try
        {
            File.WriteAllText(src, "source");
            File.WriteAllText(dst, "original");
            IOHelpers.MoveOverwrite(src, dst);
            Assert.IsFalse(File.Exists(src));
            Assert.AreEqual("source", File.ReadAllText(dst));
        }
        finally
        {
            if (File.Exists(src)) File.Delete(src);
            if (File.Exists(dst)) File.Delete(dst);
        }
    }
}

// ---------------------------------------------------------------------------
// BaseExtractor tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class BaseExtractorTests
{
    [TestMethod]
    public void GetOrExtractBase_MissingZip_Throws()
    {
        var fakeToolsPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(fakeToolsPath);
        try
        {
            Assert.ThrowsExactly<FileNotFoundException>(() =>
                BaseExtractor.GetOrExtractBase(fakeToolsPath));
        }
        finally { Directory.Delete(fakeToolsPath, true); }
    }

    [TestMethod]
    public void GetOrExtractBase_EmptyToolsPath_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            BaseExtractor.GetOrExtractBase(""));
    }

    [TestMethod]
    public void GetOrExtractBase_NullToolsPath_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            BaseExtractor.GetOrExtractBase(null!));
    }
}

// ---------------------------------------------------------------------------
// WiiPatchService tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class WiiPatchServiceTests
{
    [DataTestMethod]
    [DataRow(true,  false, (byte)0x01)] // US
    [DataRow(false, true,  (byte)0x00)] // JP
    [DataRow(false, false, (byte)0x02)] // PAL
    public void ApplyRegionFrii_WritesCorrectByte(bool us, bool jp, byte expected)
    {
        // Create a temp file large enough to hold the patched offsets (0x4E010 + 16 = 0x4E020 = 319520 bytes)
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tmp, new byte[0x4E020 + 1]);
            WiiPatchService.ApplyRegionFrii(tmp, us, jp);

            using var fs = new FileStream(tmp, FileMode.Open, FileAccess.Read);
            fs.Seek(0x4E003, SeekOrigin.Begin);
            var actual = (byte)fs.ReadByte();
            Assert.AreEqual(expected, actual);
        }
        finally { File.Delete(tmp); }
    }
}

// ---------------------------------------------------------------------------
// NKitService argument-building tests (recording runner)
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class NKitServiceTests
{
    [TestMethod]
    public async Task ConvertToIsoAsync_InvokesCorrectTool()
    {
        var tmp     = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var source  = Path.Combine(tmp, "game.nkit.iso");
        var outName = "out.iso";
        File.WriteAllBytes(source, new byte[16]);
        try
        {
            var runner = new RecordingRunner();
            // The real tool would create the output file; simulate it via OnRun
            runner.OnRun = (_, _, cwd) => File.WriteAllBytes(Path.Combine(cwd ?? tmp, outName), new byte[16]);
            await NKitService.ConvertToIsoAsync(tmp, source, outName, runner);
            Assert.AreEqual(1, runner.Calls.Count);
            Assert.AreEqual("ConvertToIso", runner.Calls[0].tool);
        }
        finally { Directory.Delete(tmp, true); }
    }

    [TestMethod]
    public async Task ConvertToNKitAsync_InvokesCorrectTool()
    {
        var tmp     = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var source  = Path.Combine(tmp, "game.iso");
        var outName = "out.nkit.iso";
        File.WriteAllBytes(source, new byte[16]);
        try
        {
            var runner = new RecordingRunner();
            runner.OnRun = (_, _, cwd) => File.WriteAllBytes(Path.Combine(cwd ?? tmp, outName), new byte[16]);
            await NKitService.ConvertToNKitAsync(tmp, source, outName, runner);
            Assert.AreEqual(1, runner.Calls.Count);
            Assert.AreEqual("ConvertToNKit", runner.Calls[0].tool);
        }
        finally { Directory.Delete(tmp, true); }
    }

    [TestMethod]
    public async Task ConvertToIsoAsync_FailedTool_Throws()
    {
        var tmp    = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var source = Path.Combine(tmp, "game.nkit.iso");
        File.WriteAllBytes(source, new byte[16]);
        try
        {
            var runner = new RecordingRunner { NextResult = new ToolResult { ExitCode = 1, StandardError = "tool error" } };
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                NKitService.ConvertToIsoAsync(tmp, source, "out.iso", runner));
        }
        finally { Directory.Delete(tmp, true); }
    }
}

// ---------------------------------------------------------------------------
// WitTicketExtractionService tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class WitTicketExtractionServiceTests
{
    [TestMethod]
    public async Task ExtractTicketsAsync_InvokesWitWithCorrectArgs()
    {
        var tmp    = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tikTmd = Path.Combine(tmp, "TIKTMD");
        var iso    = Path.Combine(tmp, "game.iso");
        Directory.CreateDirectory(tmp);
        File.WriteAllBytes(iso, new byte[32]);
        try
        {
            var runner   = new RecordingRunner();
            // Simulate wit creating tmd.bin + ticket.bin after running
            runner.OnRun = (_, _, _) =>
            {
                Directory.CreateDirectory(tikTmd);
                File.WriteAllBytes(Path.Combine(tikTmd, "tmd.bin"),    new byte[512]);
                File.WriteAllBytes(Path.Combine(tikTmd, "ticket.bin"), new byte[512]);
            };
            var platform = new NativePlatform();
            await WitTicketExtractionService.ExtractTicketsAsync(
                "tools", iso, tikTmd, platform, runner);
            Assert.AreEqual(1, runner.Calls.Count);
            Assert.AreEqual("wit", runner.Calls[0].tool);
            StringAssert.Contains(runner.Calls[0].args, "--psel data");
            StringAssert.Contains(runner.Calls[0].args, "+tmd.bin");
            StringAssert.Contains(runner.Calls[0].args, "+ticket.bin");
        }
        finally { Directory.Delete(tmp, true); }
    }

    [TestMethod]
    public async Task ExtractTicketsAsync_ToolFailure_Throws()
    {
        var tmp     = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var iso     = Path.Combine(tmp, "game.iso");
        File.WriteAllBytes(iso, new byte[32]);
        try
        {
            var runner   = new RecordingRunner { NextResult = new ToolResult { ExitCode = 1, StandardError = "fail" } };
            var platform = new NativePlatform();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                WitTicketExtractionService.ExtractTicketsAsync("tools", iso, Path.Combine(tmp, "tiktmd"), platform, runner));
        }
        finally { Directory.Delete(tmp, true); }
    }
}

// ---------------------------------------------------------------------------
// GCNInjectService unit-level tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class GCNInjectServiceTests
{
    [TestMethod]
    public void PrepareTempBase_MissingZip_Throws()
    {
        var tools = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var temp  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(temp);
        try
        {
            Assert.ThrowsExactly<FileNotFoundException>(() =>
                GCNInjectService.PrepareTempBase(tools, temp));
        }
        finally
        {
            Directory.Delete(tools, true);
            Directory.Delete(temp,  true);
        }
    }

    [TestMethod]
    public void ApplyNintendontDol_CopiesDolToTempBase()
    {
        var tools    = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sysDir   = Path.Combine(tempBase, "sys");
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(sysDir);
        File.WriteAllBytes(Path.Combine(tools, "nintendont.dol"), new byte[32]);
        try
        {
            GCNInjectService.ApplyNintendontDol(tools, tempBase, force43: false);
            Assert.IsTrue(File.Exists(Path.Combine(sysDir, "main.dol")));
        }
        finally
        {
            Directory.Delete(tools,    true);
            Directory.Delete(tempBase, true);
        }
    }

    [TestMethod]
    public void ApplyNintendontDol_Force43_CopiesForce43Dol()
    {
        var tools    = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sysDir   = Path.Combine(tempBase, "sys");
        Directory.CreateDirectory(tools);
        Directory.CreateDirectory(sysDir);
        File.WriteAllBytes(Path.Combine(tools, "nintendont_force.dol"), [0xDE, 0xAD, 0xBE, 0xEF]);
        try
        {
            GCNInjectService.ApplyNintendontDol(tools, tempBase, force43: true);
            var content = File.ReadAllBytes(Path.Combine(sysDir, "main.dol"));
            CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, content);
        }
        finally
        {
            Directory.Delete(tools,    true);
            Directory.Delete(tempBase, true);
        }
    }
}

// ---------------------------------------------------------------------------
// WiiInjectService unit-level tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class WiiInjectServiceTests
{
    [TestMethod]
    public void UpdateMetaReservedFlag_MissingIso_Throws()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        try
        {
            Assert.ThrowsExactly<FileNotFoundException>(() =>
                WiiInjectService.UpdateMetaReservedFlag(tmp, Path.Combine(tmp, "missing.iso")));
        }
        finally { Directory.Delete(tmp, true); }
    }

    [TestMethod]
    public void UpdateMetaReservedFlag_PatchesMetaXml()
    {
        var tmp     = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var metaDir = Path.Combine(tmp, "meta");
        Directory.CreateDirectory(metaDir);

        var isoPath = Path.Combine(tmp, "game.iso");
        // First 4 bytes = 'G', 'A', 'L', 'E' → GameID GALE
        File.WriteAllBytes(isoPath, [0x47, 0x41, 0x4C, 0x45, 0x00, 0x00, 0x00, 0x00]);

        var metaXml = Path.Combine(metaDir, "meta.xml");
        File.WriteAllText(metaXml,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><menu><reserved_flag2>00000000</reserved_flag2></menu>");
        try
        {
            WiiInjectService.UpdateMetaReservedFlag(tmp, isoPath);
            var doc  = new System.Xml.XmlDocument();
            doc.Load(metaXml);
            var node = doc.SelectSingleNode("menu/reserved_flag2")!;
            // 47414c45 = hex of GALE
            Assert.AreEqual("47414c45", node.InnerText);
        }
        finally { Directory.Delete(tmp, true); }
    }

    [TestMethod]
    public async Task PreparePreIsoAsync_IsoFile_ReturnsSourceDirectly()
    {
        var tmp      = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var iso      = Path.Combine(tmp, "game.iso");
        File.WriteAllBytes(iso, new byte[32]);
        try
        {
            var runner  = new RecordingRunner();
            var opt     = new WiiInjectOptions { ForceNkitConvert = false };
            var (path, direct, _) = await WiiInjectService.PreparePreIsoAsync(
                "tools", tmp, iso, opt, new NativePlatform(), runner, default);
            Assert.AreEqual(iso, path);
            Assert.IsTrue(direct, "Plain ISO should be used directly");
            Assert.AreEqual(0, runner.Calls.Count, "wit should not be called for plain ISO");
        }
        finally { Directory.Delete(tmp, true); }
    }

    [TestMethod]
    public async Task PreparePreIsoAsync_WbfsFile_CallsWit()
    {
        var tmp   = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmp);
        var wbfs  = Path.Combine(tmp, "game.wbfs");
        var preIso = Path.Combine(tmp, "pre.iso");
        File.WriteAllBytes(wbfs,   new byte[32]);
        File.WriteAllBytes(preIso, new byte[32]); // pre-create so WineFence sees it
        try
        {
            var runner  = new RecordingRunner();
            var opt     = new WiiInjectOptions { ForceNkitConvert = false };
            var (path, direct, _) = await WiiInjectService.PreparePreIsoAsync(
                "tools", tmp, wbfs, opt, new NativePlatform(), runner, default);
            Assert.IsFalse(direct, "WBFS should not be used directly");
            Assert.AreEqual(1, runner.Calls.Count);
            Assert.AreEqual("wit", runner.Calls[0].tool);
        }
        finally { Directory.Delete(tmp, true); }
    }
}

// ---------------------------------------------------------------------------
// N64InjectService tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class N64InjectServiceTests
{
    [TestMethod]
    public void PatchFrameLayout_WideScreen_WritesBytesAtExpectedOffset()
    {
        // Minimal FrameLayout.arc stub: SARC + FLYT header + one pic1 pane "frame"
        var arc = BuildMinimalArc(paneName: "frame", wide: false);
        var tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, arc);
        try
        {
            N64InjectService.PatchFrameLayout(tmp, wideScreen: true, removeDarkFilter: false);
            var patched = File.ReadAllBytes(tmp);
            // widescreen bytes [0x44, 0xF0, 0x00, 0x00] at pane+0x4C
            int paneOff = FindPaneOffset(patched);
            Assert.AreEqual(0x44, patched[paneOff + 0x4C]);
            Assert.AreEqual(0xF0, patched[paneOff + 0x4D]);
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public void PatchFrameLayout_DarkFilter_WritesByte0AtPaneMaskOffset()
    {
        var arc = BuildMinimalArc(paneName: "frame_mask", wide: false);
        var tmp = Path.GetTempFileName();
        File.WriteAllBytes(tmp, arc);
        try
        {
            N64InjectService.PatchFrameLayout(tmp, wideScreen: false, removeDarkFilter: true);
            var patched = File.ReadAllBytes(tmp);
            int paneOff = FindPaneOffset(patched);
            Assert.AreEqual(0x00, patched[paneOff + 0x08]);
        }
        finally { File.Delete(tmp); }
    }

    [TestMethod]
    public async Task InjectAsync_CopiesToMainRom_AndInstallsBlankIni()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var romDir    = Path.Combine(tmp, "base", "content", "rom");
        var cfgDir    = Path.Combine(tmp, "base", "content", "config");
        var toolsDir  = Path.Combine(tmp, "tools");
        Directory.CreateDirectory(romDir);
        Directory.CreateDirectory(cfgDir);
        Directory.CreateDirectory(toolsDir);
        var mainRom   = Path.Combine(romDir, "game.z64");
        var blankIni  = Path.Combine(toolsDir, "blank.ini");
        var injectRom = Path.Combine(tmp, "inject.z64");
        File.WriteAllBytes(mainRom,   [0xAA]);
        File.WriteAllBytes(blankIni,  [0xBB]);
        File.WriteAllBytes(injectRom, [0xCC]);
        try
        {
            var runner = new RecordingRunner();
            await N64InjectService.InjectAsync(
                toolsDir, Path.Combine(tmp, "base"), injectRom,
                new N64InjectOptions());
            // N64Converter is now natively reimplemented; no external tool call expected.
            Assert.AreEqual(0, runner.Calls.Count);
            var iniDest = Path.Combine(cfgDir, "game.z64.ini");
            Assert.IsTrue(File.Exists(iniDest), "blank.ini should be installed");
        }
        finally { Directory.Delete(tmp, true); }
    }

    // ---- helpers -----------------------------------------------------------

    /// <summary>
    /// Builds a minimal SARC arc stub that contains a single FLYT pane.
    /// The arc ends exactly at paneStart + paneSize so the walker exits via ReadAtLeast returning 0.
    /// </summary>
    private static byte[] BuildMinimalArc(string paneName, bool wide)
    {
        uint dataOff   = 0x40;
        uint paneStart = dataOff + 8;  // FLYT header (8 bytes) then first pane
        uint paneSize  = 0x100;        // large enough for all fields; arcSize = paneStart + paneSize
        int  arcSize   = (int)(paneStart + paneSize);

        var b = new byte[arcSize];

        // SARC magic at 0x00
        b[0] = (byte)'S'; b[1] = (byte)'A'; b[2] = (byte)'R'; b[3] = (byte)'C';
        // dataOffset at 0x0C
        b[0x0C] = (byte)(dataOff >> 24); b[0x0D] = (byte)(dataOff >> 16);
        b[0x0E] = (byte)(dataOff >>  8); b[0x0F] = (byte)(dataOff);
        // 0x38 = 0 (no extra SARC offset)

        // FLYT magic at dataOff
        b[dataOff + 0] = (byte)'F'; b[dataOff + 1] = (byte)'L';
        b[dataOff + 2] = (byte)'Y'; b[dataOff + 3] = (byte)'T';
        // sub-offset: low 2 bytes of [dataOff+4] = 8 → panes start at dataOff+8
        b[dataOff + 7] = 8;

        // pic1 pane at paneStart
        b[paneStart + 0] = (byte)'p'; b[paneStart + 1] = (byte)'i';
        b[paneStart + 2] = (byte)'c'; b[paneStart + 3] = (byte)'1';
        b[paneStart + 4] = (byte)(paneSize >> 24); b[paneStart + 5] = (byte)(paneSize >> 16);
        b[paneStart + 6] = (byte)(paneSize >>  8); b[paneStart + 7] = (byte)(paneSize);

        // Pane name at paneStart + 0x0C
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(paneName + "\0");
        Array.Copy(nameBytes, 0, b, (int)paneStart + 0x0C, Math.Min(nameBytes.Length, 0x18));

        return b;
    }

    private static int FindPaneOffset(byte[] b)
    {
        uint dataOff = (uint)(b[0x0C] << 24 | b[0x0D] << 16 | b[0x0E] << 8 | b[0x0F]);
        // sub-offset: low 2 bytes of the 4-byte field at dataOff+4
        uint subOff  = (uint)(b[dataOff + 6] << 8 | b[dataOff + 7]);
        return (int)(dataOff + subOff);
    }
}

// ---------------------------------------------------------------------------
// MsxInjectService tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class MsxInjectServiceTests
{
    [TestMethod]
    public async Task InjectAsync_PrependsHeaderAndAppendRom()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var pkgDir = Path.Combine(tmp, "base", "content", "msx");
        Directory.CreateDirectory(pkgDir);
        var romPath = Path.Combine(tmp, "game.rom");

        // pkg: exactly 10 header bytes (no extra payload)
        var headerBytes = Enumerable.Repeat((byte)0xAA, 10).ToArray();
        var pkgPath     = Path.Combine(pkgDir, "msx.pkg");
        File.WriteAllBytes(pkgPath, headerBytes);

        // ROM to inject
        File.WriteAllBytes(romPath, [0xBB, 0xCC, 0xDD]);

        try
        {
            await MsxInjectService.InjectAsync(Path.Combine(tmp, "base"), romPath);
            var result = File.ReadAllBytes(pkgPath);
            Assert.AreEqual(13, result.Length, "header(10) + ROM(3)");
            Assert.IsTrue(result[..10].SequenceEqual(headerBytes), "header must be preserved");
            Assert.AreEqual(0xBB, result[10]);
            Assert.AreEqual(0xCC, result[11]);
            Assert.AreEqual(0xDD, result[12]);
        }
        finally { Directory.Delete(tmp, true); }
    }
}

// ---------------------------------------------------------------------------
// Tg16InjectService tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class Tg16InjectServiceTests
{
    [TestMethod]
    public async Task InjectAsync_Rom_ThrowsPlatformNotSupportedException()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempDir = Path.Combine(tmp, "temp");
        var baseDir = Path.Combine(tmp, "base");
        var pceEmuDir = Path.Combine(baseDir, "content", "pceemu");
        var toolsDir  = Path.Combine(tmp, "tools");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(pceEmuDir);
        Directory.CreateDirectory(toolsDir);

        var rom = Path.Combine(tmp, "game.pce");
        File.WriteAllBytes(rom, [0xAA]);

        var runner = new RecordingRunner();

        bool threw = false;
        try
        {
            await Tg16InjectService.InjectAsync(toolsDir, tempDir, baseDir, rom, runner);
        }
        catch (PlatformNotSupportedException)
        {
            threw = true;
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }

        // BuildPcePkg is now a PlatformNotSupportedException stub (native pce.pkg
        // format not yet implemented); no runner calls should be made.
        Assert.IsTrue(threw, "Expected PlatformNotSupportedException from BuildPcePkg stub");
        Assert.AreEqual(0, runner.Calls.Count);
    }
}

// ---------------------------------------------------------------------------
// NdsInjectService tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class NdsInjectServiceTests
{
    [TestMethod]
    public async Task InjectAsync_ReplacesRomInsideZip()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dir0010 = Path.Combine(tmp, "base", "content", "0010");
        Directory.CreateDirectory(dir0010);
        var tempDir = Path.Combine(tmp, "temp");
        Directory.CreateDirectory(tempDir);
        var toolsDir = Path.Combine(tmp, "tools");
        Directory.CreateDirectory(toolsDir);

        // Create a rom.zip with a WUP entry
        var romZip = Path.Combine(dir0010, "rom.zip");
        using (var archive = System.IO.Compression.ZipFile.Open(romZip, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("WUP-game.nds");
            using var s = entry.Open();
            s.WriteByte(0xFF);
        }

        var injectRom = Path.Combine(tmp, "inject.nds");
        File.WriteAllBytes(injectRom, [0xAA, 0xBB]);

        try
        {
            await NdsInjectService.InjectAsync(
                toolsDir, tempDir, Path.Combine(tmp, "base"), injectRom,
                new NdsInjectOptions(), new RecordingRunner());

            // Verify new zip contains our ROM
            using var check = System.IO.Compression.ZipFile.OpenRead(romZip);
            Assert.AreEqual(1, check.Entries.Count);
            Assert.IsTrue(check.Entries[0].Name.Contains("WUP"));
        }
        finally { Directory.Delete(tmp, true); }
    }
}

// ---------------------------------------------------------------------------
// NesSnesInjectService tests
// ---------------------------------------------------------------------------

[TestClass]
[TestCategory("Services")]
public class NesSnesInjectServiceTests
{
    // ---- RPX fixture builder -----------------------------------------------
    // Creates a minimal valid RPX (ELF type 0xFE01, big-endian PowerPC).
    // The payload is embedded in a SHT_PROGBITS data section (not zlib-compressed).
    // WiiURpxService can decompress / compress this file without modification.
    private static byte[] BuildMinimalRpx(byte[] payload)
    {
        const int shentsize   = 40;
        const int shnum       = 4;          // null(0) + crcs(1) + fileinfo(2) + data(3)
        const int shoff       = 64;         // section header table right after ELF header
        const int sectionAlign = 0x40;

        static int AlignUp(int v, int a) => (v + a - 1) & ~(a - 1);

        int shdrDataStart  = shoff + shnum * shentsize; // 64 + 160 = 224

        int crcOff  = shdrDataStart;        // 224
        int crcSize = shnum * 4;            // 16 bytes (4 × uint32)

        int fiOff  = AlignUp(crcOff  + crcSize,   sectionAlign); // 256
        int fiSize = 32;

        int dataOff  = AlignUp(fiOff + fiSize, sectionAlign);   // 320
        int dataSize = payload.Length;

        int totalSize = AlignUp(dataOff + dataSize, sectionAlign);
        var buf = new byte[totalSize];

        // ---- ELF header (bytes 0–63) ----------------------------------------
        buf[0]=0x7F; buf[1]=0x45; buf[2]=0x4C; buf[3]=0x46; // magic
        buf[4]=1;    // ELFCLASS32
        buf[5]=2;    // ELFDATA2MSB
        buf[6]=1;    // version
        buf[16]=0xFE; buf[17]=0x01;  // e_type = RPX
        buf[18]=0x00; buf[19]=0x14;  // e_machine = PowerPC
        BEW32(buf, 20, 1);           // e_version
        BEW32(buf, 32, (uint)shoff); // e_shoff
        BEW16(buf, 40, 64);          // e_ehsize
        BEW16(buf, 46, shentsize);   // e_shentsize
        BEW16(buf, 48, shnum);       // e_shnum

        // ---- Section 1: CRC section -----------------------------------------
        int s1 = shoff + 1 * shentsize;
        BEW32(buf, s1 +  4, 0x80000003u);   // sh_type = SHT_RPL_CRCS
        BEW32(buf, s1 + 16, (uint)crcOff);
        BEW32(buf, s1 + 20, (uint)crcSize);

        // ---- Section 2: FILEINFO section ------------------------------------
        int s2 = shoff + 2 * shentsize;
        BEW32(buf, s2 +  4, 0x80000004u);   // sh_type = SHT_RPL_FILEINFO
        BEW32(buf, s2 + 16, (uint)fiOff);
        BEW32(buf, s2 + 20, (uint)fiSize);

        // ---- Section 3: DATA section ----------------------------------------
        int s3 = shoff + 3 * shentsize;
        BEW32(buf, s3 +  4, 0x00000001u);   // sh_type = SHT_PROGBITS
        BEW32(buf, s3 +  8, 0x00000006u);   // sh_flags = SHF_ALLOC | SHF_EXECINSTR
        BEW32(buf, s3 + 16, (uint)dataOff);
        BEW32(buf, s3 + 20, (uint)dataSize);

        // ---- Payload --------------------------------------------------------
        Array.Copy(payload, 0, buf, dataOff, payload.Length);

        // ---- CRCs (CRC section CRC is always 0 by convention) ---------------
        BEW32(buf, crcOff + 0,  0);
        BEW32(buf, crcOff + 4,  0);
        BEW32(buf, crcOff + 8,  Crc32(buf, fiOff,   fiSize));
        BEW32(buf, crcOff + 12, Crc32(buf, dataOff, dataSize));

        return buf;
    }

    private static void BEW32(byte[] d, int o, uint v)
    {
        d[o]=   (byte)(v>>24); d[o+1]=(byte)(v>>16);
        d[o+2]= (byte)(v>> 8); d[o+3]=(byte)v;
    }
    private static void BEW16(byte[] d, int o, int v) { d[o]=(byte)(v>>8); d[o+1]=(byte)v; }

    private static uint Crc32(byte[] d, int off, int len)
    {
        uint crc = ~0u;
        for (int i = 0; i < len; i++)
            crc = (crc >> 8) ^ Crc32Tab[(crc ^ d[off + i]) & 0xFF];
        return ~crc;
    }
    private static readonly uint[] Crc32Tab = BuildCrc32Tab();
    private static uint[] BuildCrc32Tab()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    // ---- Tests -------------------------------------------------------------

    [TestMethod]
    public async Task InjectNes_DecompressesReInjectsRecompresses()
    {
        var tmp     = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var codeDir = Path.Combine(tmp, "base", "code");
        Directory.CreateDirectory(codeDir);
        Directory.CreateDirectory(Path.Combine(tmp, "tools"));

        // Build RPX payload: iNES stub (1 PRG bank = 16 KiB slot = 16400 bytes total)
        const int prgBanks = 1;
        const int slotSize = 16 + prgBanks * 16384; // 16400
        var payload = new byte[slotSize];
        payload[0]=0x4E; payload[1]=0x45; payload[2]=0x53; payload[3]=0x1A; // iNES magic
        payload[4]=(byte)prgBanks;
        Array.Fill(payload, (byte)0xFF, 16, slotSize - 16);   // fill ROM slot

        var rpx = Path.Combine(codeDir, "game.rpx");
        File.WriteAllBytes(rpx, BuildMinimalRpx(payload));

        // Valid 1-PRG-bank NES ROM (fits in the slot)
        var nesRom = new byte[slotSize];
        nesRom[0]=0x4E; nesRom[1]=0x45; nesRom[2]=0x53; nesRom[3]=0x1A;
        nesRom[4]=(byte)prgBanks;
        var romPath = Path.Combine(tmp, "game.nes");
        File.WriteAllBytes(romPath, nesRom);

        var runner = new RecordingRunner();
        try
        {
            await NesSnesInjectService.InjectAsync(
                Path.Combine(tmp, "tools"),
                Path.Combine(tmp, "base"),
                romPath,
                new NesSnesInjectOptions { IsNes = true },
                runner);

            // No wiiurpxtool runner calls — RPX processing is now native
            Assert.AreEqual(0, runner.Calls.Count,
                "Expected zero external tool calls; wiiurpxtool is now native.");
        }
        finally { Directory.Delete(tmp, true); }
    }

    [TestMethod]
    public async Task InjectNes_TooBigRom_ThrowsInvalidOperationException()
    {
        var tmp     = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var codeDir = Path.Combine(tmp, "base", "code");
        Directory.CreateDirectory(codeDir);
        Directory.CreateDirectory(Path.Combine(tmp, "tools"));

        // RPX payload: iNES stub with 0 PRG banks → slot size = 16 bytes (header only)
        var payload = new byte[16]; // iNES header only, prgBanks=0
        payload[0]=0x4E; payload[1]=0x45; payload[2]=0x53; payload[3]=0x1A;

        File.WriteAllBytes(Path.Combine(codeDir, "game.rpx"), BuildMinimalRpx(payload));

        // ROM is 32 bytes — larger than the 16-byte slot → must throw
        var romPath = Path.Combine(tmp, "game.nes");
        File.WriteAllBytes(romPath, new byte[32]);

        var runner = new RecordingRunner();
        bool threw = false;
        try
        {
            await NesSnesInjectService.InjectAsync(
                Path.Combine(tmp, "tools"),
                Path.Combine(tmp, "base"),
                romPath,
                new NesSnesInjectOptions { IsNes = true },
                runner);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }

        Assert.IsTrue(threw, "Expected InvalidOperationException for too-large ROM.");
    }
}
