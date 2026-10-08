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
            Capture(options.Output, options.Width, options.Height, options.Scale, options.NoGame, options.Section);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }

    private static void Capture(string output, int width, int height, float scale, bool noGame, string section)
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
        // Show the control's normal appearance without running setup or changing registration.
        var releaseControls = (Control)Field(form, "releaseControls");
        ((CheckBox)releaseControls.GetType().GetProperty("StartupCheckBox")!.GetValue(releaseControls)!).Enabled = true;
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
            if (!noGame)
            {
                var previewSnapshot = PreviewFixture.CreateSnapshot();
                previewSnapshot["sections"]!["talents"] = PreviewFixture.CreateGroupedTalentSection();
                if (section == "equipment")
                {
                    previewSnapshot["character"]!["level"] = 60;
                    previewSnapshot["sections"]!["character"]!["data"]!["level"] = 60;
                    previewSnapshot["sections"]!["character"]!["data"]!["stats"] = PreviewFixture.CreateCharacterStats();
                }
                Populate(form, previewSnapshot);
            }
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
            Console.WriteLine("Verifying assistant home, data interactions, and layout.");
            VerifyMinimalShell(host, form, releaseControls);
            if (noGame) VerifyGameFolderPrompt(form);
            else Verify(form, host);
            if (!noGame)
            {
                if (section == "home") Invoke(form, "ShowAssistantHome");
                else
                {
                    Invoke(form, "ShowDataView");
                    var tabs = Field(form, "tabs");
                    var sectionIndex = Array.IndexOf(new[] { "character", "quests", "talents", "inventory", "equipment" }, section);
                    tabs.GetType().GetProperty("SelectedIndex")!.SetValue(tabs, sectionIndex);
                }
                LayoutTree(host);
                Application.DoEvents();
            }
            Console.WriteLine("Rendering bitmap.");
            using var bitmap = new Bitmap(host.Width, host.Height);
            host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            AssertNonempty(bitmap, noGame ? 10 : 30);
            var absolutePath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            bitmap.Save(absolutePath, ImageFormat.Png);
            if (!noGame && section is "talents" or "equipment") SaveNativeCanvas(form, absolutePath, section);
            host.Hide();
            Console.WriteLine($"PASS Preview render: {absolutePath} ({bitmap.Width}x{bitmap.Height}); layout scale {scale:0.##}x; device DPI {form.DeviceDpi}.");
            Console.WriteLine(noGame ? "PASS Single game-folder prompt and hidden browser actions."
                : "PASS Assistant home, hidden data navigation, collection search, and main control bounds.");
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

    private static void SaveNativeCanvas(Form form, string previewPath, string section)
    {
        var browsers = (IDictionary)Field(form, "browsers");
        var nativeView = Field(browsers[section]!, "customView");
        var canvas = (Control)Field(nativeView, "canvas");
        using var bitmap = new Bitmap(canvas.Width, canvas.Height);
        canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        var suffix = section == "talents" ? "tree" : "sheet";
        var path = Path.Combine(Path.GetDirectoryName(previewPath)!, Path.GetFileNameWithoutExtension(previewPath) + $"-{suffix}.png");
        bitmap.Save(path, ImageFormat.Png);
        Console.WriteLine($"PASS Native {section} canvas render: {path} ({bitmap.Width}x{bitmap.Height}).");
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
        var assistantHome = Field(form, "assistantHome");
        assistantHome.GetType().GetMethod("SetCharacter")!.Invoke(assistantHome, [snapshot]);
        var connection = (Control)Field(form, "connection");
        connection.Text = "●  Ready";
        var theme = form.GetType().Assembly.GetType("Claudgar.App.Browser.BrowserTheme")!;
        connection.ForeColor = (Color)theme.GetField("Success", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        ((Control)Field(form, "exportStatus")).Text = "1 saved character · Updates automatically · Export: ready";
        ((Control)Field(form, "setupDetails")).Text = "SETUP & CONNECTION\n\nPreview with fictional character data.\n\nAll five saved sections are ready to browse.";
        foreach (DictionaryEntry entry in (IDictionary)Field(form, "browsers"))
        {
            var browser = (Control)entry.Value!;
            if ((string)entry.Key == "equipment")
                browser.GetType().GetMethod("SetCharacter")!.Invoke(browser, [snapshot["sections"]!["character"]!.AsObject()]);
            browser.GetType().GetMethod("SetSection")!.Invoke(browser, [snapshot["sections"]![(string)entry.Key]!.AsObject(), false]);
            if ((string)entry.Key is "talents" or "equipment") continue;
            // Hidden TreeViews do not reliably dispatch AfterSelect until their native handle exists.
            var tree = (TreeView)Field(browser, "tree");
            typeof(TreeView).GetMethod("OnAfterSelect", PrivateInstance)!.Invoke(tree, [new TreeViewEventArgs(tree.Nodes[0])]);
        }
    }

    private static void Verify(Form form, Control host)
    {
        VerifyStreamlinedActions(form);
        VerifyAssistantHome(form, host);
        Invoke(form, "ShowDataView");
        LayoutTree(host);
        Application.DoEvents();
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
        tree.SelectedNode = tree.Nodes[0].Nodes[1];
        typeof(TreeView).GetMethod("OnAfterSelect", PrivateInstance)!.Invoke(tree, [new TreeViewEventArgs(tree.SelectedNode)]);
        var questGrid = (DataGridView)Field(quests, "grid");
        Assert(questGrid.RowCount == 4 && questGrid.Rows[0].Cells["title"].Value?.ToString() == "The Fargodeep Mine", "Completed quest names did not reach the browser.");
        ((TextBox)Field(quests, "search")).Text = "Jasperlode";
        Assert(questGrid.RowCount == 1, "Saved completed quest titles are not searchable.");
        ((TextBox)Field(quests, "search")).Clear();
        VerifyCustomViews(form, browsers);
        VerifyDataNavigation(form, host);
    }

    private static void VerifyAssistantHome(Form form, Control host)
    {
        var home = (Control)Field(form, "assistantHome");
        var tabs = (Control)Field(form, "tabs");
        Assert(home.Visible && !tabs.Visible, "The assistant home must be the default; data tabs must stay hidden.");
        Assert(!((Control)Field(form, "exportStatus")).Visible, "The home still shows the detailed export footer.");
        var suggestions = new[]
        {
            "Which areas should I quest next?",
            "What quests do I need for Deadmines?",
            "How can I improve my gear?"
        };
        var buttons = Descendants(home).OfType<Button>().ToArray();
        var actualSuggestions = ((IEnumerable)Property(home, "SuggestionButtons")).Cast<Button>().Select(button => button.Text).ToArray();
        Assert(actualSuggestions.SequenceEqual(suggestions),
            "The home does not show exactly the three requested questions.");
        Assert(buttons.Any(button => button.Text.Contains("ChatGPT")) && buttons.Any(button => button.Text.Contains("Claude")),
            "Both assistant launch choices must be reachable from home.");
        foreach (var button in buttons.Where(button => button.Visible))
        {
            Assert(button.Height >= 32 && button.Width >= 100, $"Home action is too small: {button.Text}, {button.Bounds}.");
            Assert(button.Parent!.ClientRectangle.Contains(button.Bounds), $"Home action is clipped: {button.Text}, {button.Bounds}.");
        }
        Assert(home.Width > 500 && home.Height > 280 && home.Parent!.ClientRectangle.Contains(home.Bounds),
            $"The assistant home does not fit its content area: {home.Bounds}.");
        var banner = (Control)Field(form, "characterHeading");
        var detailsButton = (Button)banner.GetType().GetProperty("DetailsButton")!.GetValue(banner)!;
        Assert(detailsButton.Visible && detailsButton.Enabled && banner.ClientRectangle.Contains(detailsButton.Bounds),
            "Character details must be reachable without exposing the data tabs.");
        detailsButton.PerformClick();
        LayoutTree(host);
        Application.DoEvents();
        Assert(tabs.Visible && !home.Visible, "The character-details action does not open the data view.");
        Invoke(form, "ShowAssistantHome");
        LayoutTree(host);
        Application.DoEvents();
        Console.WriteLine("PASS Minimal assistant home, exact question suggestions, and reachable character details.");
    }

    private static void VerifyDataNavigation(Form form, Control host)
    {
        var tabs = Field(form, "tabs");
        var selectedIndex = tabs.GetType().GetProperty("SelectedIndex")!;
        var characters = (ListBox)Field(form, "characters");
        var selectedCharacter = characters.SelectedItem;
        selectedIndex.SetValue(tabs, 1);
        var back = (Button)Field(form, "dataBackButton");
        Assert(back.Visible && back.Enabled && back.Parent!.ClientRectangle.Contains(back.Bounds),
            "The data view needs an accessible action to return to the assistant home.");
        back.PerformClick();
        LayoutTree(host);
        Application.DoEvents();
        Assert(((Control)Field(form, "assistantHome")).Visible && !((Control)tabs).Visible,
            "Returning from details does not restore the assistant home.");
        Assert(ReferenceEquals(selectedCharacter, characters.SelectedItem) && (int)selectedIndex.GetValue(tabs)! == 1,
            "Returning home lost the selected character or data section.");
        Invoke(form, "ShowDataView");
        LayoutTree(host);
        Application.DoEvents();
        Assert(((Control)tabs).Visible && (int)selectedIndex.GetValue(tabs)! == 1 && ReferenceEquals(selectedCharacter, characters.SelectedItem),
            "Opening details again did not preserve the last selected character and section.");
        selectedIndex.SetValue(tabs, 0);
        Console.WriteLine("PASS Home/details navigation preserves selected character and data section.");
    }

    private static void VerifyCustomViews(Form form, IDictionary browsers)
    {
        var tabs = Field(form, "tabs");
        var selectedIndex = tabs.GetType().GetProperty("SelectedIndex")!;
        foreach (var (section, index) in new[] { ("inventory", 3), ("talents", 2), ("equipment", 4) })
        {
            selectedIndex.SetValue(tabs, index);
            var browser = browsers[section]!;
            var custom = Field(browser, "customView");
            if (section == "inventory")
            {
                var modes = Field(browser, "viewModes");
                var modeIndex = modes.GetType().GetProperty("SelectedIndex")!;
                Assert((int)modeIndex.GetValue(modes)! == 0, "Inventory does not default to its custom view.");
                modeIndex.SetValue(modes, 1);
                Assert(((TreeView)Field(browser, "tree")).Visible, "Inventory JSON tree toggle failed.");
                modeIndex.SetValue(modes, 0);
                var slots = ((IEnumerable)Field(custom, "slots")).Cast<Button>().ToArray();
                Assert(slots.Length == 28, "Bag view lost physical slot positions.");
                var first = slots[0];
                first.PerformClick();
                Assert(((RichTextBox)Field(custom, "details")).Text.Contains("Hearthstone"), "Selecting a stack did not show item details.");
                var search = (TextBox)Field(custom, "search");
                search.Text = "Healing Potion";
                Assert(slots.Count(slot => (bool)slot.GetType().GetProperty("MatchesSearch")!.GetValue(slot)!) == 1, "Bag search did not highlight one stack.");
                search.Clear();
                var snapshot = (JsonObject)Field(form, "currentSnapshot");
                var partial = snapshot["sections"]!["inventory"]!.DeepClone().AsObject();
                partial["status"] = "partial";
                custom.GetType().GetMethod("SetSection")!.Invoke(custom, [partial]);
                var partialSlots = ((IEnumerable)Field(custom, "slots")).Cast<Button>().ToArray();
                Assert(partialSlots.All(slot => !(bool)slot.GetType().GetProperty("KnownEmpty")!.GetValue(slot)!), "Partial inventory presented an unrecorded slot as empty.");
                custom.GetType().GetMethod("SetSection")!.Invoke(custom, [snapshot["sections"]!["inventory"]!.AsObject()]);
            }
            else if (section == "talents")
            {
                VerifyTalents(custom, (JsonObject)Field(form, "currentSnapshot"));
            }
            else VerifyEquipment(custom, (JsonObject)Field(form, "currentSnapshot"));
        }
        selectedIndex.SetValue(tabs, 0);
    }

    private static void VerifyEquipment(object custom, JsonObject snapshot)
    {
        var canvas = (Control)Field(custom, "canvas");
        var presentation = custom.GetType().Assembly.GetType("Claudgar.App.Browser.EquipmentPresentation")!;
        var original = snapshot["sections"]!["equipment"]!.AsObject();
        void SetSection(JsonObject section) => custom.GetType().GetMethod("SetSection")!.Invoke(custom, [section]);
        object[] Slots() => ((IEnumerable)Property(Field(canvas, "sheet"), "Slots")).Cast<object>().ToArray();
        object Slot(string name) => Slots().Single(slot => Equals(Property(Property(slot, "Definition"), "Name"), name));
        string State(string name) => Property(Slot(name), "State").ToString()!;
        int UnplacedCount() => ((IEnumerable)Property(Field(canvas, "sheet"), "Unplaced")).Cast<object>().Count();

        Assert(!Descendants((Control)custom).Any(control => control is ComboBox or Button or RichTextBox),
            "The paper doll still includes selectors, toolbar buttons, or a details sidebar.");
        Assert(canvas.Visible && canvas.Dock == DockStyle.Fill && canvas.Size == ((Control)custom).ClientSize,
            "The equipment canvas does not fill its available viewport.");
        Assert(Slots().Length == 20 && Slots().Count(slot => Property(slot, "State").ToString() == "Equipped") == 18 &&
            Slots().Count(slot => Property(slot, "State").ToString() == "Empty") == 2 && UnplacedCount() == 0,
            "The paper doll did not preserve all 20 physical equipment slots and their saved contents.");
        var placements = ((IEnumerable)Field(canvas, "placements")).Cast<object>().ToArray();
        Assert(placements.Length == 20 && placements.Select(placed => Property(Property(Property(placed, "Slot"), "Definition"), "Name")).Distinct().Count() == 20,
            "The paper doll omitted or duplicated physical slot positions.");
        var slotBounds = placements.ToDictionary(
            placed => (string)Property(Property(Property(placed, "Slot"), "Definition"), "Name"),
            placed => (RectangleF)Property(placed, "Bounds"));
        Assert(slotBounds["HeadSlot"].Left < slotBounds["HandsSlot"].Left && slotBounds["HeadSlot"].Top == slotBounds["HandsSlot"].Top &&
            slotBounds["ChestSlot"].Top < slotBounds["WristSlot"].Top && slotBounds["MainHandSlot"].Top > slotBounds["WristSlot"].Bottom &&
            slotBounds["MainHandSlot"].Left < slotBounds["SecondaryHandSlot"].Left && slotBounds["SecondaryHandSlot"].Left < slotBounds["RangedSlot"].Left,
            "Equipment was not arranged into familiar left/right armor columns and bottom weapon slots.");

        var chest = (JsonObject)Property(Slot("ChestSlot"), "Item");
        var details = (string)presentation.GetMethod("Details")!.Invoke(null, [chest, "Chest"])!;
        Assert(details.Contains("Devout Robe") && details.Contains("Rare") && details.Contains("32 / 80") &&
            details.Contains("Intellect") && details.Contains("item:16690") && !details.Contains("JSON"),
            "The equipment hover details lost its recorded quality, durability, stats, or item link.");
        Assert(Equals(presentation.GetMethod("Quality")!.Invoke(null, [chest]), 3) &&
            Equals(presentation.GetMethod("IconId")!.Invoke(null, [chest]), 132652L),
            "Recorded equipment quality or icon identity was not retained.");
        Assert(Equals(presentation.GetMethod("Stat")!.Invoke(null, [chest["stats"]!.AsObject(), "ITEM_MOD_INTELLECT_SHORT"]), 4d),
            "Integer-valued saved item stats were omitted from the recorded-bonus display.");

        var preferredName = original.DeepClone().AsObject();
        preferredName["data"]!["items"]!.AsArray().OfType<JsonObject>().Single(item => item["slotName"]!.GetValue<string>() == "HeadSlot")["slot"] = 16;
        SetSection(preferredName);
        Assert(State("HeadSlot") == "Equipped" && State("MainHandSlot") == "Equipped" && UnplacedCount() == 0 &&
            ((JsonObject)Property(Slot("HeadSlot"), "Item"))["name"]!.GetValue<string>() == "Devout Crown",
            "A saved slot name lost priority over a conflicting numeric slot ID.");

        var partial = original.DeepClone().AsObject();
        partial["status"] = "partial";
        var partialItems = partial["data"]!["items"]!.AsArray();
        partialItems.Remove(partialItems.OfType<JsonObject>().Single(item => item["slotName"]!.GetValue<string>() == "NeckSlot"));
        SetSection(partial);
        Assert(State("NeckSlot") == "Unknown" && State("AmmoSlot") == "Empty" && State("Trinket1Slot") == "Empty",
            "A partial export treated an unrecorded equipment slot as empty or discarded explicit empty records.");

        var duplicates = original.DeepClone().AsObject();
        var duplicateItems = duplicates["data"]!["items"]!.AsArray();
        var duplicate = duplicateItems.OfType<JsonObject>().Single(item => item["slotName"]!.GetValue<string>() == "HeadSlot").DeepClone().AsObject();
        duplicate["name"] = "Another recorded crown";
        duplicateItems.Add(duplicate);
        SetSection(duplicates);
        Assert(State("HeadSlot") == "Conflict" && UnplacedCount() == 2,
            "Conflicting equipment records chose one item silently or lost one of the recorded items.");
        Assert(((IEnumerable)Field(canvas, "extras")).Cast<object>().Count() == 2,
            "The conflicting saved equipment items did not receive visible positions below the sheet.");

        var unmatched = original.DeepClone().AsObject();
        var extra = chest.DeepClone().AsObject();
        extra["slotName"] = "FutureEquipmentSlot";
        extra["slot"] = 21;
        unmatched["data"]!["items"]!.AsArray().Add(extra);
        SetSection(unmatched);
        Assert(UnplacedCount() == 1 && Slots().Count(slot => Property(slot, "State").ToString() == "Equipped") == 18,
            "An unfamiliar equipment slot was assigned to a known position or discarded.");
        Assert(((IEnumerable)Field(canvas, "extras")).Cast<object>().Count() == 1,
            "An unfamiliar-slot item has no visible position in the equipment sheet.");
        Assert(((IEnumerable)Field(canvas, "placements")).Cast<object>().Count() >= 20,
            "The unknown-slot item prevented ordinary equipment positions from rendering.");

        var legacy = original.DeepClone().AsObject();
        foreach (var item in legacy["data"]!["items"]!.AsArray().OfType<JsonObject>())
        {
            var name = item["slotName"]!.GetValue<string>();
            if (name is not ("ChestSlot" or "MainHandSlot")) continue;
            item.Remove("slotName");
            item["slot"] = name == "ChestSlot" ? "Chest" : "Main hand";
            item["quality"] = name == "ChestSlot" ? "Rare" : "Uncommon";
        }
        SetSection(legacy);
        Assert(State("ChestSlot") == "Equipped" && State("MainHandSlot") == "Equipped" && UnplacedCount() == 0,
            "Older text equipment-slot records disappeared from their familiar paper-doll positions.");
        var legacyQuality = presentation.GetMethod("Quality")!.Invoke(null, [(JsonObject)Property(Slot("ChestSlot"), "Item")]);
        Assert(Equals(legacyQuality, 3), "An older named equipment quality did not reach the quality border.");

        SetSection(original);
        VerifyEquipmentViewport(canvas);
        canvas.GetType().GetMethod("OnKeyDown", PrivateInstance)!.Invoke(canvas, [new KeyEventArgs(Keys.Right)]);
        Assert(Field(canvas, "selected") is not null, "Keyboard navigation could not select an equipment slot.");
        canvas.GetType().GetMethod("OnKeyDown", PrivateInstance)!.Invoke(canvas, [new KeyEventArgs(Keys.Escape)]);
        Assert(Field(canvas, "selected") is null, "Escape did not clear the selected equipment slot.");
        canvas.Refresh();
        Console.WriteLine("PASS Full paper doll, slot certainty, conflicts, older snapshots, quality and recorded hover details.");
    }

    private static void VerifyEquipmentViewport(Control canvas)
    {
        var sheetSize = (SizeF)Field(canvas, "sheetSize");
        var scale = (float)Field(canvas, "fittedScale");
        Assert(sheetSize.Width * scale <= canvas.Width && sheetSize.Height * scale <= canvas.Height,
            "The fitted equipment sheet extends outside its viewport.");
        Assert(sheetSize.Width * scale >= canvas.Width * 0.90 || sheetSize.Height * scale >= canvas.Height * 0.90,
            "The equipment sheet does not use the available viewport.");
        var placements = ((IEnumerable)Field(canvas, "placements")).Cast<object>().ToArray();
        var minimumFrame = placements.Select(placed => (RectangleF)Property(placed, "Bounds")).Min(bounds => Math.Min(bounds.Width, bounds.Height));
        Assert(minimumFrame * scale >= (canvas.Height >= 400 ? 30 : 24), "Paper-doll item frames remain too small for the available viewport.");
        var originalSize = canvas.Size;
        canvas.Dock = DockStyle.None;
        canvas.Size = new Size(originalSize.Width * 3 / 4, originalSize.Height * 3 / 4);
        Assert((float)Field(canvas, "fittedScale") < scale * 0.85, "Resizing the equipment canvas did not refit its paper doll.");
        canvas.Size = originalSize;
        canvas.Dock = DockStyle.Fill;
        canvas.Parent!.PerformLayout();
        Assert(Math.Abs((float)Field(canvas, "fittedScale") - scale) < 0.01, "Restoring the equipment viewport did not restore its fitted layout.");
    }

    private static void VerifyTalents(object custom, JsonObject snapshot)
    {
        var list = (DataGridView)Field(custom, "list");
        var canvas = (Control)Field(custom, "canvas");
        var original = snapshot["sections"]!["talents"]!.AsObject();
        void SetSection(JsonObject section) => custom.GetType().GetMethod("SetSection")!.Invoke(custom, [section]);
        int PanelCount() => ((IEnumerable)Field(canvas, "panels")).Cast<object>().Count();
        object[] RenderedNodes() => ((IEnumerable)Field(canvas, "nodes")).Cast<object>()
            .Select(placed => Property(placed, "Node")).ToArray();

        var descendants = Descendants((Control)custom).ToArray();
        Assert(!descendants.Any(control => control is ComboBox or Button or RichTextBox),
            "The full talent tree still includes selectors, toolbar buttons, or a details sidebar.");
        Assert(canvas.Visible && canvas.Dock == DockStyle.Fill && canvas.Size == ((Control)custom).ClientSize,
            "The native talent canvas does not fill its available viewport.");
        Assert(!((Label)Field(custom, "fallbackMessage")).Visible,
            "The normal native talent tree still displays a helper heading.");
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 3,
            "The full tree did not render all three native talent panels.");
        Assert(RenderedNodes().Length == 47 && list.RowCount == 47, "The talent overview lost saved nodes.");
        Assert(RenderedNodes().All(node => Property(node, "IconFileId") is long icon && icon > 0),
            "Saved talent icon IDs did not reach the native nodes.");

        var separateTrees = PreviewFixture.CreateSnapshot()["sections"]!["talents"]!.AsObject();
        SetSection(separateTrees);
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 3 && RenderedNodes().Length == 47,
            "Multiple saved native trees did not render their complete overview.");

        var hidden = original.DeepClone().AsObject();
        var hiddenNode = hidden["data"]!["trees"]![0]!["nodes"]![0]!.AsObject();
        hiddenNode["isVisible"] = false;
        hiddenNode.Remove("posX");
        SetSection(hidden);
        Assert((bool)Field(custom, "nativeLayout") && RenderedNodes().Length == 46 && list.RowCount == 47,
            "A hidden node with no position broke the tree or disappeared from the complete list.");

        var missingLayout = original.DeepClone().AsObject();
        missingLayout["data"]!["trees"]![0]!["nodes"]![0]!.AsObject().Remove("posX");
        SetSection(missingLayout);
        Assert(!(bool)Field(custom, "nativeLayout") && list.Visible && list.RowCount == 47,
            "Missing visible talent positions did not fall back to a complete overview list.");
        Assert(((Label)Field(custom, "fallbackMessage")).Visible && ((Label)Field(custom, "fallbackMessage")).Text.Length > 0,
            "The incomplete-layout fallback does not explain why the talent list is shown.");

        var overlapping = original.DeepClone().AsObject();
        var firstNodes = overlapping["data"]!["trees"]![0]!["nodes"]!.AsArray();
        firstNodes[1]!["posX"] = firstNodes[0]!["posX"]!.DeepClone();
        firstNodes[1]!["posY"] = firstNodes[0]!["posY"]!.DeepClone();
        SetSection(overlapping);
        Assert(!(bool)Field(custom, "nativeLayout") && list.RowCount == 47,
            "Overlapping saved talent positions did not preserve the complete fallback list.");

        var grouped = PreviewFixture.CreateGroupedTalentSection();
        SetSection(grouped);
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 3,
            "A single native tree containing three talent groups did not preserve its three columns.");
        Assert(RenderedNodes().Length == 47 && list.RowCount == 47, "Splitting talent groups lost or duplicated native nodes.");
        var groupedPositions = ((IEnumerable)Field(canvas, "nodes")).Cast<object>().ToDictionary(
            placed => (long)Property(Property(placed, "Node"), "Id"),
            placed => (PointF)Property(placed, "Position"));
        var columnSpacing = groupedPositions[101].X - groupedPositions[100].X;
        Assert(Math.Abs(groupedPositions[200].X - groupedPositions[100].X - 3 * columnSpacing) < 0.02 &&
            Math.Abs(groupedPositions[200].Y - groupedPositions[100].Y) < 0.02,
            "Separating group backgrounds changed the native distances or alignment between talent columns.");

        var ordinaryTreeHeight = ((SizeF)Field(canvas, "treeSize")).Height;
        var distantDuplicate = grouped.DeepClone().AsObject();
        var distantNodes = distantDuplicate["data"]!["trees"]![0]!["nodes"]!.AsArray();
        var duplicate = distantNodes.OfType<JsonObject>().Single(node => node["nodeId"]!.GetValue<int>() == 202).DeepClone().AsObject();
        duplicate["nodeId"] = 9998;
        duplicate["posX"] = 9280;
        duplicate["posY"] = 21300;
        distantNodes.Add(duplicate);
        SetSection(distantDuplicate);
        var duplicatePlacement = ((IEnumerable)Field(canvas, "nodes")).Cast<object>()
            .Single(node => Equals(Property(Property(node, "Node"), "Id"), 9998L));
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 3 && RenderedNodes().Length == 48 && list.RowCount == 48 &&
            (bool)Property(duplicatePlacement, "OutsideSavedLayout") && ((SizeF)Field(canvas, "treeSize")).Height < ordinaryTreeHeight + 200,
            "A distant duplicate still shrinks ordinary talents or disappears from the complete tree.");

        var hiddenGroup = grouped.DeepClone().AsObject();
        foreach (var node in hiddenGroup["data"]!["trees"]![0]!["nodes"]!.AsArray().OfType<JsonObject>())
            if (node["groupIds"]!.AsArray().Any(group => group!.GetValue<int>() == 1)) node["isVisible"] = false;
        SetSection(hiddenGroup);
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 1 && RenderedNodes().Length == 32 && list.RowCount == 47,
            "An entirely hidden group produced a degenerate column or removed hidden talents from the list.");

        var emptyMembership = grouped.DeepClone().AsObject();
        foreach (var node in emptyMembership["data"]!["trees"]![0]!["nodes"]!.AsArray().OfType<JsonObject>())
            node["groupIds"] = new JsonArray();
        SetSection(emptyMembership);
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 1 && RenderedNodes().Length == 47 && list.RowCount == 47,
            "Empty membership arrays prevented the complete native tree from rendering.");

        var unknownMembership = grouped.DeepClone().AsObject();
        unknownMembership["data"]!["trees"]![0]!["nodes"]![0]!.AsObject().Remove("groupIds");
        SetSection(unknownMembership);
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 1 && RenderedNodes().Length == 47 && list.RowCount == 47,
            "Unknown group membership invented separate native columns or lost nodes.");

        var sharedMembership = grouped.DeepClone().AsObject();
        sharedMembership["data"]!["trees"]![0]!["nodes"]![0]!["groupIds"] = new JsonArray(1, 2);
        SetSection(sharedMembership);
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 1 && RenderedNodes().Length == 47,
            "A shared talent was duplicated or its saved native tree was rearranged.");

        var unassigned = grouped.DeepClone().AsObject();
        var unassignedNodes = unassigned["data"]!["trees"]![0]!["nodes"]!.AsArray();
        var additionalNode = unassignedNodes[0]!.DeepClone().AsObject();
        additionalNode["nodeId"] = 9999;
        additionalNode["groupIds"] = new JsonArray();
        additionalNode["posX"] = 9000;
        unassignedNodes.Add(additionalNode);
        SetSection(unassigned);
        Assert((bool)Field(custom, "nativeLayout") && PanelCount() == 1 && RenderedNodes().Length == 48 && list.RowCount == 48,
            "A talent without a recorded group disappeared or received an invented group position.");

        var choice = original.DeepClone().AsObject();
        var choiceNode = choice["data"]!["trees"]![0]!["nodes"]![0]!.AsObject();
        choiceNode["activeRank"] = 1;
        choiceNode["currentRank"] = 1;
        choiceNode["activeEntryId"] = 9002;
        choiceNode["entries"] = new JsonArray(
            new JsonObject { ["entryId"] = 9001, ["name"] = "Healing Focus", ["iconFileId"] = 135918, ["isActive"] = false },
            new JsonObject { ["entryId"] = 9002, ["name"] = "Holy Specialization", ["iconFileId"] = 135967, ["isActive"] = true });
        SetSection(choice);
        var chosenNode = RenderedNodes().Single(node => Equals(Property(node, "Id"), 100L));
        Assert(Equals(Property(chosenNode, "IconFileId"), 135967L),
            "A choice node retained the first option's metadata instead of its active option.");
        canvas.GetType().GetMethod("SelectNode", PrivateInstance)!.Invoke(canvas, [chosenNode]);
        Assert(ReferenceEquals(Field(canvas, "selected"), chosenNode), "Selecting a choice node did not retain its selection.");

        var staged = original.DeepClone().AsObject();
        staged["data"]!["hasStagedChanges"] = true;
        staged["data"]!["trees"]![0]!["nodes"]![0]!["currentRank"] = 3;
        SetSection(staged);
        var stagedNode = RenderedNodes().Single(node => Equals(Property(node, "Id"), 100L));
        var stagedDescription = (string)canvas.GetType().GetMethod("NodeDescription", PrivateInstance)!.Invoke(canvas, [stagedNode])!;
        Assert((bool)Property(canvas, "HasStagedChanges") && stagedDescription.Contains("unapplied", StringComparison.OrdinalIgnoreCase),
            "The compact talent tooltip lost the warning about saved unapplied talent changes.");
        SetSection(original);
        canvas.GetType().GetMethod("FitToViewport")!.Invoke(canvas, null);
        VerifyTalentViewport(canvas);
        VerifyNativeTalentSnapshot(custom);
        Console.WriteLine("PASS Full talent viewport, native group coordinates, active choice metadata, selection, hidden nodes, and complete layout fallbacks.");
    }

    private static void VerifyNativeTalentSnapshot(object custom)
    {
        var nativeSection = PreviewFixture.CreateNativeTalentSection();
        custom.GetType().GetMethod("SetSection")!.Invoke(custom, [nativeSection]);
        var canvas = (Control)Field(custom, "canvas");
        var placed = ((IEnumerable)Field(canvas, "nodes")).Cast<object>().ToArray();
        Assert((bool)Field(custom, "nativeLayout") && placed.Length == 54 && ((DataGridView)Field(custom, "list")).RowCount == 54,
            "The exact native coordinate fixture lost saved talents or fell back to a list.");
        Assert(((IEnumerable)Field(canvas, "panels")).Cast<object>().Count() == 3,
            "A distant duplicate talent prevented the real native tree from displaying its three groups.");
        var outliers = placed.Where(node => (bool)Property(node, "OutsideSavedLayout")).ToArray();
        Assert(outliers.Length == 1 && Equals(Property(Property(outliers[0], "Node"), "Id"), 105865L),
            "The distant duplicate was omitted or compacted without preserving its explicit status.");

        var normal = nativeSection["data"]!["trees"]![0]!["nodes"]!.AsArray().OfType<JsonObject>()
            .Where(node => node["nodeId"]!.GetValue<long>() != 105865L).ToArray();
        var positions = placed.ToDictionary(node => (long)Property(Property(node, "Node"), "Id"), node => (PointF)Property(node, "Position"));
        var first = normal[0];
        var second = normal.First(node => node["posX"]!.GetValue<double>() != first["posX"]!.GetValue<double>());
        var firstPosition = positions[first["nodeId"]!.GetValue<long>()];
        var secondPosition = positions[second["nodeId"]!.GetValue<long>()];
        var nativeScale = (secondPosition.X - firstPosition.X) / (second["posX"]!.GetValue<double>() - first["posX"]!.GetValue<double>());
        foreach (var node in normal)
        {
            var position = positions[node["nodeId"]!.GetValue<long>()];
            Assert(Math.Abs(position.X - firstPosition.X - nativeScale * (node["posX"]!.GetValue<double>() - first["posX"]!.GetValue<double>())) < 0.03 &&
                Math.Abs(position.Y - firstPosition.Y - nativeScale * (node["posY"]!.GetValue<double>() - first["posY"]!.GetValue<double>())) < 0.03,
                "Compacting a distant duplicate altered the 53 ordinary nodes' native relative positions.");
        }
        var treeSize = (SizeF)Field(canvas, "treeSize");
        Assert(treeSize.Height < (21300 - 2130) * nativeScale / 3,
            "A distant duplicate still forces the entire native tree into a tall mostly-empty coordinate range.");
        var description = (string)canvas.GetType().GetMethod("NodeDescription", PrivateInstance)!.Invoke(canvas, [Property(outliers[0], "Node")])!;
        Assert(description.Contains("21300", StringComparison.Ordinal), "The compact duplicate tooltip lost its original saved coordinates.");
        VerifyTalentViewport(canvas);
        Console.WriteLine("PASS All 54 native talents retained, 53 native positions preserved, and the distant duplicate represented compactly.");
    }

    private static void VerifyTalentViewport(Control canvas)
    {
        var treeSize = (SizeF)Field(canvas, "treeSize");
        float DrawScale() => (float)canvas.GetType().GetProperty("DrawScale", PrivateInstance)!.GetValue(canvas)!;
        var originalScale = DrawScale();
        var rendered = new SizeF(treeSize.Width * originalScale, treeSize.Height * originalScale);
        Assert(rendered.Width <= canvas.ClientSize.Width && rendered.Height <= canvas.ClientSize.Height,
            "The fitted talent tree extends outside its viewport.");
        Assert(rendered.Width >= canvas.ClientSize.Width * 0.90 || rendered.Height >= canvas.ClientSize.Height * 0.90,
            "The automatic talent layout leaves most of the available viewport empty.");
        var layoutType = canvas.GetType().Assembly.GetType("Claudgar.App.Browser.TalentTreeLayout")!;
        var iconSize = (int)layoutType.GetField("IconSize", BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
        var minimumNodePixels = canvas.Height >= 400 ? 30 : 24;
        Assert(iconSize * originalScale >= minimumNodePixels,
            $"Talent nodes remain too small for the available preview viewport: {canvas.ClientSize}, node {iconSize * originalScale:0.0}px, required {minimumNodePixels}px.");

        var originalSize = canvas.Size;
        canvas.Dock = DockStyle.None;
        canvas.Size = new Size(originalSize.Width * 3 / 4, originalSize.Height * 3 / 4);
        Assert(DrawScale() < originalScale * 0.85, "Resizing the talent canvas did not refit its native tree.");
        canvas.Size = originalSize;
        canvas.Dock = DockStyle.Fill;
        canvas.Parent!.PerformLayout();
        Assert(Math.Abs(DrawScale() - originalScale) < 0.01, "Restoring the talent viewport did not restore its fitted layout.");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
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
        var shell = (TableLayoutPanel)issueBar.Parent!;
        Assert(shell.GetRowHeights()[shell.GetRow(issueBar)] == 0,
            "Issue actions occupy space before a game folder is selected.");
    }

    private static void VerifyMinimalShell(Control host, Form form, Control releaseControls)
    {
        Assert(!Descendants(host).Any(control => control.GetType().Name == "AppGuidance" && control.Visible),
            "The old question-guidance banner still occupies the primary interface.");
        var startup = (CheckBox)releaseControls.GetType().GetProperty("StartupCheckBox")!.GetValue(releaseControls)!;
        Assert(!startup.Visible, "Startup preferences must be tucked into settings on the default screen.");
        var settingsButton = (Button)Field(form, "settingsButton");
        var settingsMenu = (ContextMenuStrip)Field(form, "settingsMenu");
        Assert(settingsButton.Visible && settingsButton.Enabled && settingsButton.Parent!.ClientRectangle.Contains(settingsButton.Bounds),
            "The settings action is missing or clipped.");
        try
        {
            settingsButton.PerformClick();
            Application.DoEvents();
            Assert(settingsMenu.Visible, "The settings action did not open its menu.");
            Assert(startup.Visible && startup.Parent!.ClientRectangle.Contains(startup.Bounds),
                "The startup preference is not reachable inside settings.");
            var help = new[] { "Download ChatGPT", "Download Claude", "Ask from your phone", "About Claudgar" };
            Assert(help.All(text => settingsMenu.Items.OfType<ToolStripMenuItem>().Any(item => item.Text == text && item.Enabled)),
                "The existing help destinations are missing from settings.");
        }
        finally
        {
            settingsMenu.Close();
            Application.DoEvents();
        }
        Assert(!startup.Visible, "Closing settings leaves its preferences visible.");
        Console.WriteLine("PASS Hidden instruction banner and startup preference; settings retains startup and help destinations.");
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
    private static object Property(object target, string name) => target.GetType().GetProperty(name)!.GetValue(target)!;
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, null);
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static (string Output, int Width, int Height, float Scale, bool NoGame, string Section) ParseArguments(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Usage: Claudgar.Preview <output.png> [--width 1440] [--height 960] [--scale 2] [--section home] [--no-game]");
        var width = 1440;
        var height = 960;
        var scale = 1f;
        var noGame = false;
        var section = "home";
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--no-game") { noGame = true; continue; }
            if (i + 1 == args.Length) throw new ArgumentException("Every size option needs a value.");
            switch (args[i])
            {
                case "--width": width = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                case "--height": height = int.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                case "--scale": scale = float.Parse(args[i + 1], CultureInfo.InvariantCulture); break;
                case "--section": section = args[i + 1].ToLowerInvariant(); break;
                default: throw new ArgumentException($"Unknown option: {args[i]}");
            }
            i++;
        }
        if (width < 950 || height < 620 || width > 3840 || height > 2160 || scale is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(args), "Use widths 950–3840, heights 620–2160, and scales 1–2.");
        if (!new[] { "home", "character", "quests", "talents", "inventory", "equipment" }.Contains(section))
            throw new ArgumentException("Choose home, character, quests, talents, inventory, or equipment for --section.");
        return (args[0], width, height, scale, noGame, section);
    }
}
