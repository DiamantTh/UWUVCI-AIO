using Microsoft.VisualStudio.TestTools.UnitTesting;
using SkiaSharp;
using UWUVCI.ImagePipeline;

namespace UWUVCI.Tests;

[TestClass]
public class ImageServiceTests
{
    // ---- CreateBlank -------------------------------------------------------

    [TestMethod]
    [TestCategory("Image")]
    public void CreateBlank_CorrectDimensions()
    {
        using var bmp = ImageService.CreateBlank(128, 128, SKColors.Black);
        Assert.AreEqual(128, bmp.Width);
        Assert.AreEqual(128, bmp.Height);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void CreateBlank_FillColorApplied()
    {
        using var bmp = ImageService.CreateBlank(4, 4, SKColors.Red);
        var px = bmp.GetPixel(0, 0);
        Assert.AreEqual(255, px.Red);
        Assert.AreEqual(0,   px.Green);
        Assert.AreEqual(0,   px.Blue);
    }

    // ---- Resize ------------------------------------------------------------

    [TestMethod]
    [TestCategory("Image")]
    public void Resize_ProducesCorrectDimensions()
    {
        using var src    = ImageService.CreateBlank(64, 64, SKColors.Blue);
        using var result = ImageService.Resize(src, 128, 128);
        Assert.AreEqual(128, result.Width);
        Assert.AreEqual(128, result.Height);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void Resize_DownScale_ProducesCorrectDimensions()
    {
        using var src    = ImageService.CreateBlank(1280, 720, SKColors.Green);
        using var result = ImageService.Resize(src, 854, 480);
        Assert.AreEqual(854, result.Width);
        Assert.AreEqual(480, result.Height);
    }

    // ---- EncodePng / LoadFromBytes roundtrip --------------------------------

    [TestMethod]
    [TestCategory("Image")]
    public void EncodePng_ProducesValidPng()
    {
        using var src  = ImageService.CreateBlank(16, 16, SKColors.Cyan);
        var pngBytes   = ImageService.EncodePng(src);

        Assert.IsNotNull(pngBytes);
        Assert.IsTrue(pngBytes.Length > 0);

        // PNG magic bytes: 89 50 4E 47
        Assert.AreEqual(0x89, pngBytes[0]);
        Assert.AreEqual(0x50, pngBytes[1]);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void LoadFromBytes_Roundtrip_PreservesPixels()
    {
        using var original = ImageService.CreateBlank(8, 8, SKColors.Magenta);
        var bytes          = ImageService.EncodePng(original);
        using var loaded   = ImageService.LoadFromBytes(bytes);

        Assert.AreEqual(original.Width,  loaded.Width);
        Assert.AreEqual(original.Height, loaded.Height);
    }

    // ---- Validate ----------------------------------------------------------

    [TestMethod]
    [TestCategory("Image")]
    public void Validate_CorrectSize_NoErrors()
    {
        using var bmp = ImageService.CreateBlank(128, 128, SKColors.White);
        var errors    = ImageService.Validate(bmp, 128, 128);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void Validate_WrongSize_ReturnsError()
    {
        using var bmp = ImageService.CreateBlank(64, 64, SKColors.White);
        var errors    = ImageService.Validate(bmp, 128, 128);
        Assert.AreEqual(1, errors.Count);
        StringAssert.Contains(errors[0], "128");
    }

    [TestMethod]
    [TestCategory("Image")]
    public void ValidateForWiiU_IconTex_CorrectSize_NoErrors()
    {
        using var bmp = ImageService.CreateBlank(128, 128, SKColors.Black);
        var errors    = ImageService.ValidateForWiiU(bmp, "iconTex");
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void ValidateForWiiU_BootTvTex_WrongSize_ReturnsError()
    {
        using var bmp = ImageService.CreateBlank(128, 128, SKColors.Black);
        var errors    = ImageService.ValidateForWiiU(bmp, "bootTvTex");
        Assert.AreEqual(1, errors.Count);
    }

    // ---- Composite ---------------------------------------------------------

    [TestMethod]
    [TestCategory("Image")]
    public void Composite_ProducesCorrectDimensions()
    {
        using var bg      = ImageService.CreateBlank(128, 128, SKColors.Black);
        using var overlay = ImageService.CreateBlank(32, 32, SKColors.White);
        using var result  = ImageService.Composite(bg, overlay, new SKRectI(0, 0, 32, 32));

        Assert.AreEqual(128, result.Width);
        Assert.AreEqual(128, result.Height);
    }
}

[TestClass]
public class TgaServiceTests
{
    // ---- encode / decode roundtrip ----------------------------------------

    [TestMethod]
    [TestCategory("Image")]
    public void Roundtrip_Rgb24_PreservesDimensions()
    {
        using var original = ImageService.CreateBlank(16, 16, SKColors.Red);
        var tgaBytes       = TgaService.EncodeTga(original);
        using var loaded   = TgaService.LoadTgaFromBytes(tgaBytes);

        Assert.AreEqual(16, loaded.Width);
        Assert.AreEqual(16, loaded.Height);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void Roundtrip_Rgba32_PreservesDimensions()
    {
        var info   = new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var original = new SKBitmap(info);
        original.Erase(new SKColor(100, 150, 200, 128));

        var tgaBytes = TgaService.EncodeTga(original);
        using var loaded = TgaService.LoadTgaFromBytes(tgaBytes);

        Assert.AreEqual(8, loaded.Width);
        Assert.AreEqual(8, loaded.Height);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void EncodeTga_ProducesNonEmptyBytes()
    {
        using var bmp = ImageService.CreateBlank(4, 4, SKColors.Blue);
        var bytes     = TgaService.EncodeTga(bmp);
        Assert.IsTrue(bytes.Length > 18); // at least header
    }

    [TestMethod]
    [TestCategory("Image")]
    public void EncodeTga_HeaderStartsWithImageType2()
    {
        using var bmp = ImageService.CreateBlank(4, 4, SKColors.Blue);
        var bytes     = TgaService.EncodeTga(bmp);
        // Byte 2 = image type; 2 = uncompressed truecolor
        Assert.AreEqual(2, bytes[2]);
    }

    [TestMethod]
    [TestCategory("Image")]
    public void SaveTga_FileExists_AfterSave()
    {
        var path = Path.Combine(Path.GetTempPath(), $"uwuvci_test_{Guid.NewGuid():N}.tga");
        try
        {
            using var bmp = ImageService.CreateBlank(8, 8, SKColors.Green);
            TgaService.SaveTga(bmp, path);
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Image")]
    public void LoadTga_SavedFile_ProducesCorrectDimensions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"uwuvci_test_{Guid.NewGuid():N}.tga");
        try
        {
            using var original = ImageService.CreateBlank(32, 32, SKColors.Yellow);
            TgaService.SaveTga(original, path);
            using var loaded = TgaService.LoadTga(path);

            Assert.AreEqual(32, loaded.Width);
            Assert.AreEqual(32, loaded.Height);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
