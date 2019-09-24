using CefSharp;
using CefSharp.WinForms.Internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcher2.Core
{
    public enum ContextState { Stop, Paly, Pause  }
    public sealed class Context : IDisposable
    {
        public class WrongStateException : ApplicationException
        {
            public WrongStateException(string message = null)
                : base(message ?? "Exec Context is in wrong state") { }
        }

        public class LogEventArgs : EventArgs
        {
            public string Category;
            public string Text;
        }

        public class StepEventArgs : EventArgs
        {
            public Actions.Action Action { get; private set; }
            public int Count { get; private set; }
            public int Index { get; set; }
            public StepEventArgs(Actions.Action action, int count) { Action = action; Count = count; }
        }

        public event EventHandler<UnhandledExceptionEventArgs> Exception;
        public event EventHandler<StepEventArgs> StepChanged;
        public event EventHandler StateChanged;
        public event EventHandler<LogEventArgs> Logger;

        public ContextManager Manager { get; private set; }
        public IWebBrowser Browser { get; private set; }
        public Actions.Action CurrentAction { get; private set; }

        private Dictionary<string, Record> _records;
        private System.Threading.AutoResetEvent _continueEvent;
        private System.Threading.ManualResetEvent _breakEvent;
        private System.Threading.ManualResetEvent _frameLoadEvent;
        private System.Threading.ManualResetEvent _pageLoadEvent;
        private Task _executeTask;
        private long _state = (long)ContextState.Stop;
        private long _isWait = 0;
        private bool _skipSelfExecute = false;

        public ContextState State
        {
            get {
                return (ContextState)System.Threading.Interlocked.Read(ref _state);
            }
            private set {
                System.Threading.Interlocked.Exchange(ref _state, (long)value);
                Manager.UIThreadCall(() => StateChanged?.Invoke(this, EventArgs.Empty));
            }
        }

        public bool IsWait
        {
            get { return System.Threading.Interlocked.Read(ref _isWait) != 0; }
            private set { System.Threading.Interlocked.Exchange(ref _isWait, value ? 1 : 0); }
        }

        public Context(IWebBrowser browser, ContextManager manager)
        {
            _records = new Dictionary<string, Record>();
            _continueEvent = new System.Threading.AutoResetEvent(true);
            _breakEvent = new System.Threading.ManualResetEvent(false);
            _frameLoadEvent = new System.Threading.ManualResetEvent(false);
            _pageLoadEvent = new System.Threading.ManualResetEvent(false);
            Manager = manager;
            Browser = browser;
            browser.LoadingStateChanged += Browser_LoadingStateChanged;
            browser.FrameLoadStart += Browser_FrameLoadStart;
            browser.FrameLoadEnd += Browser_FrameLoadEnd;
        }

        void IDisposable.Dispose()
        {
            if (IsRunning) { Stop(); _executeTask.Wait(); }
            if (Browser != null)
            {
                Browser.LoadingStateChanged -= Browser_LoadingStateChanged;
                Browser.FrameLoadStart -= Browser_FrameLoadStart;
                Browser.FrameLoadEnd -= Browser_FrameLoadEnd;
                Browser = null;
            }
            if (_executeTask != null) {_executeTask.Dispose(); _executeTask = null; }
            if (_breakEvent != null) { _continueEvent.Dispose(); _continueEvent = null; }
            if (_continueEvent != null) {_continueEvent.Dispose(); _continueEvent = null; }
            if (_frameLoadEvent != null) { _frameLoadEvent.Dispose(); _frameLoadEvent = null; }
            if (_pageLoadEvent != null) { _pageLoadEvent.Dispose(); _pageLoadEvent = null; }
        }

        public object Eval(string script)
        {
            var r = Browser.GetMainFrame().EvaluateScriptAsync(script).Result;
            if (!r.Success) throw new Exception(r.Message);
            return r.Result;
        }

        public void Log(string data, string cetegory = null)
        {
            var args = new LogEventArgs() { Category = cetegory, Text = data };
            if (Logger != null) Manager.UIThreadCall(() => Logger.Invoke(this, args));
        }

        private Record GetRecord(string record)
        {
            Record ret;
            System.Data.DataTable table;
            if (!Manager.DataSet.Tables.Contains(record)) Manager.DataSet.Tables.Add(record);
            table = Manager.DataSet.Tables[record];
            if (_records.TryGetValue(record, out ret)) return ret;
            ret = new Record(table);
            _records.Add(record, ret);
            return ret;
        }

        public void Record(string record)
        {
            Manager.UIThreadCall(() => GetRecord(record).NextRow());
        }

        public void Field(string record, string field, string value, bool isUnique = false)
        {
            Manager.UIThreadCall(() => GetRecord(record).SetField(field, value, isUnique));
        }

        public Context PushContext(int timeout = -1)
        {
            System.Diagnostics.Debug.WriteLine("PushContext");
            return Manager.GetContext(this, timeout);
        }

        public void LoadUrl(string href)
        {
            System.Diagnostics.Debug.WriteLine("LoadUrl " + href);
            Browser.Load(href);
        }

        internal bool Wait(System.Threading.WaitHandle handle, int timeout)
        {
            var handles = handle == null ?
                new System.Threading.WaitHandle[] { _breakEvent } :
                new System.Threading.WaitHandle[] { _breakEvent, handle };
            var startWait = DateTime.Now;
            do {
                IsWait = true;
                int ret = System.Threading.WaitHandle.WaitAny(handles, timeout);
                IsWait = false;
                if (ret == System.Threading.WaitHandle.WaitTimeout) return (handle == null);
                else if (ret == 1) return true;
                else if (ret == 0) {
                    if (State == ContextState.Pause) _continueEvent.WaitOne();
                    if (State == ContextState.Stop) return false;
                    timeout -= (DateTime.Now - startWait).Milliseconds;
                }
            } while (timeout >= 0);
            return false;
        }

        public void Sleep(int milliseconds) { Wait(null, milliseconds); }
        public bool WaitPage(int timeout = -1) { System.Threading.Thread.Sleep(100); return Wait(_pageLoadEvent, timeout); }
        public bool WaitFrame(int timeout = -1) { System.Threading.Thread.Sleep(100); return Wait(_frameLoadEvent, timeout); }

        private void Browser_FrameLoadStart(object sender, FrameLoadStartEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Browser_FrameLoadStart " + e.Url);
            _frameLoadEvent.Reset();
        }

        private void Browser_FrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Browser_FrameLoadEnd " + e.Url);
            _frameLoadEvent.Set();
        }

        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("LoadingStateChanged " + e.IsLoading);
            if (e.IsLoading) _pageLoadEvent.Reset(); else _pageLoadEvent.Set();
        }

        private void Execute(object data)
        {
            try {
                var action = CurrentAction;
                actionStart:
                var ret = _skipSelfExecute ? ExecuteChildren(action as Actions.ParentAction, data) : Execute(action, data);
                _skipSelfExecute = false;
                if (ret is BreakValue br && br.Mode == BreakValue.BreakMode.Goto)
                {
                    if (string.IsNullOrEmpty(br.GotoActionID)) action = Manager.File;
                    else action = null;// CurrentFile.ActionByID(br.GotoActionID);
                    if (action == null) throw new Exception("Goto action target not found");
                    goto actionStart;
                }
            } catch (Exception ex) {
                Manager.UIThreadCall(() => Exception?.Invoke(this, new UnhandledExceptionEventArgs(ex, false)));
            } finally {
                Manager.UIThreadCall(() => {
                    foreach (var v in _records) v.Value.NextRow();
                    _records.Clear();
                });
                State = ContextState.Stop;
            }
        }

        private object ExecuteChildren(Actions.ParentAction action, object data)
        {
            foreach (var v in action.Children)
            {
                if (State == ContextState.Stop) return null;
                var cret = Execute(v, data);
                if (cret is BreakValue bv) {
                    if (bv.Mode == BreakValue.BreakMode.Break) { 
                        if (string.IsNullOrEmpty(bv.GotoActionID)) break;
                        if (bv.GotoActionID != action.ID) break;
                    }
                    return bv;
                }
            }
            return data;
        }

        private object Execute(Actions.Action action, object data)
        {
            object ret = null;
            if (action.Skip) return null;
            CurrentAction = action;
            var count = action.GetItemCount(this, data);
            var sea = new StepEventArgs(action, count);
            for (int i = 0; i < count; i++)
            {
                sea.Index = i;
                Manager.UIThreadCall(() => StepChanged?.Invoke(this, sea));
                if (State == ContextState.Stop) return null;
                else if (State == ContextState.Pause) _continueEvent.WaitOne();
                ret = action.Execute(this, i, data);
                if (ret is BreakValue) return ret;
                if (action is Actions.ParentAction pa && !(action is Actions.Window))
                    ExecuteChildren(pa, ret);
            }
            return ret;
        }

        private bool IsRunning { get { lock(this) return (_executeTask != null) && (_executeTask.Status <= TaskStatus.Running); } }

        public void Play(Actions.Action action, object data, bool skipSelf, bool startPauses)
        {
            if (IsRunning) throw new WrongStateException();
            _skipSelfExecute = skipSelf;
            CurrentAction = action;
            State = startPauses ? ContextState.Pause : ContextState.Paly;
            if (startPauses) _continueEvent.Reset(); else _continueEvent.Set();
            if (_executeTask != null) _executeTask.Dispose();
            _breakEvent.Reset();
            lock (this) {
                _executeTask = new Task(Execute, data);
                _executeTask.Start();
            }
        }

        public void Play()
        {
            if (IsRunning) {
                State = ContextState.Paly;
                _breakEvent.Reset();
                _continueEvent.Set();
            } else Play(Manager.File, null, false, false);
        }

        public void Step()
        {
            if (IsRunning) {
                _breakEvent.Reset();
                _continueEvent.Set();
            } else Play(Manager.File, null, false, true); 
        }

        public void Break()
        {
            if (!IsRunning) throw new WrongStateException();
            State = ContextState.Pause;
            _continueEvent.Reset();
            _breakEvent.Set();
        }

        public void Stop()
        {
            if (!IsRunning) throw new WrongStateException();
            State = ContextState.Stop;
            _breakEvent.Set();
            _continueEvent.Set();
        }

        public void WaitComplete(int timeout = -1)
        {
            var task = _executeTask;
            task.Wait(timeout);
        }
    }

    public class Record
    {
        public System.Data.DataTable Table { get; private set; }
        public System.Data.DataRow Row { get; private set; }
        private List<Tuple<string, Func<System.Data.DataRow, System.Data.DataColumn, bool>>> _validators = new List<Tuple<string, Func<System.Data.DataRow, System.Data.DataColumn, bool>>>();

        public Record(System.Data.DataTable table) { Table = table; }
        public bool IsValid(System.Data.DataRow row)
        {
            foreach (var v in _validators)
            {
                if (v.Item2 == null) continue;
                if (!v.Item2(row, row.Table.Columns[v.Item1])) return false;
            }
            return true;
        }
        public void ClearValidators() { _validators.Clear(); }
        public void AddValidator(string field, Func<System.Data.DataRow, System.Data.DataColumn, bool> vaidation)
        {
            _validators.Add(new Tuple<string, Func<System.Data.DataRow, System.Data.DataColumn, bool>>(field, vaidation));
        }

        public void SetField(string field, string value, bool isUnique = false)
        {
            if (!Table.Columns.Contains(field)) Table.Columns.Add(field);
            if (Row == null) Row = Table.NewRow();
            Row[field] += value;
            if (isUnique) AddValidator(field, (r, c) => (r.Table.FindValue(c, r[c]).Count == 0));
        }

        public void NextRow()
        {
            if (Row != null) {
                if (IsValid(Row)) Table.Rows.Add(Row);
                Row = null;
            }
            ClearValidators();
        }
    }

}
