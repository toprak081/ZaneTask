using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace ZaneTask.Desktop;

/// <summary>The app window: shows a short "starting" message, then the ZaneTask UI in an embedded browser.</summary>
internal sealed class MainForm : Form
{
    // Page background colors from design-system/zanetask/MASTER.md, so there is no white flash while loading.
    private static readonly Color LightBackground = Color.FromArgb(0xF5, 0xF3, 0xFF);
    private static readonly Color DarkBackground = Color.FromArgb(0x0F, 0x0E, 0x1A);

    private readonly WebView2 _webView;
    private readonly Label _status;
    private readonly CancellationTokenSource _closing = new();
    private LocalServer? _server;

    public MainForm()
    {
        var dark = IsWindowsDarkMode();
        var background = dark ? DarkBackground : LightBackground;

        Text = "ZaneTask";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(420, 480);
        BackColor = background;

        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11f),
            ForeColor = dark ? Color.FromArgb(0xA8, 0xAE, 0xC8) : Color.FromArgb(0x47, 0x55, 0x69),
            Text = "Starting ZaneTask…",
        };
        _webView = new WebView2 { Dock = DockStyle.Fill, Visible = false, DefaultBackgroundColor = background };

        Controls.Add(_webView);
        Controls.Add(_status);

        if (dark)
            HandleCreated += (_, _) => UseDarkTitleBar(Handle);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            _server = await LocalServer.StartAsync(_closing.Token);

            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebViewDir);
            await _webView.EnsureCoreWebView2Async(environment);
            ConfigureBrowser(_webView.CoreWebView2);

            _webView.Source = _server.Url;
            _webView.Visible = true;
            _status.Visible = false;
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested)
        {
            // Window closed while starting.
        }
        catch (WebView2RuntimeNotFoundException)
        {
            Fail("ZaneTask needs the Microsoft Edge WebView2 Runtime, which is missing on this computer.\n\n" +
                 "Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and start ZaneTask again.");
        }
        catch (Exception ex)
        {
            Fail($"ZaneTask could not start.\n\n{ex.Message}");
        }
    }

    private void ConfigureBrowser(CoreWebView2 browser)
    {
        browser.Settings.IsStatusBarEnabled = false;
#if !DEBUG
        browser.Settings.AreDevToolsEnabled = false;
#endif
        browser.DocumentTitleChanged += (_, _) => Text = browser.DocumentTitle;

        // Keep the app window on ZaneTask; anything else (e.g. links in comments) opens in your normal browser.
        browser.NewWindowRequested += (_, args) =>
        {
            args.Handled = true;
            OpenInDefaultBrowser(args.Uri);
        };
        browser.NavigationStarting += (_, args) =>
        {
            if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var target) && target.Authority != _server!.Url.Authority)
            {
                args.Cancel = true;
                OpenInDefaultBrowser(args.Uri);
            }
        };
    }

    /// <summary>Called when the icon is clicked again while the app is already open.</summary>
    public void BringToFrontFromAnotherLaunch()
    {
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
    }

    private void Fail(string message)
    {
        MessageBox.Show(this, message, "ZaneTask", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _closing.Cancel();
        _webView.Dispose();
        _server?.Dispose();
        base.OnFormClosed(e);
    }

    private static void OpenInDefaultBrowser(string url)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private static bool IsWindowsDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is 0;
    }

    private static void UseDarkTitleBar(IntPtr handle)
    {
        const int DwmwaUseImmersiveDarkMode = 20;
        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
