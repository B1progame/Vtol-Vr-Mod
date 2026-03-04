using System.Buffers.Binary;
using SkiaSharp;
using Svg.Skia;

if (args.Length < 1)
{
    PrintUsage();
    return 1;
}

var mode = args[0].ToLowerInvariant();
return mode switch
{
    "render" => RunRender(args),
    "pack" => RunPack(args),
    _ => Fail($"Unknown mode: {mode}")
};

static int RunRender(string[] args)
{
    if (args.Length < 4)
    {
        return Fail("Usage: IconBuilder render <input.svg> <outputDir> <sizesCsv>");
    }

    var svgPath = Path.GetFullPath(args[1]);
    var outputDir = Path.GetFullPath(args[2]);
    var sizes = args[3]
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(static s => int.Parse(s))
        .Distinct()
        .OrderBy(static s => s)
        .ToArray();

    if (!File.Exists(svgPath))
    {
        return Fail($"SVG not found: {svgPath}");
    }

    if (sizes.Length == 0)
    {
        return Fail("No sizes provided.");
    }

    Directory.CreateDirectory(outputDir);

    var svg = new SKSvg();
    var picture = svg.Load(svgPath);
    if (picture is null)
    {
        return Fail($"Failed to load SVG: {svgPath}");
    }

    var bounds = picture.CullRect;
    if (bounds.Width <= 0 || bounds.Height <= 0)
    {
        return Fail("SVG has invalid drawable bounds.");
    }

    foreach (var size in sizes)
    {
        if (size <= 0 || size > 256)
        {
            return Fail($"Invalid icon size: {size}");
        }

        var outputPngPath = Path.Combine(outputDir, $"AppIcon_{size}.png");
        RenderFrame(picture, bounds, size, outputPngPath);
    }

    Console.WriteLine($"Rendered {sizes.Length} PNG frame(s) to {outputDir}");
    return 0;
}

static void RenderFrame(SKPicture picture, SKRect bounds, int size, string outputPngPath)
{
    var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);

    var scale = Math.Min(size / bounds.Width, size / bounds.Height);
    var tx = ((size - (bounds.Width * scale)) / 2f) - (bounds.Left * scale);
    var ty = ((size - (bounds.Height * scale)) / 2f) - (bounds.Top * scale);

    // Small frames benefit from pixel-aligned translation to avoid blur.
    if (size <= 32)
    {
        tx = MathF.Round(tx);
        ty = MathF.Round(ty);
    }

    canvas.Translate(tx, ty);
    canvas.Scale(scale);
    canvas.DrawPicture(picture);
    canvas.Flush();

    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
    File.WriteAllBytes(outputPngPath, data.ToArray());
}

static int RunPack(string[] args)
{
    if (args.Length < 3)
    {
        return Fail("Usage: IconBuilder pack <output.ico> <input1.png> [input2.png] ...");
    }

    var outputIcoPath = Path.GetFullPath(args[1]);
    var inputPngPaths = args.Skip(2).Select(Path.GetFullPath).ToArray();
    var frames = new List<IconFrame>(inputPngPaths.Length);

    foreach (var pngPath in inputPngPaths)
    {
        if (!File.Exists(pngPath))
        {
            return Fail($"PNG not found: {pngPath}");
        }

        var data = File.ReadAllBytes(pngPath);
        if (!IsPng(data))
        {
            return Fail($"Not a valid PNG: {pngPath}");
        }

        var (width, height) = ReadPngDimensions(data);
        frames.Add(new IconFrame(width, height, data));
    }

    frames = frames
        .OrderBy(static f => f.Width)
        .ThenBy(static f => f.Height)
        .ToList();

    var outputDir = Path.GetDirectoryName(outputIcoPath);
    if (!string.IsNullOrWhiteSpace(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }

    using var fs = new FileStream(outputIcoPath, FileMode.Create, FileAccess.Write, FileShare.None);
    using var bw = new BinaryWriter(fs);

    bw.Write((ushort)0); // ICONDIR reserved
    bw.Write((ushort)1); // ICONDIR type=icon
    bw.Write((ushort)frames.Count);

    var dataOffset = 6 + (frames.Count * 16);
    foreach (var frame in frames)
    {
        bw.Write(ToIconDimensionByte(frame.Width));
        bw.Write(ToIconDimensionByte(frame.Height));
        bw.Write((byte)0); // color count
        bw.Write((byte)0); // reserved
        bw.Write((ushort)1); // planes
        bw.Write((ushort)32); // bpp
        bw.Write(frame.Data.Length);
        bw.Write(dataOffset);
        dataOffset += frame.Data.Length;
    }

    foreach (var frame in frames)
    {
        bw.Write(frame.Data);
    }

    Console.WriteLine($"Wrote {outputIcoPath} with {frames.Count} frame(s).");
    return 0;
}

static bool IsPng(byte[] data)
{
    ReadOnlySpan<byte> sig = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
    return data.Length >= 24 && data.AsSpan(0, 8).SequenceEqual(sig);
}

static (int width, int height) ReadPngDimensions(byte[] data)
{
    var width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
    var height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));
    return (width, height);
}

static byte ToIconDimensionByte(int dimension)
{
    if (dimension <= 0 || dimension > 256)
    {
        throw new InvalidOperationException($"Invalid icon frame dimension: {dimension}.");
    }

    return dimension == 256 ? (byte)0 : (byte)dimension;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  IconBuilder render <input.svg> <outputDir> <sizesCsv>");
    Console.WriteLine("  IconBuilder pack <output.ico> <input1.png> [input2.png] ...");
}

file sealed record IconFrame(int Width, int Height, byte[] Data);
