using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Fetcher2.Actions
{
    public class Wait : Action
    {
        public enum WaitMode { Sleep, Frame, Page }
        [XmlAttribute, DefaultValue(WaitMode.Page)]
        public WaitMode Mode { get; set; }

        [XmlAttribute, DefaultValue(10000)]
        public int Miliseconds { get; set; } = 10000;

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            switch (Mode)
            {
                case WaitMode.Sleep: context.Sleep(Miliseconds); break;
                case WaitMode.Frame: context.WaitFrame(Miliseconds); break;
                case WaitMode.Page: context.WaitPage(Miliseconds); break;
            }
            return data;
        }
    }

    public class Goto : Action
    {
        [XmlAttribute]
        public string ActionID { get; set; }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            return new Core.BreakValue(Core.BreakValue.BreakMode.Goto, ActionID);
        }
    }

    public class Break : Action
    {
        public enum BreakMode { Action, Stop }

        [XmlAttribute, DefaultValue(BreakMode.Action)]
        public BreakMode Mode { get; set; }

        [XmlAttribute]
        public string ActionID { get; set; }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            switch (Mode)
            {
                case BreakMode.Stop: context.Stop(); return null;
                case BreakMode.Action: return new Core.BreakValue(Core.BreakValue.BreakMode.Break, ActionID); 
            }
            return data;
        }
    }

    
    public class Navigate : Action
    {
        [XmlAttribute, DefaultValue(-1)]
        public int Timeout { get; set; } = -1;

        [XmlAttribute, DefaultValue(true)]
        public bool Once { get; set; } = true;

        public static string GetHrefFromData(Core.Context context, object data)
        {
            if (data is Core.ElementReference el) return el.Eval(context, ".href")?.ToString();
            return data?.ToString();
        }

        public override int GetItemCount(Core.Context context, object data = null)
        {
            if (Once) { 
                var href = GetHrefFromData(context, data);
                if (!string.IsNullOrEmpty(href))
                    lock (context.Values) return context.Values.Contains("Navigate$" + href) ? 0 : 1;
            }
            return base.GetItemCount(context, data);
        }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            var href = GetHrefFromData(context, data);
            context.LoadUrl(href);
            if (Once) lock (context.Values) context.Values.Add("Navigate$" + href);
            context.WaitPage(Timeout);
            return href;
        }
    }

    public class Window : ParentAction
    {
        [XmlAttribute, DefaultValue(-1)]
        public int ContextTimeout { get; set; } = -1;

        [XmlAttribute, DefaultValue(false)]
        public bool Clone { get; set; }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            var href = Navigate.GetHrefFromData(context, data);
            var newContext = context.PushContext(ContextTimeout);
            if (Clone) {
                newContext.LoadUrl(context.Browser.Address);
                newContext.WaitPage();
            }
            newContext.Play(this, data, true, context.State == Core.ContextState.Pause);
            return null;
        }
    }

}
