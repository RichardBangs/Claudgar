namespace Claudgar.App.Browser;

/// <summary>Keeps custom geometry in step with explicit Control.Scale and native monitor DPI changes.</summary>
internal sealed class ControlScaleState
{
    private int nativeDpi = 96;
    private SizeF explicitScale = new(1, 1);
    private BoundsSpecified pendingNativeAxes;

    public float Factor(int deviceDpi)
    {
        ObserveDpi(deviceDpi);
        return nativeDpi / 96f * Math.Min(explicitScale.Width, explicitScale.Height);
    }

    public void Apply(SizeF factor, BoundsSpecified specified, int deviceDpi)
    {
        ObserveDpi(deviceDpi);
        var width = explicitScale.Width;
        var height = explicitScale.Height;
        if ((specified & BoundsSpecified.Width) != 0)
        {
            if ((pendingNativeAxes & BoundsSpecified.Width) == 0 && Valid(factor.Width)) width *= factor.Width;
            pendingNativeAxes &= ~BoundsSpecified.Width;
        }
        if ((specified & BoundsSpecified.Height) != 0)
        {
            if ((pendingNativeAxes & BoundsSpecified.Height) == 0 && Valid(factor.Height)) height *= factor.Height;
            pendingNativeAxes &= ~BoundsSpecified.Height;
        }
        explicitScale = new SizeF(width, height);
    }

    public void DpiChanged(int deviceDpi)
    {
        nativeDpi = Math.Max(1, deviceDpi);
        explicitScale = new SizeF(1, 1);
        pendingNativeAxes = BoundsSpecified.None;
    }

    private void ObserveDpi(int deviceDpi)
    {
        deviceDpi = Math.Max(1, deviceDpi);
        if (deviceDpi == nativeDpi) return;
        nativeDpi = deviceDpi;
        explicitScale = new SizeF(1, 1);
        // WinForms updates DeviceDpi before scaling children. That first size factor
        // is already represented by the new DPI and must not also become an explicit zoom.
        pendingNativeAxes = BoundsSpecified.Size;
    }

    private static bool Valid(float factor) => float.IsFinite(factor) && factor > 0;
}
