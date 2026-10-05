using System.Collections;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Claudgar.Core.Setup;
using Claudgar.Preview;

internal static class Program
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            // A stable 96-DPI baseline. --scale is explicitly a layout simulation, not a monitor DPI change.
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Capture(options.Output, options.Width, options.Height, options.Scale, options.NoGame);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }

    private static void Capture(string output, int width, int height, float scale, bool noGame)
    {
        var assembly = Assembly.Load("Claudgar");
        var coordinatorType = assembly.GetType("Claudgar.App.ApplicationCoordinator", throwOnError: true)!;
        var formType = assembly.GetType("Claudgar.App.Browser.MainForm", throwOnError: true)!;
        // Construction loads settings and embedded resources only. Never call InitializeAsync,
        // InstallComponents, StartAsync, or show the production MainForm in this harness.
        var coordinator = Activator.CreateInstance(coordinatorType)!;
        if (!noGame)
            coordinatorType.GetProperty("Installations")!.SetValue(coordinator,
                new[] { new GameInstallation(@"C:\Preview\World of Warcraft\_classic_beta_", @"C:\Preview\World of Warcraft", "1.60.1.69913") });
        Console.WriteLine("Preparing isolated controls.");
        using var form = (Form)Activator.CreateInstance(formType, coordinator)!;
        var tray = (NotifyIcon)Field(form, "tray");
        var timer = (System.Windows.Forms.Timer)Field(form, "refreshTimer");
        tray.Visible = false;
        try
        {
            form.StartPosition = FormStartPosition.Manual;
            form.AutoScaleMode = AutoScaleMode.None;
            form.ClientSize = new Size(width, height);
            Console.WriteLine("Creating hidden handles.");
            CreateHandles(form);
            Console.WriteLine("Populating fictional character data.");
            if (!noGame) Populate(form, PreviewFixture.CreateSnapshot());
            // Show only a plain host with no startup hooks. The production form remains hidden.
            using var host = new PreviewWindow
            {
                Text = form.Text, Font = form.Font, ForeColor = form.ForeColor, BackColor = form.BackColor,
                Icon = form.Icon, ClientSize = form.ClientSize, ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000),
                AutoScaleMode = AutoScaleMode.None
            };
            host.Controls.Add(form.Controls[0]);
            if (scale != 1)
            {
                host.Scale(new SizeF(scale, scale));
                host.ClientSize = new Size((int)(width * scale), (int)(height * scale));
            }
            host.Show();
            LayoutTree(host);
            Application.DoEvents();
            Console.WriteLine("Verifying data interactions and layout.");
            if (noGame) VerifyGameFolderPrompt(form);
            else Verify(form);
            Console.WriteLine("Rendering bitmap.");
            using var bitmap = new Bitmap(host.Width, host.Height);
            host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            host.Hide();
            AssertNonempty(bitmap, noGame ? 10 : 30);
            var absolutePath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            bitmap.Save(absolutePath, ImageFormat.Png);
            Console.WriteLine($"PASS Preview render: {absolutePath} ({bitmap.Width}x{bitmap.Height}); layout scale {scale:0.##}x; device DPI {form.DeviceDpi}.");
            Console.WriteLine(noGame ? "PASS Single game-folder prompt and hidden browser actions."
                : "PASS Synthetic data navigation, collection search, and main control bounds.");
            Console.WriteLine("Only a plain offscreen preview host was shown. MainForm startup and the local MCP service were never run.");
        }
        finally
        {
            timer.Stop();
            tray.Dispose();
            timer.Dispose();
            ((IAsyncDisposable)coordinator).DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static void Populate(Form form, JsonObject snapshot)
    {
        // Display the data view without pretending a real game installation was found.
        if (form.GetType().GetField("gameFolderPrompt", PrivateInstance)?.GetValue(form) is Control prompt) prompt.Visible = false;
        if (form.GetType().GetField("characterBrowser", PrivateInstance)?.GetValue(form) is Control browserPanel)
        {
            browserPanel.Visible = true;
            browserPanel.BringToFront();
        }
        SetField(form, "characterData", new JsonArray(snapshot.DeepClone()));
        SetField(form, "currentSnapshot", snapshot);
        Invoke(form, "PopulateCharacters");
        var heading = (Control)Field(form, "characterHeading");
        var setCharacter = heading.GetType().GetMethod("SetCharacter");
        if (setCharacter is not null) setCharacter.Invoke(heading, [snapshot]);
        else heading.Text = "Aeloria  ·  Classic Beta PvE\nLevel 22 Human Priest";
        ((Control)Field(form, "connection")).Text = "Local service ready";
        ((Control)Field(form, "exportStatus")).Text = "1 saved character · Updates automatically · Export: ready";
        ((Control)Field(form, "setupDetails")).Text = "SETUP & CONNECTION\n\nPreview with fictional character data.\n\nAll five saved sections are ready to browse.";
        foreach (DictionaryEntry entry in (IDictionary)Field(form, "browsers"))
        {
            var browser = (Control)entry.Value!;
            browser.GetType().GetMethod("SetSection")!.Invoke(browser, [snapshot["sections"]![(string)entry.Key]!.AsObject(), false]);
            // Hidden TreeViews do not reliably dispatch AfterSelect until their native handle exists.
            var tree = (TreeView)Field(browser, "tree");
            typeof(TreeView).GetMethod("OnAfterSelect", PrivateInstance)!.Invoke(tree, [new TreeViewEventArgs(tree.Nodes[0])]);
        }
    }

    private static void Verify(Form form)
    {
        VerifyStreamlinedActions(form);
        foreach (var name in new[] { "characters", "characterHeading", "tabs", "exportStatus" })
        {
            var control = (Control)Field(form, name);
            Assert(control.Width > 100 && control.Height > 16, $"Control {name} is too small: {control.Bounds}.");
            Assert(control.Left >= 0 && control.Top >= 0, $"Control {name} starts outside its parent.");
            Assert(control.Right <= control.Parent!.ClientSize.Width + 1 && control.Bottom <= control.Parent.ClientSize.Height + 1,
                $"Control {name} extends outside its parent: {control.Bounds}, parent {control.Parent.ClientSize}.");
        }
        Assert(((ListBox)Field(form, "characters")).Items.Count == 1, "Synthetic character was not populated.");
        var browsers = (IDictionary)Field(form, "browsers");
        var character = (Control)browsers["character"]!;
        var grid = (DataGridView)Field(character, "grid");
        Assert(grid.RowCount >= 10, "Character fields did not reach the grid.");
        var search = (TextBox)Field(character, "search");
        search.Text = "Stormwind";
        Assert(grid.RowCount == 1, "Collection search did not select the single matching location field.");
        search.Clear();
        var quests = (Control)browsers["quests"]!;
        var tree = (TreeView)Field(quests, "tree");
        tree.SelectedNode = tree.Nodes[0].Nodes[0];
        typeof(TreeView).GetMethod("OnAfterSelect", PrivateInstance)!.Invoke(tree, [new TreeViewEventArgs(tree.SelectedNode)]);
        Assert(((DataGridView)Field(quests, "grid")).RowCount == 2, "Quest collection navigation lost fixture entries.");
    }

    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }

    private sealed class PreviewWindow : Form
    {
        protected override bool ShowWithoutActivation => true;
    }

    private static void VerifyGameFolderPrompt(Form form)
    {
        var prompt = (Control)Field(form, "gameFolderPrompt");
        var button = (Button)prompt.GetType().GetProperty("ChooseFolderButton")!.GetValue(prompt)!;
        Assert(prompt.Bounds == prompt.Parent!.ClientRectangle, "Game-folder prompt does not fill the browser area.");
        Assert(button.Text == "CHOOSE GAME FOLDER" && button.Width > 220 && button.Height > 45,
            "The game-folder action is missing or too small.");
        Assert(form.AcceptButton == button, "Enter does not activate the game-folder action.");
        var issueBar = (Control)Field(form, "setupIssueBar");
        Assert(((TableLayoutPanel)issueBar.Parent!).GetRowHeights()[1] == 0,
            "Issue actions occupy space before a game folder is selected.");
    }

    private static void VerifyStreamlinedActions(Form form)
    {
        var issueBar = (Control)Field(form, "setupIssueBar");
        var setHealth = issueBar.GetType().GetMethod("SetHealth")!;
        Assert(!issueBar.Visible, "Healthy setup should not show corrective actions.");
        var repair = (Button)issueBar.GetType().GetProperty("RepairButton")!.GetValue(issueBar)!;
        var folder = (Button)issueBar.GetType().GetProperty("ChooseFolderButton")!.GetValue(issueBar)!;
        setHealth.Invoke(issueBar, [new SetupHealth(false, true, "The addon needs attention."), false]);
        Assert(issueBar.Visible && repair.Visible && !folder.Visible, "Only repair should be offered for an addon issue.");
        setHealth.Invoke(issueBar, [new SetupHealth(true, false, "The saved game folder is missing."), false]);
        Assert(issueBar.Visible && folder.Visible && !repair.Visible, "Only folder selection should be offered for a missing game.");
        setHealth.Invoke(issueBar, [new SetupHealth(false, false, null), false]);
        Assert(!issueBar.Visible, "Resolved setup should hide corrective actions again.");
        Assert(form.Icon is not null && ((NotifyIcon)Field(form, "tray")).Icon is not null, "Brand icons are missing.");
    }

    private static void LayoutTree(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) LayoutTree(child);
        control.PerformLayout();
    }

    private static void AssertNonempty(Bitmap bitmap, int minimumColors)
    {
        var colors = new HashSet<int>();
        for (var y = 20; y < bitmap.Height; y += 19)
            for (var x = 20; x < bitmap.Width; x += 19)
                colors.Add(bitmap.GetPixel(x, y).ToArgb());
        Assert(colors.Count > minimumColors, $"Render appears empty: only {colors.Count} sampled colors.");
    }

    private static object Field(object target, string name) => target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, null);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static (string Output, int Width, int Height, float Scale, bool NoGame) ParseArguments(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Usage: Claudgar.Preview <output.png> [--width 1440] [--height 960] [--scale 2] [--no-game]");
        var width = 1440;
        var height = 960;
        var scale = 1f;
        var noGame = false;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--no-game") { noGame = true; continue; }
            if (i + 1 == args.Length) throw new ArgumentException("Every size option needs a value.");
            switch (args[i])
            {
                case "--width": width = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                case "--height": height = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                case "--scale": scale = float.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                default: throw new ArgumentException($"Unknown option: {args[i]}");
            }
            i++;
        }
        if (width < 950 || height < 620 || width > 3840 || height > 2160 || scale is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(args), "Use widths 950–3840, heights 620–2160, and scales 1–2.");
        return (args[0], width, height, scale, noGame);
    }
}
