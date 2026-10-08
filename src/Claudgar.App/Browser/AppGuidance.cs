using System.Diagnostics;
using Claudgar.App.Resources;
using Claudgar.Core;

namespace Claudgar.App.Browser;

/// <summary>Help and published setup guides opened from the settings menu.</summary>
internal static class AppGuidance
{
    internal const string ChatGptDownload = "https://learn.chatgpt.com/docs/quickstart";
    internal const string ClaudeDownload = "https://claude.com/download";
    internal const string PhoneGuide = "https://learn.chatgpt.com/docs/remote-connections";

    private static LinkLabel Link(string text, LinkLabelLinkClickedEventHandler clicked)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, LinkColor = BrowserTheme.Accent,
            ActiveLinkColor = BrowserTheme.Ink, VisitedLinkColor = BrowserTheme.Accent, Margin = new Padding(0, 0, 20, 0) };
        link.LinkClicked += clicked;
        return link;
    }

    internal static void OpenUrl(IWin32Window owner, string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        { MessageBox.Show(owner, "The browser could not open this page.\n\n" + url, "Claudgar", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }

    internal static void ShowAbout(IWin32Window owner) => ShowHelp(owner, "About Claudgar",
        $"Claudgar {BuildInfo.Version}\n\nOnly saved Forever character exports are read. Requested character data is processed by the chat provider; account folder names and local paths are excluded from tool results.\n\n" + EmbeddedPayload.LoadLicense(),
        "Report a problem", "https://github.com/RichardBangs/Claudgar/issues");

    internal static void ShowPhoneHelp(IWin32Window owner) => ShowHelp(owner, "Ask from your phone",
        "CHATGPT\n\n" +
        "1. Set up Claudgar and check that a character question works in ChatGPT on your PC.\n\n" +
        "2. In ChatGPT on the PC, open Settings > Connections > Control this Mac or PC, then Set up or Add.\n\n" +
        "3. Scan the QR code with your phone and finish pairing using the same ChatGPT account and workspace.\n\n" +
        "4. On your phone, open Codex (or Remote where that label is used), select your connected PC, and ask a Claudgar question.\n\n" +
        "CLAUDE\n\n" +
        "1. Check that a character question works in Claude Code on your PC. Remote Control needs Claude Code and a paid Claude plan.\n\n" +
        "2. On the PC, run claude remote-control in a terminal, or turn on Remote Control from Claude Desktop's settings.\n\n" +
        "3. Open the Claude app on your phone, pick the connected session, and ask a Claudgar question.\n\n" +
        "Keep your PC awake and online, with the chat app and Claudgar running. Availability depends on your account, plan, and rollout. The phone uses the PC's local tools.",
        "Open official phone setup guide", PhoneGuide);

    private static void ShowHelp(IWin32Window owner, string title, string text, string linkText, string url)
    {
        using var dialog = new Form { Text = title, StartPosition = FormStartPosition.CenterParent, Size = new Size(700, 570),
            MinimumSize = new Size(520, 420), BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink, Font = new Font("Segoe UI", 10) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(20) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new RichTextBox { Text = text, Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = false,
            BorderStyle = BorderStyle.None, BackColor = BrowserTheme.Surface, ForeColor = BrowserTheme.Ink, Font = dialog.Font }, 0, 0);
        layout.Controls.Add(Link(linkText, (_, _) => OpenUrl(dialog, url)), 0, 1);
        dialog.Controls.Add(layout); dialog.ShowDialog(owner);
    }
}
