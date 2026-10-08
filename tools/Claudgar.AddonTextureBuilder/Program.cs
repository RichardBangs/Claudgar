using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: Claudgar.AddonTextureBuilder <portrait.png> <portrait.tga> [small-preview.png]");
    return 1;
}

try
{
    const int size = 64; // Power-of-two, uncompressed BGRA texture supported by the game client.
    using var source = new Bitmap(Path.GetFullPath(args[0]));
    using var texture = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    var transparent = 0;
    for (var y = 0; y < size; y++)
    for (var x = 0; x < size; x++)
    {
        // Sample pixel centers without filtering so the established pixel art stays crisp.
        var pixel = source.GetPixel((2 * x + 1) * source.Width / (2 * size), (2 * y + 1) * source.Height / (2 * size));
        texture.SetPixel(x, y, pixel);
        if (pixel.A == 0) transparent++;
    }
    if (transparent == 0) throw new InvalidOperationException("The portrait lost its transparent background.");

    var output = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    using (var writer = new BinaryWriter(File.Create(output)))
    {
        writer.Write((byte)0); // No image ID.
        writer.Write((byte)0); // No color map.
        writer.Write((byte)2); // Uncompressed true-color TGA.
        writer.Write(new byte[9]); // Empty color-map specification and X/Y origins.
        writer.Write((ushort)size);
        writer.Write((ushort)size);
        writer.Write((byte)32);
        writer.Write((byte)8); // Eight alpha bits; bottom-left origin.
        for (var y = size - 1; y >= 0; y--)
        for (var x = 0; x < size; x++)
        {
            var pixel = texture.GetPixel(x, y);
            writer.Write(pixel.B);
            writer.Write(pixel.G);
            writer.Write(pixel.R);
            writer.Write(pixel.A);
        }
    }

    // Verify every packaged byte against the source sample, including row order and transparency.
    var data = File.ReadAllBytes(output);
    if (data.Length != 18 + size * size * 4 || data[2] != 2 || data[16] != 32 || data[17] != 8)
        throw new InvalidOperationException("The packaged TGA has an invalid header or size.");
    for (var y = 0; y < size; y++)
    for (var x = 0; x < size; x++)
    {
        var pixel = texture.GetPixel(x, y);
        var offset = 18 + ((size - 1 - y) * size + x) * 4;
        if (data[offset] != pixel.B || data[offset + 1] != pixel.G || data[offset + 2] != pixel.R || data[offset + 3] != pixel.A)
            throw new InvalidOperationException("A packaged pixel differs from the portrait.");
    }

    if (args.Length == 3)
    {
        // Show the game button's actual 20px artwork beside an enlarged nearest-neighbor view.
        using var small = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(texture, new Rectangle(0, 0, 20, 20));
        }
        using var preview = new Bitmap(260, 220, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(preview))
        {
            graphics.Clear(Color.FromArgb(25, 30, 38));
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(small, new Rectangle(10, 100, 20, 20));
            graphics.DrawImage(small, new Rectangle(50, 10, 200, 200));
        }
        var previewPath = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
        preview.Save(previewPath, ImageFormat.Png);
        Console.WriteLine("20px preview: " + previewPath);
    }
    Console.WriteLine($"Verified {size}x{size} BGRA TGA; {transparent} transparent pixels: {output}");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("Addon texture packaging failed: " + error.Message);
    return 1;
}
