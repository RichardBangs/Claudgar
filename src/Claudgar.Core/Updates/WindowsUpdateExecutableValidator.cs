using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace Claudgar.Core.Updates;

public sealed class WindowsUpdateExecutableValidator : IUpdateExecutableValidator
{
    public void Validate(string path, ReleaseVersion expectedVersion)
    {
        using (var stream = File.OpenRead(path))
        using (var reader = new PEReader(stream))
        {
            if (reader.PEHeaders.CoffHeader.Machine != Machine.Amd64 ||
                reader.PEHeaders.PEHeader?.Magic != PEMagic.PE32Plus ||
                (reader.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0)
                throw new IOException("The downloaded file is not a Windows x64 application.");
        }
        var information = FileVersionInfo.GetVersionInfo(path);
        // Build metadata is allowed in ProductVersion, but release channels remain stable numeric tags.
        var productVersion = information.ProductVersion?.Split('+')[0];
        if (!ReleaseVersion.TryParse(productVersion, out var version) || version != expectedVersion ||
            information.FileMajorPart != version.Major || information.FileMinorPart != version.Minor ||
            information.FileBuildPart != version.Patch || information.FilePrivatePart != 0 ||
            !string.Equals(information.ProductName, "Claudgar", StringComparison.Ordinal))
            throw new IOException("The downloaded app's identity or version does not match the release.");
    }
}
