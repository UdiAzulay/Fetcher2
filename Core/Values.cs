using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetcher2.Core
{
    public interface IScriptSource
    {
        string Source { get; set; }
    }

    public class BreakValue
    {
        public enum BreakMode { Break, Goto }
        public BreakMode Mode { get; private set; }
        public string GotoActionID { get; private set; }
        public BreakValue(BreakMode mode, string toActionId = null) { Mode = mode; GotoActionID = toActionId; }
    }

    public class ElementReference
    {
        private string _parentExec = null;
        public string Selector { get; private set; }
        public int Index { get; private set; }
        public ElementReference(string selector, ElementReference parentElement, int index = 0)
        {
            Selector = selector;
            _parentExec = parentElement != null ? parentElement.ExecScript(parentElement.Index) : "document";
            Index = index;
        }

        private string ExecScript(int index)
        {
            if (string.IsNullOrEmpty(Selector)) return _parentExec;
            string indexSelector = (index > -1) ? "[" + index + "]" : "";
            return _parentExec + ".querySelectorAll('" + Selector + "')" + indexSelector;
        }

        private object Eval(Context context, string script, int index)
        {
            return context.Eval(ExecScript(index) + "." + script);
        }

        public object Eval(Context context, string script) { return Eval(context, script, Index); }
        public int GetCount(Context context)
        {
            if (string.IsNullOrEmpty(Selector)) return 1;
            return int.Parse(Eval(context, "length", -1)?.ToString());
        }

        public override string ToString() { return ExecScript(Index); }
    }
}
