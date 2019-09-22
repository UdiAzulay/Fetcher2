using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcher2.Core
{
    public class ContextManager : IDisposable
    {
        public class FileChangedEventArgs : EventArgs
        {
            public Actions.File OldValue { get; private set; }
            public FileChangedEventArgs(Actions.File oldValue) { OldValue = oldValue; }
        }
        public delegate Context CreateContext(bool acquire);

        public event EventHandler<FileChangedEventArgs> FileChanged;
        public event EventHandler Changed;

        public System.Data.DataSet DataSet { get; private set; }
        public HashSet<string> Values { get; private set; }
        public Action<Action> UIThreadCall { get; private set; }

        private System.Threading.Semaphore _useContexts;
        private CreateContext _createContext;
        private HashSet<Context> _contexts = new HashSet<Context>();
        private HashSet<Context> _freeContexts = new HashSet<Context>();


        private Actions.File _file;
        public Actions.File File
        {
            get { return _file; }
            set
            {
                var ea = new FileChangedEventArgs(_file);
                _file = value;
                FileChanged?.Invoke(this, ea);
            }
        }

        public ContextManager(CreateContext createContext, Action<Action> uiThreadSync = null, int maxItems = 9)
        {
            _createContext = createContext;
            UIThreadCall = uiThreadSync ?? ((a) => a());
            DataSet = new System.Data.DataSet();
            Values = new HashSet<string>();
            SetMaxItem(maxItems);
        }

        public void Dispose()
        {
            if (_useContexts != null) { _useContexts.Dispose(); _useContexts = null; }
            if (DataSet != null) { DataSet.Dispose(); DataSet = null; }
        }

        public void SetMaxItem(int maxItems)
        {
            lock (this) {
                if (_freeContexts.Count != _contexts.Count) throw new Exception("ContextManager can't set MaxItem while contexts running");
                if (_useContexts != null) _useContexts.Dispose();
                _useContexts = null;
                _useContexts = new System.Threading.Semaphore(maxItems, maxItems);
            }
        }

        public void StopAll()
        {
            for(var i = 0; i < 2; i++) { 
                foreach (var v in _contexts)
                {
                    try { if (v.State != ContextState.Stop) v.Stop(); }
                    catch { }
                }
                System.Threading.Thread.Sleep(300);
            }
        }

        public void BreakAll()
        {
            for (var i = 0; i < 2; i++)
            {
                foreach (var v in _contexts)
                {
                    try { if (v.State != ContextState.Stop) v.Break(); }
                    catch { }
                }
                System.Threading.Thread.Sleep(300);
            }
        }

        public void ContinueAll()
        {
            foreach (var v in _contexts)
            {
                try { if (v.State == ContextState.Pause) v.Play(); }
                catch { }
            }
        }

        public void AddContext(Context context, bool acquired)
        {
            lock (this) _contexts.Add(context);
            if (context.State == ContextState.Stop && !acquired)
                lock (this) _freeContexts.Add(context);
            context.StateChanged += Context_StateChanged;
        }

        public void RemoveContext(Context context)
        {
            lock (this) { 
                if (_contexts.Remove(context))
                    context.StateChanged -= Context_StateChanged;
                _freeContexts.Remove(context);
            }
        }

        private void Context_StateChanged(object sender, EventArgs e)
        {
            var context = sender as Context;
            bool success = false;
            if (context.State == ContextState.Stop) {
                lock (this) success = _freeContexts.Add(context);
                if (success) _useContexts.Release();
            } else {
                lock (this) success = _freeContexts.Remove(context);
                if (success) _useContexts.WaitOne();
            }
            if (success) Changed?.Invoke(this, EventArgs.Empty);
        }

        private Context PopContext()
        {
            Context ret = null;
            lock (this) {
                if (_freeContexts.Count > 0)
                {
                    ret = _freeContexts.First();
                    _freeContexts.Remove(ret);
                }
            }
            return ret;
        }

        public Context GetContext(Context source, int timeout = -1)
        {
            if (!source.Wait(_useContexts, timeout)) return null;
            Context ret = PopContext();
            if (ret == null) {
                ret = _createContext(true);
                if (ret != null) {
                    System.Threading.Thread.Sleep(100);
                    if (ret.Browser.IsLoading) ret.WaitPage();
                } else _useContexts.Release();
            }
            if (ret != null) Changed?.Invoke(this, EventArgs.Empty);
            return ret;
        }

        public int ContextCount { get { lock (this) return _contexts.Count; } }
        public int RuningContextCount { get { lock (this) return _contexts.Count - _freeContexts.Count; } }

        public void ClearData()
        {
            UIThreadCall(() => DataSet.Tables.Clear());
            lock (Values) Values.Clear();
        }
    }
}
