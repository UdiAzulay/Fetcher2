using System;
using System.Linq;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using CefSharp.WinForms.Internals;
using Fetcher2;
namespace Fetcher2.UI
{
    public class AppWindow : Form
    {
        public static string FileExt = ".fetcher2";
        private static string DefaultFilter { get { return string.Format("{0} (*{1})|*{1}|All files (*.*)|*.*", Application.ProductName, FileExt); } }

        public event EventHandler<DocumentEventArgs> DocumentCreated;
        public Core.ContextManager ContextManager { get; private set; }

        public UI.ActionsListStrip ActionsList { get; private set; }
        public UI.PropertiesStrip Properties { get; private set; }
        public UI.LogAndDataStrip LogAndData { get; private set; }

        private Control RightContainer;
        private StatusStrip StatusBar;
        private ToolStripStatusLabel StateBox;
        private ToolStripStatusLabel StateContexts;
        private ToolStripStatusLabel StatusRowCount;
        private ToolStripStatusLabel StatusBox;

        public AppWindow(string fileName = null)
        {
            ContextManager = new Core.ContextManager((b) => NewContext(b), this.InvokeOnUiThreadIfRequired, 9);
            IsMdiContainer = true;
            Text = Application.ProductName;
            Size = new System.Drawing.Size(800, 580);
            Controls.AddRange(new Control[]{
                new Splitter(){ Dock = DockStyle.Right, MinSize = 30 },
                (RightContainer = new Control(){ Dock = DockStyle.Right, Width = 300 }),
                (MainMenuStrip = new MenuStrip() { Dock = DockStyle.Top, ImageList = Icons.ImageList, ImageScalingSize = Icons.ImageList.ImageSize }),
                new Splitter(){ Dock = DockStyle.Bottom, MinSize = 30 },
                (LogAndData = new LogAndDataStrip(this) { Dock = DockStyle.Bottom, Height = 150 }),
                (StatusBar = new StatusStrip() { Dock = DockStyle.Bottom, Stretch = true, LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow }),
            });
            InitAppMenu();
            RightContainer.Controls.AddRange(new Control[] {
                Properties = new PropertiesStrip(this) { Dock = DockStyle.Fill },
                new Splitter(){ Dock = DockStyle.Top, MinSize = 50 },
                ActionsList = new ActionsListStrip(this) { Dock = DockStyle.Top, Height = 200 },
            });
            ActionsList.SelectedActionsChanged += (e, a) => { Properties.SelectedObjects = ActionsList.SelectedActions.ToArray(); };

            StatusBar.Items.AddRange(new ToolStripItem[] {
                StateBox = new ToolStripStatusLabel() { AutoSize = false, Overflow = ToolStripItemOverflow.Never, Width = 60, Alignment = ToolStripItemAlignment.Right, TextAlign = System.Drawing.ContentAlignment.MiddleCenter },
                new ToolStripSeparator() { Alignment = ToolStripItemAlignment.Right },
                StateContexts = new ToolStripStatusLabel() { AutoSize = false, Overflow = ToolStripItemOverflow.Never, Width = 100, Alignment = ToolStripItemAlignment.Right, TextAlign = System.Drawing.ContentAlignment.MiddleCenter },
                new ToolStripSeparator() { Alignment = ToolStripItemAlignment.Right },
                StatusRowCount = new ToolStripStatusLabel() { AutoSize = false, Overflow = ToolStripItemOverflow.Never, Width = 80, Alignment = ToolStripItemAlignment.Right, TextAlign = System.Drawing.ContentAlignment.MiddleCenter },
                new ToolStripSeparator() { Alignment = ToolStripItemAlignment.Right },
                StatusBox = new ToolStripStatusLabel() { AutoSize = true, Overflow = ToolStripItemOverflow.Never, Alignment = ToolStripItemAlignment.Left,TextAlign = System.Drawing.ContentAlignment.MiddleLeft },
            });
            ContextManager.Changed += (s, a) => this.InvokeOnUiThreadIfRequired(() => StateContexts.Text = string.Format("{0} / {1} running", ContextManager.RuningContextCount, ContextManager.ContextCount));
            //ToolStripManager.LoadSettings(this);
            if (!string.IsNullOrEmpty(fileName) && System.IO.File.Exists(fileName)) Open(fileName);
            else New();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                if (ContextManager != null) ContextManager.StopAll();
                CloseAllDocuments();
                Application.DoEvents();
                if (ContextManager != null) { ContextManager.Dispose(); ContextManager = null; }
            }
            base.Dispose(disposing);
        }

