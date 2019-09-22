using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcher2.Core
{
    public class ContextManager : IDisposable
    {
        public delegate Context CreateContext(bool acquire);
        public event EventHandler Changed;

        private System.Threading.Semaphore _useContexts;
        private CreateContext _createContext;
        private HashSet<Context> _contexts = new HashSet<Context>();
        private HashSet<Context> _freeContexts = new HashSet<Context>();

        public ContextManager(CreateContext createContext, int maxItems)
        {
            _createContext = createContext;
            SetMaxItem(maxItems);
        }

        public void Dispose()
        {
            if (_useContexts != null) { _useContexts.Dispose(); _useContexts = null; }
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
            foreach (var v in _contexts)
            {
                try { if (v.State != ContextState.Stop) v.Stop(); }
                catch { }
            }
        }

        public void BreakAll()
        {
            foreach (var v in _contexts) { 
                try { if (v.State != ContextState.Stop) v.Break(); }
                catch { }
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
            //source.Wait(_useContexts, timeout)
            if (!_useContexts.WaitOne(timeout)) return null;
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
    }
}
