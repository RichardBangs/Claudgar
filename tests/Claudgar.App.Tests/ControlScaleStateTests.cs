using System.Drawing;
using System.Windows.Forms;
using Claudgar.App.Browser;

internal static class ControlScaleStateTests
{
    public static void Run()
    {
        ExplicitScalingIsReversible();
        NativeTransitionsAreConsumedOnce();
        NativeAndExplicitScalingRemainIndependent();
        Console.WriteLine("PASS Explicit control scaling, native DPI transitions, and size/location independence.");
    }

    private static void ExplicitScalingIsReversible()
    {
        var state = new ControlScaleState();
        Equal(state.Factor(96), 1, "Baseline geometry must match 96 DPI.");
        state.Apply(new SizeF(2, 2), BoundsSpecified.Size, 96);
        Equal(state.Factor(96), 2, "Explicit doubled control bounds need doubled custom geometry.");
        state.Apply(new SizeF(.5f, .5f), BoundsSpecified.Size, 96);
        Equal(state.Factor(96), 1, "Reversing explicit scaling must restore baseline geometry.");
        state.Apply(new SizeF(3, 3), BoundsSpecified.Location, 96);
        Equal(state.Factor(96), 1, "Moving a control must not change its custom drawing size.");
    }

    private static void NativeTransitionsAreConsumedOnce()
    {
        var state = new ControlScaleState();
        var previousDpi = 96;
        foreach (var dpi in new[] { 144, 192, 96 })
        {
            var expected = dpi / 96f;
            Equal(state.Factor(dpi), expected, "A new native DPI must apply before child scaling.");
            // Repeated layout reads must neither multiply the new native scale nor consume its size transition.
            Equal(state.Factor(dpi), expected, "Repeated layout reads must preserve native geometry.");
            var factor = dpi / (float)previousDpi;
            state.Apply(new SizeF(factor, factor), BoundsSpecified.Width, dpi);
            Equal(state.Factor(dpi), expected, "Native width scaling must not double-count device DPI.");
            state.Apply(new SizeF(factor, factor), BoundsSpecified.Height, dpi);
            Equal(state.Factor(dpi), expected, "Native height scaling must consume its axis only once.");
            state.DpiChanged(dpi);
            Equal(state.Factor(dpi), expected, "Completing the parent DPI change must keep the native baseline.");
            previousDpi = dpi;
        }
    }

    private static void NativeAndExplicitScalingRemainIndependent()
    {
        var state = new ControlScaleState();
        // WinForms can update DeviceDpi before custom geometry has been queried for the new monitor.
        state.Apply(new SizeF(2, 2), BoundsSpecified.Size, 192);
        Equal(state.Factor(192), 2, "A native scale factor must be consumed even before the first geometry query.");
        state.DpiChanged(192);
        state.Apply(new SizeF(2, 2), BoundsSpecified.Size, 192);
        Equal(state.Factor(192), 4, "Explicit scaling at stable 192 DPI must remain independent of native scale.");
        state.Apply(new SizeF(.5f, .5f), BoundsSpecified.Size, 192);
        Equal(state.Factor(192), 2, "Undoing explicit zoom must return to the current native scale.");
        state.Apply(new SizeF(2, 2), BoundsSpecified.Size, 192);
        Equal(state.Factor(192), 4, "A second explicit scale must work after returning to native geometry.");
        Equal(state.Factor(144), 1.5f, "Moving to a new monitor must reset the previous explicit zoom baseline.");
        state.Apply(new SizeF(.75f, .75f), BoundsSpecified.Size, 144);
        Equal(state.Factor(144), 1.5f, "A downward native DPI transition must not shrink geometry twice.");
        state.DpiChanged(144);
        state.Apply(new SizeF(2, 2), BoundsSpecified.Size, 144);
        Equal(state.Factor(144), 3, "A completed native transition must not consume a later explicit scale call.");
        state.Apply(new SizeF(4, 4), BoundsSpecified.Location, 144);
        Equal(state.Factor(144), 3, "Location-only scaling must leave explicit and native geometry unchanged.");
    }

    private static void Equal(float actual, float expected, string message)
    {
        if (Math.Abs(actual - expected) > .0001f)
            throw new InvalidOperationException($"{message} Expected {expected}; actual {actual}.");
    }
}
