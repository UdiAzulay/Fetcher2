using System;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;

namespace Fetcher2.Actions
{
    public class Record : Action
    {
        [XmlAttribute]
        public string Name { get; set; }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            context.Record(Name);
            return data;
        }
    }

    public class Field : Action
    {
        private static System.Text.RegularExpressions.Regex removeScript = new System.Text.RegularExpressions.Regex(@"<script([\S\s]*?)>([\S\s]*?)<\/script>");
        private static System.Text.RegularExpressions.Regex removeHtml = new System.Text.RegularExpressions.Regex(@"<[^>]*>");

        [XmlAttribute]
        public string RecordName { get; set; }
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute, DefaultValue(true)]
        public bool StripHtml { get; set; } = true;

        [XmlAttribute, DefaultValue(false)]
        public bool FixWhiteSpaces { get; set; }

        [XmlAttribute, DefaultValue(false)]
        public bool IsUnique { get; set; }

        [DefaultValue("")]
        public string TextAnchorBegin { get; set; }
        [DefaultValue("")]
        public string TextAnchorEnd { get; set; }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            var dataString = ((data is Core.ElementReference el) ? el.Eval(context, "outerHTML") : data)?.ToString();
            dataString = ProcessText(dataString);
            context.Field(RecordName, Name, dataString, IsUnique);
            return dataString;
        }

        public string ProcessText(string value)
        {
            if (value == null) return null;
            int startIndex = 0, endIndex = -1;
            if (!string.IsNullOrEmpty(TextAnchorBegin))
            {
                startIndex = value.IndexOf(TextAnchorBegin);
                if (startIndex < 0) return null;
                startIndex += TextAnchorBegin.Length;
            }
            if (!string.IsNullOrEmpty(TextAnchorEnd)) endIndex = value.IndexOf(TextAnchorEnd, startIndex);
            if (endIndex < 0) endIndex = value.Length;
            var res = value.Substring(startIndex, endIndex - startIndex).Trim();
            if (StripHtml)
            {
                res = removeScript.Replace(res, "");
                res = removeHtml.Replace(res, "");
            }
            if (FixWhiteSpaces)
            {
                StringBuilder sb = new StringBuilder();
                bool lastIsSpace = true;
                foreach (var v in res)
                {
                    var isWhite = char.IsWhiteSpace(v);
                    if (!isWhite || !lastIsSpace) sb.Append(v);
                    lastIsSpace = isWhite;
                }
                res = sb.ToString();
            }
            return res?.Trim();
        }
    }
    
    public class Dictionary : ParentAction
    {
        public enum ValueAction { Exist, NotExist, Add, Remove, }
        [XmlAttribute]
        public string GroupName { get; set; }

        [XmlAttribute, DefaultValue(ValueAction.Exist)]
        public ValueAction Action { get; set; }

        protected string FormatGroupName(object data) { return string.Format("{0}-{1}", GroupName, data?.ToString()); }
        public override int GetItemCount(Core.Context context, object data = null)
        {
            var key = FormatGroupName(data);
            switch (Action)
            {
                case ValueAction.Exist:
                case ValueAction.Remove: lock (context.Values) return context.Values.Contains(key) ? 1 : 0; 
                case ValueAction.NotExist:
                case ValueAction.Add: lock (context.Values) return context.Values.Contains(key) ? 0 : 1;
            }
            return base.GetItemCount(context, data);
        }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            var key = FormatGroupName(data);
            switch (Action)
            {
                case ValueAction.Add: lock (context.Values) context.Values.Add(key); break;
                case ValueAction.Remove: lock (context.Values) context.Values.Remove(key); break;
            }
            return data;
        }
    }

}