        public ToolStripDropDownItem CreateNewActionMenu(ToolStripDropDownItem parent)
        {
            foreach (var v in Actions.File.ActionsTypes) parent.DropDownItems.Add(v.Name);
            parent.DropDownItems.Add(new ToolStripSeparator());
            var scFile = new MenuButton("Script File...", null, (s, a) => OpenScript(null, true));
            parent.DropDownItems.Add(scFile);
            parent.DropDownItemClicked += (s, a) =>
            {
                if (a.ClickedItem == scFile) return;
                var item = Actions.Action.Create(a.ClickedItem.Text);
                ContextManager.File.Children.Add(item);
            };
            return parent;
        }

        private void InitAppMenu()
        {
            MainMenuStrip.Items.AddRange(new[] {
                new MenuButton("&File") {
                    DropDownItems = {
                        new MenuButton("&New Solution", null, (s, a) => New()),
                        new ToolStripSeparator(),
                        new MenuButton("&Open Solution...", Icons.Icon_FileOpenFolder, (s, a) => Open(), Keys.Control| Keys.Shift | Keys.O) { },
                        new MenuButton("&Save Solution", Icons.Icon_FileSave, (s, a) => Save(ContextManager.File.FileName), Keys.Control | Keys.Shift | Keys.S) { },
                        new MenuButton("&Save Solution As...", Icons.Icon_FileSave, (s, a) => Save(null)) {  },
                        new ToolStripSeparator(),
                        new MenuButton("&Open Script...", Icons.Icon_FileOpen, (s, a) => OpenScript(), Keys.Control | Keys.O) { },
                        new ToolStripSeparator(),
                        new MenuButton("&New Browser", null, (s, a) => new BrowserDocument(this), Keys.Control | Keys.N) { },
                        new ToolStripSeparator(),
                        new MenuButton("&Save Data...", Icons.Icon_FileSaveAll, (s, a) => Core.DataUtils.Save(ContextManager.DataSet, ContextManager.File.OutputPath, null, this), Keys.Control | Keys.E) { },
                        new ToolStripSeparator(),
                        new MenuButton("&Exit", null, (s, a) => Close(), Keys.Alt | Keys.F4),
                    }
                },
                new MenuButton("&Edit"){
                    DropDownItems = {
                        CreateNewActionMenu(new MenuButton("Add &Action", Icons.Icon_ActionAdd)),
                        new MenuButton("&Remove Action", Icons.Icon_ActionRemove, (s, a) => ActionsList.RemoveSelected()),
                        new ToolStripSeparator(),
                        new MenuButton("Cu&t", Icons.Icon_EditCut, (s, a) => ForwardClipboardEvent(Keys.Control | Keys.X), Keys.Control | Keys.X),
                        new MenuButton("&Copy", Icons.Icon_EditCopy, (s, a) => ForwardClipboardEvent(Keys.Control | Keys.C), Keys.Control | Keys.C),
                        new MenuButton("&Paste", Icons.Icon_EditPaste, (s, a) => ForwardClipboardEvent(Keys.Control | Keys.V), Keys.Control | Keys.V),
                    }
                },
                new MenuButton("&View") {
                    DropDownItems = {
                        new MenuButton("Show &Actions", null, (s, a) => RightContainer.Visible = (s as ToolStripMenuItem).Checked){ CheckOnClick = true, CheckState = CheckState.Checked },
                        new MenuButton("&Show Log", null, (s, a) => LogAndData.Visible = (s as ToolStripMenuItem).Checked){ CheckOnClick = true, CheckState = CheckState.Checked },
                        new ToolStripSeparator(),
                        new MenuButton("Clear &Log", null, (s, a) => LogAndData.LogTextBox.Clear()),
                        new ToolStripSeparator(),
                        new MenuButton("Refre&sh Data", null, (s, a) => LogAndData.RefreshDataTables()),
                        new MenuButton("Clear &Data", null, (s, a) => ContextManager.ClearData()),
                    },
                },
                new MenuButton("&Execute")
                {
                    DropDownItems = {
                        new MenuButton("Break A&ll", Icons.Icon_Break, (s, a) => ContextManager.BreakAll(), Keys.Control | Keys.Shift | Keys.Pause),
                        new MenuButton("C&ontinue All", Icons.Icon_Step, (s, a) => ContextManager.ContinueAll()),
                        new MenuButton("Stop &All", Icons.Icon_Step, (s, a) => ContextManager.StopAll()),
                    }
                },
                MainMenuStrip.MdiWindowListItem = MainMenuStrip.MdiWindowListItem = new MenuButton("&Window") {
                    DropDownItems = {
                        new MenuButton("&Cascade", null, (s, a) => LayoutMdi(MdiLayout.Cascade)),
                        new MenuButton("&Tile Horizontaly", null, (s, a) => LayoutMdi(MdiLayout.TileHorizontal)),
                        new MenuButton("&Tile Verticaly", null, (s, a) => LayoutMdi(MdiLayout.TileVertical)),
                        new MenuButton("&Arrange Icons", null, (s, a) => LayoutMdi(MdiLayout.ArrangeIcons)),
                        new ToolStripSeparator(),
                        new MenuButton("Close All &Windows", null, (s, a) => CloseAllDocuments()),
                    }
                },
                new MenuButton("&Help") {
                    DropDownItems = {
                        new MenuButton("&About...", null, (s, a) => ShowAbout())
                    }
                }
            });
        }
        
