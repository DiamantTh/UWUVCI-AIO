using System.Text;

namespace UWUVCI.Services;

/// <summary>
/// Builds Wii U Virtual Console pce.pkg files for Turbo CD / PC Engine games.
/// Based on BuildTurboCdPcePkg reference implementation.
/// </summary>
internal static class TurboGrafx16PkgBuilder
{
    /// <summary>
    /// Build a pce.pkg file from a directory containing .hcd, .ogg, and .bin files.
    /// </summary>
    /// <param name="inputDir">Directory containing game files (.hcd, .ogg, .bin).</param>
    /// <param name="outputPath">Output path for pce.pkg file.</param>
    public static void BuildPcePkg(string inputDir, string outputPath)
    {
        if (!Directory.Exists(inputDir))
            throw new DirectoryNotFoundException($"Input directory not found: {inputDir}");

        var hcdFiles = Directory.GetFiles(inputDir, "*.hcd");
        var oggFiles = Directory.GetFiles(inputDir, "*.ogg");
        var binFiles = Directory.GetFiles(inputDir, "*.bin");

        if (hcdFiles.Length == 0)
            throw new FileNotFoundException("No .hcd file found in input directory.");
        if (oggFiles.Length == 0)
            throw new FileNotFoundException("No .ogg files found in input directory.");
        if (binFiles.Length == 0)
            throw new FileNotFoundException("No .bin files found in input directory.");
        if (hcdFiles.Length > 1)
            throw new InvalidOperationException("Multiple .hcd files found; exactly one is required.");

        var toWrite = new List<byte>();

        // 1. Add pceconfig.bin header
        var hcdFilePath = hcdFiles[0];
        var hcdFileName = Path.GetFileName(hcdFilePath);
        var pceConfigData = BuildPceConfigData(hcdFileName);
        toWrite.AddRange(pceConfigData);

        // 2. Add HCD file
        var hcdData = File.ReadAllBytes(hcdFilePath);
        toWrite.AddRange(BuildFileData(hcdData, hcdFileName));

        // 3. Parse HCD and add referenced files
        var hcdText = File.ReadAllText(hcdFilePath);
        var lines = hcdText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                var fileName = parts[2].Trim();
                var filePath = Path.Combine(inputDir, fileName);

                if (File.Exists(filePath))
                {
                    var fileData = File.ReadAllBytes(filePath);
                    toWrite.AddRange(BuildFileData(fileData, fileName));
                }
            }
        }

        // 4. Write pce.pkg
        var packageData = toWrite.ToArray();
        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            // Write total package size (including this 4-byte header) in reversed byte order
            var sizeBytes = GetLengthAsByteArrayInReverseOrder(packageData.Length);
            fs.Write(sizeBytes, 0, 4);
            fs.Write(packageData, 0, packageData.Length);
        }
    }

    private static List<byte> BuildPceConfigData(string hcdFileName)
    {
        var result = new List<byte>();

        // Config header: size (160 bytes) in reversed byte order
        result.AddRange(new byte[] { 160, 0, 0, 0 });

        // "pceconfig.bin" with null terminator
        var pceConfigNameBytes = Encoding.ASCII.GetBytes("pceconfig.bin\0");
        result.AddRange(pceConfigNameBytes);

        // Standard pceconfig content (from Lords of Thunder reference)
        result.AddRange(new byte[]
        {
            1, 0, 0, 128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        });

        // Add HCD filename (64 bytes, null-padded)
        var hcdNameBytes = Encoding.ASCII.GetBytes(hcdFileName);
        result.AddRange(hcdNameBytes);
        result.AddRange(Enumerable.Repeat((byte)0, 64 - hcdNameBytes.Length));

        // Add HCD filename again (64 bytes, null-padded)
        result.AddRange(hcdNameBytes);
        result.AddRange(Enumerable.Repeat((byte)0, 64 - hcdNameBytes.Length));

        return result;
    }

    private static List<byte> BuildFileData(byte[] data, string fileName)
    {
        var result = new List<byte>();

        // File size in reversed byte order
        var sizeBytes = GetLengthAsByteArrayInReverseOrder(data.Length);
        result.AddRange(sizeBytes);

        // File name with null terminator
        var fileNameBytes = Encoding.ASCII.GetBytes(fileName);
        result.AddRange(fileNameBytes);
        result.Add(0);

        // File data
        result.AddRange(data);

        return result;
    }

    /// <summary>Convert integer to 4 big-endian bytes in reversed order (little-endian representation of big-endian value).</summary>
    private static byte[] GetLengthAsByteArrayInReverseOrder(int length)
    {
        var bytes = new byte[4];
        bytes[3] = (byte)(length >> 24);
        bytes[2] = (byte)(length >> 16);
        bytes[1] = (byte)(length >> 8);
        bytes[0] = (byte)length;
        return bytes;
    }
}
