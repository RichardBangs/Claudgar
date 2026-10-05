using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

try
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine("Usage: Claudgar.IconBuilder <generated-portrait.png> <artwork-directory>");
        return 1;
    }

    var sourcePath = Path.GetFullPath(args[0]);
    var outputDirectory = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(outputDirectory);
    var portraitPath = Path.Combine(outputDirectory, "claudgar-portrait.png");
    if (!string.Equals(sourcePath, portraitPath, StringComparison.OrdinalIgnoreCase))
        File.Copy(sourcePath, portraitPath, overwrite: true);

    using var source = new Bitmap(sourcePath);
    int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
    var entries = sizes.Select(size => CreateEntry(source, size)).ToArray();
    var iconPath = Path.Combine(outputDirectory, "claudgar.ico");
    using (var writer = new BinaryWriter(File.Create(iconPath)))
    {
        writer.Write((ushort)0); // Reserved.
        writer.Write((ushort)1); // ICO format.
        writer.Write((ushort)entries.Length);
        var offset = 6 + 16 * entries.Length;
        foreach (var entry in entries)
        {
            writer.Write((byte)(entry.Size == 256 ? 0 : entry.Size));
            writer.Write((byte)(entry.Size == 256 ? 0 : entry.Size));
            writer.Write((byte)0); // No palette.
            writer.Write((byte)0); // Reserved.
            writer.Write((ushort)1); // Color planes.
            writer.Write((ushort)32); // BGRA pixels, including alpha.
            writer.Write(entry.Png.Length);
            writer.Write(offset);
            offset += entry.Png.Length;
        }
        foreach (var entry in entries)
            writer.Write(entry.Png);
    }

    foreach (var entry in entries)
    {
        var size = entry.Size;
        using var pngStream = new MemoryStream(entry.Png);
        using var bitmap = new Bitmap(pngStream);
        if (bitmap.Width != size || bitmap.Height != size)
            throw new InvalidOperationException($"The {size}px PNG entry has incorrect dimensions.");
        var transparentPixels = 0;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
            if (bitmap.GetPixel(x, y).A == 0)
                transparentPixels++;
        if (transparentPixels == 0)
            throw new InvalidOperationException($"The {size}px icon lost its transparent background.");
        using var icon = new Icon(iconPath, new Size(size, size));
        // System.Drawing can choose a smaller entry for the ICO zero-byte (256px) size.
        // Validate that PNG directly above; verify exact native-size selection below 256px.
        if (size < 256 && (icon.Width != size || icon.Height != size))
            throw new InvalidOperationException($"Windows did not select the {size}px icon entry.");
        Console.WriteLine($"{size}x{size}: PNG and transparency passed; {transparentPixels} transparent pixels; System.Drawing selected {icon.Width}px.");
    }

    Console.WriteLine($"Portrait: {portraitPath}");
    Console.WriteLine($"Icon: {iconPath}");
    return 0;
}
catch (Exception error)
{
    // Asset validation failures should be console diagnostics, never a native crash dialog.
    Console.Error.WriteLine("Icon packaging failed: " + error.Message);
    return 1;
}

static IconEntry CreateEntry(Image source, int size)
{
    using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using (var graphics = Graphics.FromImage(bitmap))
    {
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.Clear(Color.Transparent);
        graphics.DrawImage(source, new Rectangle(0, 0, size, size));
    }
    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    return new IconEntry(size, stream.ToArray());
}

internal sealed record IconEntry(int Size, byte[] Png);
