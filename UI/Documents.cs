using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using CefSharp.WinForms.Internals;

namespace Fetcher2.UI
{
    public class DocumentException : Exception
    {
        public Document Document { get; private set; }
        public DocumentException(Document document, string message, Exception inner = null) : base(message, inner) { Document = document; }
    }

    public class DocumentEventArgs : EventArgs
    {
        public Document Document { get; private set; }
        public DocumentEventArgs(Document document) { Document = document; }
    }

    public class Document : Form
    {
        public AppWindow OwnerWindow { get; private set; }
        public Document(AppWindow owner)
        {
            MdiParent = OwnerWindow = owner;
            WindowState = FormWindowState.Maximized;
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            OwnerWindow.DocummentAdded(this);
        }
    }

    public class TextDocument : Document
    {
        public Core.IScriptSource Action { get; private set; }
        private TextBox TextBox;
        public TextDocument(AppWindow owner) : base(owner)
        {
            TextBox = new TextBox() { Multiline = true, Dock = DockStyle.Fill };
            MainMenuStrip = new MenuStrip()
            {
                Dock = DockStyle.Top, AllowMerge = true, Visible = false,
                Items = {
                    new MenuButton("&File") {
                        MergeAction = MergeAction.MatchOnly,
                        DropDownItems = {
                            new MenuButton("Save Script...", null, (s, a)=> Save(), Keys.Control | Keys.S) { MergeAction = MergeAction.Insert, MergeIndex = 7 }
                        }
                    },
                },
            };
            Controls.AddRange(new Control[] { TextBox, MainMenuStrip });
        }

        public TextDocument(AppWindow owner, string path) : this(owner, new Actions.Script(path)) { }
        public TextDocument(AppWindow owner, Core.IScriptSource action) : this(owner)
        {
            Action = action;
            TextBox.Text = Action.Source;
        }

        public void Save()
        {
            var value = TextBox.Text;
            Action.Source = value;
        }
    }

