using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UWUVCI.Services;

namespace UWUVCI.Tests;

[TestClass]
public class BaseDownloadServiceTests
{
    private string _tempDir = "";
    private string _basesDir = "";

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"uwuvci_test_{Guid.NewGuid()}");
        _basesDir = Path.Combine(_tempDir, "bases");
        Directory.CreateDirectory(_basesDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public void GetStatuses_EmptyDir_ReturnsAllMissing()
    {
        var service = new BaseDownloadService();
        var statuses = service.GetStatuses(_basesDir);

        Assert.AreEqual(3, statuses.Count);
        Assert.IsTrue(statuses.All(s => !s.IsPresent));
        Assert.IsTrue(statuses.Any(s => s.Region == "USA"));
        Assert.IsTrue(statuses.Any(s => s.Region == "EUR"));
        Assert.IsTrue(statuses.Any(s => s.Region == "JPN"));
    }

    [TestMethod]
    public void GetStatuses_WithBase_ReturnsPresent()
    {
        var service = new BaseDownloadService();
        var baseFile = Path.Combine(_basesDir, "WiiU_USA.zip");
        File.WriteAllText(baseFile, "dummy");

        var statuses = service.GetStatuses(_basesDir);
        var usa = statuses.First(s => s.Region == "USA");

        Assert.IsTrue(usa.IsPresent);
        Assert.AreEqual(baseFile, usa.ResolvedPath);
    }

    [TestMethod]
    public void GetStatuses_MixedState_ReturnsCorrect()
    {
        var service = new BaseDownloadService();

        // Create USA base
        File.WriteAllText(Path.Combine(_basesDir, "WiiU_USA.zip"), "usa");

        var statuses = service.GetStatuses(_basesDir);

        Assert.IsTrue(statuses.First(s => s.Region == "USA").IsPresent);
        Assert.IsFalse(statuses.First(s => s.Region == "EUR").IsPresent);
        Assert.IsFalse(statuses.First(s => s.Region == "JPN").IsPresent);
    }

    [TestMethod]
    public void GetStatuses_AllBasesPresent_AllShown()
    {
        var service = new BaseDownloadService();

        // Create all bases
        File.WriteAllText(Path.Combine(_basesDir, "WiiU_USA.zip"), "usa");
        File.WriteAllText(Path.Combine(_basesDir, "WiiU_EUR.zip"), "eur");
        File.WriteAllText(Path.Combine(_basesDir, "WiiU_JPN.zip"), "jpn");

        var statuses = service.GetStatuses(_basesDir);

        Assert.IsTrue(statuses.All(s => s.IsPresent));
        Assert.IsTrue(statuses.All(s => s.StatusText == "✓ Present"));
    }

    [TestMethod]
    public void BaseStatus_Properties_SetCorrectly()
    {
        var status = new BaseDownloadService.BaseStatus
        {
            Name = "Wii U Base (USA)",
            Region = "USA",
            IsPresent = true,
            HasUrl = true,
            HasSha256 = true,
            ResolvedPath = "/path/to/base.zip",
            StatusText = "✓ Present",
        };

        Assert.AreEqual("USA", status.Region);
        Assert.IsTrue(status.IsPresent);
        Assert.AreEqual("✓ Present", status.StatusText);
    }
}
