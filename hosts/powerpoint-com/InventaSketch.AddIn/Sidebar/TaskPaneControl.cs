using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace InventaSketch.AddIn.Sidebar
{
    /// <summary>
    /// Content of the InventaSketch custom task pane: a WebView2 hosting the same editor as the web add-in
    /// (sidebar.html: library, stage, inspector, pie menu, floating bar) from the add-in folder. Slide work goes through
    /// <see cref="SidebarBridge"/>. Created by Office through its ProgId (ICTPFactory.CreateCTP).
    /// </summary>
    [ComVisible(true)]
    [Guid("3E4B8064-5C25-452F-8F79-22581212A547")]
    [ProgId("InventaSketch.AddIn.TaskPane")]
    [ClassInterface(ClassInterfaceType.AutoDispatch)]
    public sealed class TaskPaneControl : UserControl
    {
        public const string HostName = "app.inventasketch";
        private readonly WebView2 _web;
        private bool _started;

        /// <summary>The task pane instance created by Office (one per add-in session).</summary>
        internal static TaskPaneControl? Current { get; private set; }

        /// <summary>JSON messages posted by the editor (window.chrome.webview.postMessage).</summary>
        internal event Action<string>? MessageReceived;

        /// <summary>Query string for sidebar.html (language, theme, slide size); set before the pane is shown.</summary>
        internal static string Query { get; set; } = "";

        public TaskPaneControl()
        {
            _web = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_web);
            Current = this;
            HandleCreated += (_, _) => _ = StartAsync();
        }

        public static string AppFolder => Path.Combine(Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location)!, "App");

        private async Task StartAsync()
        {
            if (_started) return;
            _started = true;
            try
            {
                var dir = Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location)!;
                var arch = Environment.Is64BitProcess ? "win-x64" : "win-x86";
                var loader = Path.Combine(dir, "runtimes", arch, "native");
                if (Directory.Exists(loader)) CoreWebView2Environment.SetLoaderDllFolderPath(loader);
                // A writable user data folder is mandatory: the default would be next to POWERPNT.EXE (read-only).
                var data = Path.Combine(Log.Folder, "WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, data, null);
                await _web.EnsureCoreWebView2Async(env);
                var core = _web.CoreWebView2;
                core.Settings.AreDevToolsEnabled = true;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.SetVirtualHostNameToFolderMapping(HostName, AppFolder, CoreWebView2HostResourceAccessKind.Allow);
                core.WebMessageReceived += (_, e) =>
                {
                    try { MessageReceived?.Invoke(e.WebMessageAsJson); }
                    catch (Exception ex) { Log.Error("Sidebar message", ex); }
                };
                if (!File.Exists(Path.Combine(AppFolder, "sidebar.html")))
                {
                    core.NavigateToString("<p style='font-family:Segoe UI;padding:12px'>InventaSketch: sidebar files missing (" + AppFolder + "). Run scripts\\install-com.ps1 after building hosts\\office-web.</p>");
                    return;
                }
                core.Navigate("https://" + HostName + "/sidebar.html" + Query);
                Log.Info("Sidebar started (WebView2 " + env.BrowserVersionString + ")");
            }
            catch (Exception ex)
            {
                Log.Error("Sidebar start", ex);
                var label = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "InventaSketch: WebView2 could not be started. " + ex.Message,
                    Padding = new Padding(12),
                };
                Controls.Add(label);
                label.BringToFront();
            }
        }

        /// <summary>Runs a script in the editor (automation and tests); fire-and-forget.</summary>
        internal void Run(string script)
        {
            try { _ = _web.CoreWebView2?.ExecuteScriptAsync(script); }
            catch (Exception ex) { Log.Error("Sidebar script", ex); }
        }

        /// <summary>Sends a JSON message to the editor (window.chrome.webview "message" event).</summary>
        internal void Post(string json)
        {
            try { _web.CoreWebView2?.PostWebMessageAsJson(json); }
            catch (Exception ex) { Log.Error("Sidebar post", ex); }
        }
    }
}
