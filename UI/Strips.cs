using CefSharp.WinForms.Internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Fetcher2.UI
{
    public abstract class Strip : Control
    {
        public AppWindow OwnerWindow { get; private set; }
        public abstract string Title { get; }
        protected Control Control { get { return Controls[0]; } }
        public Strip(AppWindow owner, Control control) {
            OwnerWindow = owner;
            control.Dock = DockStyle.Fill;
            SuspendLayout();
            Controls.Add(control);
            Controls.Add(new Control() { Height = 2, Dock = DockStyle.Top, });
            Controls.Add(new Label() {
                Text = Title, TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                AutoSize = false, Dock = DockStyle.Top,
                Height = SystemInformation.MenuHeight,
                Font = System.Drawing.SystemFonts.CaptionFont,
                BackColor = System.Drawing.SystemColors.ActiveCaption,
                ForeColor = System.Drawing.SystemColors.ActiveCaptionText,
            });
            ResumeLayout(false);
        }
    }

    public class ActionsListStrip : Strip
    {
        public override string Title { get { return "Actions"; } }
        public EventHandler SelectedActionsChanged;
        private ToolStrip ToolBar;
        private ListView List;
        private BrowserDocument _browserDoc;
        private ListViewItem _actionItem;

        public IList<Actions.Action> SelectedActions
        {
            get {
                var ret = new List<Actions.Action>();
                foreach (ListViewItem v in List.SelectedItems) ret.Add(v.Tag as Actions.Action);
                return ret;
            }
            set {
                List.SelectedItems.Clear();
                foreach (var v in value) {
                    var item = ItemByAction(v);
                    if (item != null) item.Selected = true;
                }
            }
        }

        public ActionsListStrip(AppWindow owner) : base(owner, new Control() { Dock = DockStyle.Fill })
        {
            List = new ListView() {
                Dock = DockStyle.Fill, Height = 300,
                View = View.Details, AllowDrop = true,
                FullRowSelect = true, HideSelection = false,
                SmallImageList = new ImageList() { ImageSize = new System.Drawing.Size(16, 16) },
            };
            List.Columns.AddRange(new[] {
                new ColumnHeader() { Text = "Action", Width = 150 },
                new ColumnHeader() { Text = "Count", Width = 40},
                new ColumnHeader() { Text = "Progress", Width = 40 }
            });
            List.SelectedIndexChanged += (s, a) => SelectedActionsChanged?.Invoke(this, EventArgs.Empty);
            List.DoubleClick += (s, a) => {
                if (List.SelectedItems.Count != 1) return;
                var item = List.SelectedItems[0].Tag as Actions.Action;
                OwnerWindow.EditAction(item);
            };
            List.KeyUp += (s, a) => { if (a.KeyCode == Keys.Delete) RemoveSelected(); };
            List.ItemDrag += (s, a) => List.DoDragDrop(a.Item, DragDropEffects.Move);
            List.DragEnter += (s, a) => a.Effect = CanDrop(a, false) ? DragDropEffects.Move : DragDropEffects.None;
            List.DragOver += (s, a) => a.Effect = CanDrop(a, false) ? DragDropEffects.Move : DragDropEffects.None;
            List.DragDrop += (s, a) => { if (CanDrop(a, true)) RefreshActions(); };
            ToolBar = new ToolStrip() {
                Dock = DockStyle.Top,
                Stretch = true, AutoSize = true,
                LayoutStyle = ToolStripLayoutStyle.Flow,
                ImageList = Icons.ImageList, 
                Items = {
                    new ToolbarButton("Stop", Icons.Icon_Stop, (s, a) => _browserDoc?.Context?.Stop(), "stop"),
                    new ToolbarButton("Break / Continue", Icons.Icon_Break, (s, a) =>  BreakOrStep(), "break"),
                    new ToolbarButton("Play", Icons.Icon_Play, (s, a) => _browserDoc?.Context?.Play(), "play") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, AutoSize = true },
                    new ToolStripSeparator(),
                    new ToolbarButton("Analyze", Icons.Icon_Analyze, (s, a) => Analyze(), "analyze"),
                    new ToolStripSeparator(),
                    OwnerWindow.CreateNewActionMenu(new ToolStripDropDownButton("&Add"){ ImageKey = Icons.Icon_ActionAdd, ImageAlign = System.Drawing.ContentAlignment.BottomLeft }),
                }
            };
            base.Control.Controls.AddRange(new Control[] { List, ToolBar });
            OwnerWindow.ContextManager.FileChanged += OwnerWindow_FileChanged;
            OwnerWindow.MdiChildActivate += OwnerWindow_MdiChildActivate;
        }

        private bool CanDrop(DragEventArgs e, bool drop)
        {
            var srcItem = e.Data.GetData(typeof(ListViewItem)) as ListViewItem;
            var cp = List.PointToClient(new System.Drawing.Point(e.X, e.Y));
            var trgitem = List.GetItemAt(cp.X, cp.Y);
            var target = trgitem?.Tag as Actions.Action;
            var targetParent = (target == null) ? OwnerWindow.ContextManager.File : target as Actions.ParentAction;
            var source = srcItem?.Tag as Actions.Action;
            if (source == null || source == target || targetParent == null) return false;
            var ret = targetParent.CanHaveChild(source);
            if (!ret) return false;
            if (drop) targetParent.Children.Add(source);
            return true;
        }

        private void OwnerWindow_MdiChildActivate(object sender, EventArgs e)
        {
            BrowserDocument browserDoc = null;
            if (OwnerWindow.ActiveMdiChild is UI.TextDocument textDoc) {
                if (textDoc != null && textDoc.Action != null) SelectedActions = new[] { textDoc.Action as Actions.Action };
                else List.SelectedItems.Clear();
            } else if (OwnerWindow.ActiveMdiChild is UI.BrowserDocument bd) {
                browserDoc = bd;
            }
            BrowserDoc = browserDoc;
        }

        private BrowserDocument BrowserDoc
        {
            get { return _browserDoc;}
            set {
                if (_browserDoc != null && _browserDoc.Context != null) {
                    _browserDoc.Context.StepChanged -= Context_Step;
                    _browserDoc.Context.StateChanged -= Context_StateChanged;
                }
                foreach (ListViewItem v in List.Items) v.BackColor = System.Drawing.Color.Transparent;
                _actionItem = null;
                _browserDoc = value;
                ToolBar.Items["play"].Enabled = ToolBar.Items["break"].Enabled = 
                    ToolBar.Items["stop"].Enabled = ToolBar.Items["analyze"].Enabled = _browserDoc != null;
                if (_browserDoc != null) {
                    _browserDoc.Context.StateChanged += Context_StateChanged;
                    Context_StateChanged(_browserDoc.Context, EventArgs.Empty);
                    _actionItem = _browserDoc.Context.CurrentAction != null ? ItemByAction(_browserDoc.Context.CurrentAction) : null;
                    if (_actionItem != null) _actionItem.BackColor = System.Drawing.Color.Red;
                    _browserDoc.Context.StepChanged += Context_Step;
                    //if (_browserDoc.Context.CurrentAction != null) Context_Step(_browserDoc.Context, EventArgs.Empty);
                }
            }
        }

        private void Context_StateChanged(object sender, EventArgs e)
        {
            var context = sender as Core.Context;
            if(context.State == Core.ContextState.Stop)
                foreach (ListViewItem v in List.Items) v.BackColor = System.Drawing.Color.Transparent;
            _actionItem = null;
            ToolBar.Items["analyze"].Enabled = context.State == Core.ContextState.Stop;
            ToolBar.Items["stop"].Enabled = context.State != Core.ContextState.Stop;
            ToolBar.Items["play"].Enabled = context.State != Core.ContextState.Paly;
            ToolBar.Items["break"].ImageKey = context.State == Core.ContextState.Pause ? Icons.Icon_Step : Icons.Icon_Break;
        }

        private void Context_Step(object sender, Core.Context.StepEventArgs e)
        {
            var context = sender as Core.Context;
            if (_actionItem != null) _actionItem.BackColor = System.Drawing.Color.Transparent;
            _actionItem = e.Action != null ? ItemByAction(e.Action) : null;
            if (_actionItem != null)
            {
                while (_actionItem.SubItems.Count < 3) _actionItem.SubItems.Add(new ListViewItem.ListViewSubItem());
                _actionItem.SubItems[1].Text = e.Count.ToString();
                _actionItem.SubItems[2].Text = (e.Index + 1).ToString();
                _actionItem.BackColor = System.Drawing.Color.Red;
            }
        }

        private void BreakOrStep()
        {
            if (_browserDoc == null) return;
            var context = _browserDoc.Context;
            if (context.State == Core.ContextState.Paly) context.Break(); 
            else if (context.IsWait) context.Break();
            else context.Step();
        }

        private ListViewItem ItemByAction(Actions.Action action)
        {
            var key = action.GetHashCode().ToString();
            if (!List.Items.ContainsKey(key)) return null;
            return List.Items[key];
        }

        private void OwnerWindow_FileChanged(object sender, Core.ContextManager.FileChangedEventArgs e)
        {
            if (e.OldValue != null) e.OldValue.Changed -= File_Changed;
            if (sender is Core.ContextManager cm && cm.File != null) cm.File.Changed += File_Changed;
            RefreshActions();
        }

        private void File_Changed(object sender, EventArgs e) { RefreshActions(); }

        public void RefreshActions()
        {
            List.Items.Clear();
            RefreshActions(OwnerWindow.ContextManager.File, 0);
        }

        private void RefreshActions(Actions.Action action, int indent)
        {
            List.Items.Add(new ListViewItem() { Name = action.GetHashCode().ToString(), Text = action.GetType().Name, Tag = action, IndentCount = indent });
            var pa = action as Actions.ParentAction;
            if (pa == null) return;
            foreach (var v in pa.Children) RefreshActions(v, indent + 1);
        }

        public void RemoveSelected()
        {
            foreach (var v in SelectedActions)
                if (v.Parent != null) v.Parent.Children.Remove(v);
        }

        private void Analyze()
        {
            if (BrowserDoc == null) return;
            foreach(ListViewItem item in List.Items)
            {
                if (item.SubItems.Count < 2) item.SubItems.Add(new ListViewItem.ListViewSubItem());
                item.SubItems[1].Text = (item.Tag as Actions.Action).GetItemCount(BrowserDoc.Context).ToString();
            }
        }
    }

    public class PropertiesStrip : Strip
    {
        public object[] SelectedObjects { get { return PropertyGrid.SelectedObjects; } set { PropertyGrid.SelectedObjects = value; } }
        public PropertyGrid PropertyGrid { get { return base.Control as PropertyGrid; } }
        public override string Title { get { return "Properties"; } }
        public PropertiesStrip(AppWindow owner) : base(owner, new PropertyGrid() { Dock = DockStyle.Fill }) { }
    }

    public class LogAndDataStrip : Strip
    {
        public override string Title { get { return "Files And Log"; } }
        public TextBox LogTextBox { get; private set; }
        public TabControl TabControl { get; private set; }
        public void LogWrite(string value) { LogTextBox.AppendText(value); }

        private class TableTab : TabPage
        {
            public event EventHandler Changed;
            private DateTime _lastGridUpdate;
            public DataGrid GridView { get; private set; }
            private System.Data.DataTable _table;
            public System.Data.DataTable Table
            {
                get { return _table; }
                set {
                    if (_table != null) _table.RowChanged -= CurTable_RowChanged;
                    _table = value;
                    if (_table != null) _table.RowChanged += CurTable_RowChanged;
                    GridView.DataSource = _table;
                }
            }

            private void CurTable_RowChanged(object sender, System.Data.DataRowChangeEventArgs e)
            {
                if (!(e.Action == System.Data.DataRowAction.Add || e.Action == System.Data.DataRowAction.Delete)) return;
                if ((DateTime.Now - _lastGridUpdate).Seconds < 1) return;
                _lastGridUpdate = DateTime.Now;
                GridView.Refresh();
                Changed?.Invoke(this, EventArgs.Empty);
            }

            public TableTab(string tableName): base(tableName)
            {
                GridView = new DataGrid() {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    CaptionVisible = false,
                };
                Controls.Add(GridView);
            }

            protected override void Dispose(bool disposing)
            {
                if (_table != null) Table = null;
                base.Dispose(disposing);
            }
        }

        public LogAndDataStrip(AppWindow owner) : base(owner, new TabControl() { Dock = DockStyle.Fill })
        {
            TabControl = base.Control as TabControl;
            AutoSize = true;
            LogTextBox = new TextBox() { ReadOnly = true, Dock = DockStyle.Fill, Multiline = true, WordWrap = false, ScrollBars = ScrollBars.Both };
            var logPage = new TabPage("Log");
            logPage.Controls.Add(LogTextBox);
            TabControl.Controls.Add(logPage);
            TabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            OwnerWindow.DocumentCreated += OwnerWindow_DocumentCreated;
            OwnerWindow.ContextManager.DataSet.Tables.CollectionChanged += Tables_CollectionChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                OwnerWindow.DocumentCreated -= OwnerWindow_DocumentCreated;
                var dataSet = OwnerWindow?.ContextManager?.DataSet;
                if (dataSet != null) dataSet.Tables.CollectionChanged -= Tables_CollectionChanged;
            }
            base.Dispose(disposing);
        }

        private void Tables_CollectionChanged(object sender, System.ComponentModel.CollectionChangeEventArgs e)
        {
            SetupDataTables();
        }

        private void SetupDataTables()
        {
            while (TabControl.Controls.Count > 1)
            {
                var tab = TabControl.Controls[TabControl.Controls.Count - 1] as TableTab;
                TabControl.Controls.Remove(tab);
                tab.Dispose();
            }
            if (OwnerWindow.ContextManager.DataSet == null) return;
            foreach (System.Data.DataTable t in OwnerWindow.ContextManager.DataSet.Tables)
            {
                var page = new TableTab(t.TableName) { Table = t };
                page.Changed += TableTab_Changed;
                TabControl.Controls.Add(page);
            }
        }

        private void TableTab_Changed(object sender, EventArgs e)
        {
            if (sender == TabControl.SelectedTab)
                TabControl_SelectedIndexChanged(TabControl, EventArgs.Empty);
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            System.Data.DataTable dataTable = null;
            if (TabControl.SelectedIndex > 0) dataTable = (TabControl.SelectedTab as TableTab)?.Table;
            if (dataTable != null) OwnerWindow.RowCount = dataTable.Rows.Count.ToString() + " rows";
            else OwnerWindow.RowCount = "";
        }

        private void OwnerWindow_DocumentCreated(object sender, DocumentEventArgs e)
        {
            var browserDoc = e.Document as BrowserDocument;
            if (browserDoc == null) return;
            browserDoc.Context.Logger += Context_Logger;
            browserDoc.FormClosed += (s, a)=> browserDoc.Context.Logger -= Context_Logger;
        }

        private void Context_Logger(object sender, Core.Context.LogEventArgs e)
        {
            LogTextBox.AppendText(string.Format("{0}\t{1}\r\n", e.Category, e.Text));
        }

        public void RefreshDataTables()
        {
            for (int i = 1; i < TabControl.Controls.Count; i++)
                (TabControl.Controls[i] as TableTab).Refresh();
            TabControl_SelectedIndexChanged(TabControl, EventArgs.Empty);
        }


    }
}
