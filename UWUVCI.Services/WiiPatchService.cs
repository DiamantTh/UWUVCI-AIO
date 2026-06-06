namespace UWUVCI.Services;

/// <summary>
/// Applies low-level binary patches to Wii ISO images and main.dol files.
/// All offsets match the Wii disc format as documented on WiiBrew.
/// </summary>
public static class WiiPatchService
{
    /// <summary>
    /// Patches RegionFrii bytes in the ISO header (offset 0x4E003 / 0x4E010).
    /// Used to force a region-free game to be accepted by a specific region IOS.
    /// </summary>
    public static void ApplyRegionFrii(string isoPath, bool us, bool jp)
    {
        using var fs = new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite);

        fs.Seek(0x4E003, SeekOrigin.Begin);
        if (us)
        {
            fs.WriteByte(0x01);
            fs.Seek(0x4E010, SeekOrigin.Begin);
            fs.Write([0x80,0x06,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80]);
        }
        else if (jp)
        {
            fs.WriteByte(0x00);
            fs.Seek(0x4E010, SeekOrigin.Begin);
            fs.Write([0x00,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80,0x80]);
        }
        else // PAL
        {
            fs.WriteByte(0x02);
            fs.Seek(0x4E010, SeekOrigin.Begin);
            fs.Write([0x80,0x80,0x80,0x00,0x03,0x03,0x04,0x03,0x00,0x80,0x80,0x80,0x80,0x80,0x80,0x80]);
        }
    }

    /// <summary>
    /// Patches the JP-region check in main.dol so the game runs regardless of IOS region.
    /// Offsets: 0x4CBDAC (2 bytes), 0x4CBDAF (1 byte).
    /// </summary>
    public static void ApplyJpPatch(string tempPath)
    {
        var dolPath = Path.Combine(tempPath, "TEMP", "sys", "main.dol");
        using var writer = new BinaryWriter(new FileStream(dolPath, FileMode.Open, FileAccess.Write));
        writer.Seek(0x4CBDAC, SeekOrigin.Begin);
        writer.Write((byte)0x38);
        writer.Write((byte)0x60);
        writer.Seek(0x4CBDAF, SeekOrigin.Begin);
        writer.Write((byte)0x00);
    }
}
