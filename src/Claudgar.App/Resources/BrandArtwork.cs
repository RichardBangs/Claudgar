using System.Reflection;

namespace Claudgar.App.Resources;

/// <summary>Owns the portrait and Windows icons loaded from the portable application's resources.</summary>
internal sealed class BrandArtwork : IDisposable
{
    public Image Portrait { get; }
    public Icon WindowIcon { get; }
    public Icon TrayIcon { get; }

    public BrandArtwork()
    {
        using var portraitStream = Open("claudgar-portrait.png");
        using var source = Image.FromStream(portraitStream);
        Portrait = new Bitmap(source);
        using var windowStream = Open("claudgar.ico");
        WindowIcon = new Icon(windowStream, new Size(32, 32));
        using var trayStream = Open("claudgar.ico");
        TrayIcon = new Icon(trayStream, SystemInformation.SmallIconSize);
    }

    private static Stream Open(string filename) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream("Artwork/" + filename)
        ?? throw new InvalidOperationException("The packaged brand artwork is missing: " + filename);

    public void Dispose()
    {
        Portrait.Dispose();
        WindowIcon.Dispose();
        TrayIcon.Dispose();
    }
}
