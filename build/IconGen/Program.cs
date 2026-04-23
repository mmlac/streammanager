using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace StreamManager.Build.IconGen;

/// <summary>
/// Converts a source PNG into multi-size .ico (Windows) and .icns (macOS)
/// icon containers. Both formats can embed PNG data directly, so we resize
/// with ImageSharp and hand-roll the container headers — no extra native
/// dependencies, runs anywhere .NET does.
/// </summary>
internal static class Program
{
    // Standard Windows icon sizes. ICO entries with size 256 are written as 0
    // in the header (the byte field is only 8 bits).
    private static readonly int[] IcoSizes = { 16, 32, 48, 64, 128, 256 };

    // macOS .icns OSTypes keyed to the resolution the PNG frame represents.
    // iconutil produces these codes; Finder + Dock understand them.
    private static readonly (string Type, int Size)[] IcnsEntries =
    {
        ("icp4", 16),   // 16x16
        ("icp5", 32),   // 32x32
        ("ic07", 128),  // 128x128
        ("ic08", 256),  // 256x256
        ("ic09", 512),  // 512x512
        ("ic10", 1024), // 512@2x
        ("ic11", 32),   // 16@2x
        ("ic12", 64),   // 32@2x
        ("ic13", 256),  // 128@2x
        ("ic14", 512),  // 256@2x
    };

    public static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: IconGen <source.png> <out.ico> <out.icns>");
            return 1;
        }

        var source = args[0];
        var icoOut = args[1];
        var icnsOut = args[2];

        if (!File.Exists(source))
        {
            Console.Error.WriteLine($"Source PNG not found: {source}");
            return 2;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(icoOut))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(icnsOut))!);

        using var src = Image.Load<Rgba32>(source);

        WriteIco(src, icoOut);
        WriteIcns(src, icnsOut);

        Console.WriteLine($"Wrote {icoOut}");
        Console.WriteLine($"Wrote {icnsOut}");
        return 0;
    }

    private static byte[] EncodePng(Image<Rgba32> src, int size)
    {
        using var resized = src.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(size, size),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3,
        }));
        using var ms = new MemoryStream();
        resized.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static void WriteIco(Image<Rgba32> src, string path)
    {
        // Encode each size as PNG, then assemble the ICO container.
        var pngs = IcoSizes.Select(s => (Size: s, Data: EncodePng(src, s))).ToArray();

        using var fs = File.Create(path);
        // ICONDIR: Reserved(2) | Type(2)=1 | Count(2)
        Span<byte> hdr = stackalloc byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(hdr[0..2], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(hdr[2..4], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(hdr[4..6], (ushort)pngs.Length);
        fs.Write(hdr);

        // ICONDIRENTRY is 16 bytes per entry; image data follows the table.
        var dataOffset = 6 + (16 * pngs.Length);
        Span<byte> entry = stackalloc byte[16];
        foreach (var (size, data) in pngs)
        {
            entry.Clear();
            entry[0] = (byte)(size >= 256 ? 0 : size); // width
            entry[1] = (byte)(size >= 256 ? 0 : size); // height
            entry[2] = 0;                              // color count (0 = >= 8bpp)
            entry[3] = 0;                              // reserved
            BinaryPrimitives.WriteUInt16LittleEndian(entry[4..6], 1);   // planes
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..8], 32);  // bit depth
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..12], (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..16], (uint)dataOffset);
            fs.Write(entry);
            dataOffset += data.Length;
        }

        foreach (var (_, data) in pngs)
        {
            fs.Write(data);
        }
    }

    private static void WriteIcns(Image<Rgba32> src, string path)
    {
        // Encode each configured size once and cache — several OSTypes reuse the
        // same pixel dimensions (e.g. ic08 and ic13 are both 256×256).
        var cache = new Dictionary<int, byte[]>();
        byte[] GetPng(int size) =>
            cache.TryGetValue(size, out var existing)
                ? existing
                : (cache[size] = EncodePng(src, size));

        using var ms = new MemoryStream();
        // Magic + placeholder for total file size (big-endian)
        ms.Write(new[] { (byte)'i', (byte)'c', (byte)'n', (byte)'s' });
        Span<byte> sizeSpan = stackalloc byte[4];
        ms.Write(sizeSpan); // placeholder, filled in below

        Span<byte> blockSize = stackalloc byte[4];
        foreach (var (type, size) in IcnsEntries)
        {
            var data = GetPng(size);
            // type(4) + size(4) + data
            ms.Write(new[] { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] });
            BinaryPrimitives.WriteUInt32BigEndian(blockSize, (uint)(data.Length + 8));
            ms.Write(blockSize);
            ms.Write(data);
        }

        // Backfill total file size at offset 4
        var total = (uint)ms.Length;
        ms.Position = 4;
        BinaryPrimitives.WriteUInt32BigEndian(sizeSpan, total);
        ms.Write(sizeSpan);

        File.WriteAllBytes(path, ms.ToArray());
    }
}
