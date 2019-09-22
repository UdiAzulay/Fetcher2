using System;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;

namespace Fetcher2.Actions
{
    public class File : ParentAction
    {
        private static XmlSerializer _serializer;
        public event EventHandler Changed;
        protected static XmlSerializer Serializer
        {
            get {
                if (_serializer != null) return _serializer;
                var xao = new XmlAttributeOverrides();
                var xmlAttrib = new XmlAttributes();
                foreach (var v in ActionsTypes) xmlAttrib.XmlArrayItems.Add(new XmlArrayItemAttribute(v));
                xao.Add(typeof(ParentAction), "Children", xmlAttrib);
                _serializer = new XmlSerializer(typeof(File), xao);
                return _serializer;
            }

        }

        protected override void OnRaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        [XmlIgnore]
        public string FileName { get; private set; }

        [XmlAttribute]
        public decimal Version { get; set; } = 1;

        [XmlAttribute]
        public int MaxWindows { get; set; } = 9;

        public string Url { get; set; }

        [DefaultValue(null), Category("output")]
        public string OutputPath { get; set; }

        [DefaultValue(false), Category("output")]
        public bool Append { get; set; }

        [DefaultValue(null)]
        public DateTime? LastExecute { get; set; }

        public static Type[] ActionsTypes
        {
            get {
                return new[] {
                    typeof(Wait), typeof(Break), typeof(Goto), typeof(Script),
                    typeof(Select), typeof(Call), typeof(GetProperty), typeof(SetProperty),
                    typeof(Navigate), typeof(Window),
                    typeof(Dictionary), typeof(Record), typeof(Field)
                };
            }
        }

        public static File Load(string fileName)
        {
            using (var s = new System.IO.FileStream(fileName, System.IO.FileMode.Open))
            {
                var ret = Serializer.Deserialize(s) as File;
                s.Close();
                ret.FileName = fileName;
                return ret;
            }
        }

        public void Save(string fileName = null)
        {
            using (var s = new System.IO.FileStream(fileName ?? FileName, System.IO.FileMode.Create))
            {
                using (var w = XmlWriter.Create(s, new XmlWriterSettings() { OmitXmlDeclaration = true, Indent = true }))
                {
                    Serializer.Serialize(w, this);
                    w.Close();
                }
                s.Close();
            }
            FileName = fileName;
        }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            return data;
        }
    }

    public class Script : ParentAction, Core.IScriptSource
    {
        private string _source;
        public string Source
        {
            get { return _source; }
            set {
                _source = value;
                if (!string.IsNullOrEmpty(FileName)) System.IO.File.WriteAllText(FileName, _source);
            }
        }
        public string FileName { get; set; }

        public Script() { }
        public Script(string fileName)
        {
            FileName = fileName;
            _source = System.IO.File.ReadAllText(FileName);
        }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            return context.Eval(Source);
        }
    }
}