        private void ForwardClipboardEvent(Keys key)
        {
            var ctl = ActiveForm?.ActiveControl;
            if (ctl == null) return;
            var controlSend = typeof(SendKeys).GetMethod("Send", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            string format = ((key & Keys.Control) == Keys.Control ? "^" : "") + ((key & Keys.Alt) == Keys.Alt ? "%" : "") + ((key & Keys.Shift) == Keys.Shift ? "+" : "");
            format += (char)(Keys.KeyCode & key);
            controlSend.Invoke(null, new object[] { format, ctl, true });
        }

        protected override void OnMdiChildActivate(EventArgs e)
        {
            ActiveContext = (ActiveMdiChild as BrowserDocument)?.Context;
            base.OnMdiChildActivate(e);
        }

        private Core.Context _activeContext = null;
        private Core.Context ActiveContext
        {
            get { return _activeContext; }
            set { 
                if (_activeContext != null) _activeContext.StateChanged -= Context_StateChanged;
                _activeContext = value;
                if (_activeContext != null) {
                    _activeContext.StateChanged += Context_StateChanged;
                    Context_StateChanged(_activeContext, EventArgs.Empty);
                } else StateBox.Text = "";
            }
        }

        private void Context_StateChanged(object sender, EventArgs e)
        {
            var state = (sender as Core.Context).State;
            StateBox.Text = state.ToString();
            if (state != Core.ContextState.Paly) LogAndData.RefreshDataTables();
        }

        public string Status { get { return StatusBox.Text; } set { StatusBox.Text = value; } }
        public string RowCount { get { return StatusRowCount.Text; } set { StatusRowCount.Text = value; } }

        protected internal void DocummentAdded(Document doc)
        {
            DocumentCreated?.Invoke(this, new DocumentEventArgs(doc));
        }

        public void EditAction(Actions.Action action)
        {
            string formKey = "Document_" + action.GetHashCode().ToString();
            var doc = Application.OpenForms[formKey] as Document;
            if (doc == null) {
                switch (action)
                {
                    case Core.IScriptSource s:
                        doc = new TextDocument(this, s) { Name = formKey }; break;
                }
                if (doc == null) return;
                doc.Show();
            } else doc.Focus();
        }

        private Core.Context NewContext(bool contextAcquired)
        {
            Func<BrowserDocument> createDoc = () => new BrowserDocument(this, true, true);
            if (InvokeRequired) return (Invoke(createDoc) as BrowserDocument).Context;
            else return createDoc().Context;
        }

        public void OpenScript(string fileName = null, bool addAction = false)
        {
            if (string.IsNullOrEmpty(fileName)) fileName = this.GetOpenFileName("Script Files (*.js)|*.js|All files (*.*)|*.*");
            if (fileName == null) return;
            var doc = new TextDocument(this, fileName);
            if (addAction) ContextManager.File.Children.Add(doc.Action as Actions.Action);
            doc.Show();
        }

        public void CloseAllDocuments()
        {
            var children = MdiChildren;
            for (int i = children.Length - 1; i >= 0; i--)
                children[i].Close();
        }

        public void New() { ContextManager.File = new Actions.File(); }

        public void Open(string fileName = null)
        {
            if (string.IsNullOrEmpty(fileName)) fileName = this.GetOpenFileName(FileExt, DefaultFilter);
            if (fileName == null) return;
            ContextManager.File = Actions.File.Load(fileName);
            if (!string.IsNullOrEmpty(ContextManager.File.Url)) {
                var v = new BrowserDocument(this);
                Application.DoEvents();
                v.Context.LoadUrl(ContextManager.File.Url);
            }
        }

        public void Save(string fileName = null)
        {
            if (string.IsNullOrEmpty(fileName)) fileName = this.GetSaveFileName(FileExt, DefaultFilter);
            if (fileName == null) return;
            ContextManager.File.Save(fileName);
        }

        public void ShowAbout()
        {
            MessageBox.Show(this,
                string.Format("{0}\r\nUdi azulay, Modern Systems Ltd(c) 2019\r\nOpen source under MIT License", Application.ProductName),
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }

    }
}
