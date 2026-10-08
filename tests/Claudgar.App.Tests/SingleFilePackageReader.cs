using System.IO.Compression;
using System.Text;

/// <summary>Reads one assembly from the .NET v6 bundle manifest without executing the package.</summary>
internal static class SingleFilePackageReader
{
    // The .NET apphost bundle marker identifies the preceding Int64 manifest offset.
    // Layout: dotnet/runtime v10.0.5, Microsoft.NET.HostModel/Bundle/Manifest.cs and FileEntry.cs.
    private static readonly byte[] Signature =
    [0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38, 0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
     0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18, 0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae];
    private const long MaximumAssemblyBytes = 256L * 1024 * 1024;

    public static byte[] ReadAssembly(string packagePath, string assemblyName) => ReadFile(packagePath, assemblyName, requiredFileType: 1);

    public static byte[] ReadFile(string packagePath, string fileName, byte? requiredFileType = null)
    {
        using var input = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        // The self-contained singlefilehost includes hostfxr/hostpolicy and is larger than apphost.
        var prefix = new byte[(int)Math.Min(input.Length, 16 * 1024 * 1024)];
        input.ReadExactly(prefix);
        var marker = prefix.AsSpan().IndexOf(Signature);
        if (marker < sizeof(long)) throw new IOException("The executable is not a .NET single-file package.");
        var headerOffset = BitConverter.ToInt64(prefix, marker - sizeof(long));
        if (headerOffset <= marker + Signature.Length || headerOffset >= input.Length) throw new IOException("The bundle manifest offset is invalid.");
        input.Position = headerOffset;
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        var major = reader.ReadUInt32();
        var minor = reader.ReadUInt32();
        var count = reader.ReadInt32();
        if (major != 6 || minor != 0 || count is < 1 or > 4096) throw new IOException("The package bundle format is unsupported.");
        _ = ReadString(reader); // Bundle ID
        _ = reader.ReadInt64(); _ = reader.ReadInt64(); // deps.json location/size
        _ = reader.ReadInt64(); _ = reader.ReadInt64(); // runtimeconfig.json location/size
        _ = reader.ReadUInt64(); // extraction flags
        for (var index = 0; index < count; index++)
        {
            var offset = reader.ReadInt64();
            var size = reader.ReadInt64();
            var compressedSize = reader.ReadInt64();
            var type = reader.ReadByte();
            var name = ReadString(reader);
            if (name != fileName) continue;
            var storedSize = compressedSize == 0 ? size : compressedSize;
            if ((requiredFileType is not null && type != requiredFileType) || size <= 0 || size > MaximumAssemblyBytes || storedSize <= 0 || storedSize > MaximumAssemblyBytes ||
                offset <= 0 || offset + storedSize > headerOffset)
                throw new IOException("The bundled app assembly metadata is invalid.");
            input.Position = offset;
            var stored = new byte[(int)storedSize];
            input.ReadExactly(stored);
            if (compressedSize == 0) return stored;
            using var compressed = new MemoryStream(stored, writable: false);
            using var decompression = new DeflateStream(compressed, CompressionMode.Decompress);
            using var output = new MemoryStream((int)size);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = decompression.Read(buffer)) > 0)
            {
                if (output.Length + read > size) throw new IOException("The app assembly expands beyond its manifest size.");
                output.Write(buffer, 0, read);
            }
            if (output.Length != size) throw new IOException("The bundled app assembly is incomplete.");
            return output.ToArray();
        }
        throw new IOException("The main app assembly is not present in the package.");
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.Read7BitEncodedInt();
        if (length is < 0 or > 1024) throw new IOException("The bundle entry name is invalid.");
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException("The bundle entry is truncated.");
        return Encoding.UTF8.GetString(bytes);
    }
}
