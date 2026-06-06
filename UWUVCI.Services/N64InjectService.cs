using System.Text;

namespace UWUVCI.Services;

/// <summary>Options for an N64 injection.</summary>
public sealed class N64InjectOptions
{
    /// <summary>Path to a custom INI file. Null = use blank.ini from tools.</summary>
    public string?   IniPath    { get; set; }

    /// <summary>Raw INI bytes (takes precedence over <see cref="IniPath"/> when set).</summary>
    public byte[]?   IniBin     { get; set; }

    /// <summary>Apply widescreen patch to FrameLayout.arc.</summary>
    public bool      WideScreen { get; set; }

    /// <summary>Remove the dark-filter overlay from FrameLayout.arc.</summary>
    public bool      DarkFilter { get; set; }

    public bool      Debug      { get; set; }
}

/// <summary>
/// Injects an N64 ROM into a Wii U Virtual Console base.
/// The ROM is natively converted to z64 (big-endian) format if required.
/// Optionally patches FrameLayout.arc for widescreen / dark-filter removal,
/// and installs an INI configuration file alongside the ROM.
/// </summary>
public static class N64InjectService
{
    public static Task InjectAsync(
        string        toolsPath,
        string        baseRomPath,
        string        romPath,
        N64InjectOptions opt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opt);

        cancellationToken.ThrowIfCancellationRequested();

        // Locate the main ROM slot inside the base (first file in content/rom/)
        var romDir      = Path.Combine(baseRomPath, "content", "rom");
        var mainRomPath = Directory.GetFiles(romDir).FirstOrDefault()
                          ?? throw new FileNotFoundException("No ROM slot found in content/rom.", romDir);

        var mainIni = Path.Combine(baseRomPath, "content", "config",
                                   Path.GetFileName(mainRomPath) + ".ini");

        // 1) Convert ROM to z64 format and copy to the ROM slot (native replacement for N64Converter.exe)
        N64RomConverter.ConvertAndCopy(romPath, mainRomPath);

        // 2) FrameLayout.arc patches (widescreen / dark-filter)
        if (opt.WideScreen || opt.DarkFilter)
        {
            var arcPath = Path.Combine(baseRomPath, "content", "FrameLayout.arc");
            if (File.Exists(arcPath))
                PatchFrameLayout(arcPath, opt.WideScreen, opt.DarkFilter);
        }

        // 3) INI
        Directory.CreateDirectory(Path.GetDirectoryName(mainIni)!);
        if (opt.IniBin is { Length: > 0 })
        {
            File.WriteAllBytes(mainIni, opt.IniBin);
        }
        else if (!string.IsNullOrWhiteSpace(opt.IniPath) && File.Exists(opt.IniPath))
        {
            File.Copy(opt.IniPath, mainIni, overwrite: true);
        }
        else
        {
            var blankIni = Path.Combine(toolsPath, "blank.ini");
            if (File.Exists(blankIni))
                File.Copy(blankIni, mainIni, overwrite: true);
            else if (File.Exists(mainIni))
                File.Delete(mainIni); // use whatever the base ships with
        }

        return Task.CompletedTask;
    }

    // ---- FrameLayout.arc patching (SARC + FLYT binary format) ---------------

    internal static void PatchFrameLayout(string arcPath, bool wideScreen, bool removeDarkFilter)
    {
        byte darkFilterByte = (byte)(removeDarkFilter ? 0 : 1);
        byte[] wsBytes      = wideScreen
            ? [0x44, 0xF0, 0x00, 0x00]   // float 1920 in big-endian
            : [0x44, 0xB4, 0x00, 0x00];  // float 1440 in big-endian

        using var fs = File.Open(arcPath, FileMode.Open, FileAccess.ReadWrite);

        // Read SARC header magic
        var hdr = new byte[4];
        fs.ReadExactly(hdr);
        if (!IsMarker(hdr, 'S', 'A', 'R', 'C')) return;

        // data offset from 0x0C (big-endian uint32)
        fs.Position = 0x0C;
        uint offset = ReadBEUInt32(fs);

        // second offset at 0x38
        fs.Position = 0x38;
        offset += ReadBEUInt32(fs);

        // Expect FLYT at that offset
        fs.Position = offset;
        fs.ReadExactly(hdr);
        if (!IsMarker(hdr, 'F', 'L', 'Y', 'T')) return;

        // Skip the section-relative sub-offset (4 bytes) – clear high 2 bytes
        fs.Position = offset + 0x04;
        var subOff = new byte[4];
        fs.ReadExactly(subOff);
        subOff[0] = 0;
        subOff[1] = 0;
        offset += (uint)(subOff[0] << 24 | subOff[1] << 16 | subOff[2] << 8 | subOff[3]);

        fs.Position = offset;
        var nameBuf = new byte[0x18];

        while (true)
        {
            long entryStart = fs.Position;
            var tag     = new byte[4];
            var sizeB   = new byte[4];
            if (fs.ReadAtLeast(tag, 4, throwOnEndOfStream: false) < 4) break;
            if (fs.ReadAtLeast(sizeB, 4, throwOnEndOfStream: false) < 4) break;
            uint size = (uint)(sizeB[0] << 24 | sizeB[1] << 16 | sizeB[2] << 8 | sizeB[3]);

            if (!IsMarker(tag, 'p', 'i', 'c', '1'))
            {
                if (offset + size >= (uint)fs.Length) break;
                offset += size;
                fs.Position = offset;
                continue;
            }

            // Read name (0x18 bytes at offset+0x0C)
            fs.Position = entryStart + 0x0C;
            fs.ReadExactly(nameBuf);
            int nul  = Array.IndexOf(nameBuf, (byte)0);
            var name = Encoding.ASCII.GetString(nameBuf, 0, nul < 0 ? nameBuf.Length : nul);

            if (name == "frame")
            {
                byte[] zero4 = BitConverter.GetBytes(0f);
                byte[] one4  = BitConverter.GetBytes(1f);

                fs.Position = entryStart + 0x2C; fs.Write(zero4);
                fs.Position = entryStart + 0x30; fs.Write(zero4);
                fs.Position = entryStart + 0x44; fs.Write(one4);
                fs.Position = entryStart + 0x48; fs.Write(one4);
                fs.Position = entryStart + 0x4C; fs.Write(wsBytes);
            }
            else if (name == "frame_mask")
            {
                fs.Position = entryStart + 0x08;
                fs.WriteByte(darkFilterByte);
            }
            else if (name == "power_save_bg")
            {
                break; // done
            }

            offset += size;
            fs.Position = offset;
        }
    }

    private static bool IsMarker(byte[] b, char a, char c1, char c2, char c3) =>
        b[0] == (byte)a && b[1] == (byte)c1 && b[2] == (byte)c2 && b[3] == (byte)c3;

    private static uint ReadBEUInt32(Stream s)
    {
        var b = new byte[4];
        s.ReadExactly(b);
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }
}