    public class BrowserDocument : Document
    {
        public ChromiumWebBrowser Browser { get; private set; }
        public string Url { get { return Browser.Address; } }
        public Core.Context Context { get; private set; }
        private MenuStrip AddressBar;
        private ToolStripTextBox AddressBox;
        private string _browserTitle;
        public BrowserDocument(AppWindow owner, bool show = true, bool contextAcquired = false) : base(owner)
        {
            MainMenuStrip = new MenuStrip() {
                Dock = DockStyle.Top, AllowMerge = true, Visible = false,
                Items = {
                    new MenuButton("&View")
                    {
                        MergeAction = MergeAction.MatchOnly,
                        DropDownItems = {
                            new MenuButton("&DevTools", null, (s, a) => { if ((s as ToolStripMenuItem).Checked) Browser.ShowDevTools(); else Browser.CloseDevTools(); }, Keys.F12) { CheckOnClick = true, MergeIndex = 0, MergeAction = MergeAction.Insert },
                            new ToolStripSeparator(){ MergeIndex = 1, MergeAction = MergeAction.Insert },
                        }
                    },
                    new MenuButton("&Navigate")
                    {
                        MergeIndex = 3, MergeAction = MergeAction.Insert,
                        DropDownItems = {
                            new MenuButton("&Forward", Icons.Icon_Forward, (s, a) => Browser.Forward()),
                            new MenuButton("&Back", Icons.Icon_Back, (s, a) => Browser.Back()),
                            new MenuButton("&Stop", Icons.Icon_Stop, (s, a) => Browser.Stop()),
                            new ToolStripSeparator(),
                            new MenuButton("&Refresh", Icons.Icon_Refresh, (s, a) => Browser.Reload()),
                        }
                    },
                    new MenuButton("&Execute")
                    {
                        MergeAction = MergeAction.MatchOnly,
                        DropDownItems = {
                            new MenuButton("&Play", Icons.Icon_Play, (s, a) => Context.Play(), Keys.F5) { MergeAction = MergeAction.Insert, MergeIndex = 0 },
                            new ToolStripSeparator() { MergeAction = MergeAction.Insert, MergeIndex = 1 },
                            new MenuButton("&Break", Icons.Icon_Break, (s, a) => Context.Break(), Keys.Control | Keys.Pause) { MergeAction = MergeAction.Insert, MergeIndex = 2 },
                            new MenuButton("&Step", Icons.Icon_Step, (s, a) => Context.Step(), Keys.F8) { MergeAction = MergeAction.Insert, MergeIndex = 3 },
                            new MenuButton("Sto&p", Icons.Icon_Stop, (s, a) => Context.Stop(), Keys.Shift | Keys.F5) { MergeAction = MergeAction.Insert, MergeIndex = 4 },
                            new ToolStripSeparator(){ MergeAction = MergeAction.Insert, MergeIndex = 5 },
                            new ToolStripSeparator(),
                            new MenuButton("Register &JS Fetcher", null, (s, a) => RegisterJsObject(), Keys.F9),
                        }
                    }
                },
            };
            AddressBar = new MenuStrip() {
                Dock = DockStyle.Top, AllowMerge = false,
                Stretch = true, LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                ImageList = Icons.ImageList,
                Items = {
                    new ToolbarButton("Back", Icons.Icon_Back, (s,a) => Browser.Back(), "navigate-backward") ,
                    new ToolbarButton("Forward", Icons.Icon_Forward, (s, a) => Browser.Forward(), "navigate-forward"),
                    new ToolbarButton("Refresh", Icons.Icon_Refresh, (s, a) => Browser.Reload(), "navigate-refresh"),
                    new ToolStripLabel("&Address"){  TextAlign = System.Drawing.ContentAlignment.TopLeft },
                    (AddressBox = new ToolStripTextBox("AddressBox") { AutoSize = false }),
                    new ToolStripButton("Go", null, (s, a) => Context.LoadUrl(AddressBox.Text)) { Alignment = ToolStripItemAlignment.Right },
                }
            };
            Browser = new ChromiumWebBrowser(new CefSharp.Web.HtmlString("<center><h1>Enter URL</h1></center>")) {
                Dock = DockStyle.Fill,
                BrowserSettings = new BrowserSettings()
                {
                    FileAccessFromFileUrls = CefState.Enabled,
                    UniversalAccessFromFileUrls = CefState.Enabled,
                }
            };
            Controls.AddRange(new Control[] { Browser, MainMenuStrip, AddressBar });
            AddressBox.KeyUp += (s, a) => { if (a.KeyCode != Keys.Enter) return; Context.LoadUrl(AddressBox.Text); };

            AddressBar.Layout += (s, a) => {
                var width = AddressBar.Width;
                foreach (ToolStripItem item in AddressBar.Items)
                    if (item != AddressBox) width -= item.Width - item.Margin.Horizontal;
                AddressBox.Width = Math.Max(0, width - AddressBox.Margin.Horizontal - 18);
            };

            Browser.TitleChanged += (s, a) => this.InvokeOnUiThreadIfRequired(() => { _browserTitle = a.Title; Context_StateChanged(Context, EventArgs.Empty); });
            Browser.AddressChanged += (s, a) => this.InvokeOnUiThreadIfRequired(() => AddressBox.Text = a.Address);
            Browser.StatusMessage += (s, a) => this.InvokeOnUiThreadIfRequired(() => OwnerWindow.Status = a.Value);
            Browser.LoadingStateChanged += (s, a) => {
                this.InvokeOnUiThreadIfRequired(() => {
                    AddressBar.Items["navigate-refresh"].Enabled = a.CanReload;
                    AddressBar.Items["navigate-forward"].Enabled = a.CanGoForward;
                    AddressBar.Items["navigate-backward"].Enabled = a.CanGoBack;
                });
            };
            OwnerWindow.ContextManager.FileChanged += OwnerWindow_FileChanged;
            OwnerWindow_FileChanged(OwnerWindow, EventArgs.Empty);
            owner.ContextManager.AddContext(Context, contextAcquired);
            if (show) Show();
        }

        private void OwnerWindow_FileChanged(object sender, EventArgs e)
        {
            if (Context != null) {
                Context.Exception -= Context_Exception;
                Context.StateChanged -= Context_StateChanged;
                (Context as IDisposable).Dispose();
                Context = null;
            }
            Context = new Core.Context(Browser, OwnerWindow);
            Context.Exception += Context_Exception;
            Context.StateChanged += Context_StateChanged;
        }

        private void Context_StateChanged(object sender, EventArgs e)
        {
            Text = string.Format("{0}: {1}", Context.State, _browserTitle);
        }

        private void Context_Exception(object sender, UnhandledExceptionEventArgs e)
        {
            this.MessageException(e.ExceptionObject as Exception);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            Browser.Dispose();
            (Context as IDisposable).Dispose();
        }

        public void RegisterJsObject()
        {
            if (Browser.JavascriptObjectRepository.IsBound("Fetcher")) return;
            Browser.JavascriptObjectRepository.Register("Fetcher", Context, true, new BindingOptions());
            var res = Browser.GetMainFrame().EvaluateScriptAsync("CefSharp.BindObjectAsync(\"Fetcher\");").Result;
        }
    }
}
