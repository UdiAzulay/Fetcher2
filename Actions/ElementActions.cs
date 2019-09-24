using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using System.Xml;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using CefSharp.WinForms.Internals;

namespace Fetcher2.Actions
{
    public abstract class ElementAction : ParentAction
    {
        [XmlElement(IsNullable = false)]
        public string Selector { get; set; }

        [XmlAttribute, DefaultValue(false)]
        public bool IsGlobal { get; set; }

        [XmlElement(IsNullable = false)]
        public string Validation { get; set; }

        [XmlAttribute, DefaultValue(0)]
        public int SkipItems { get; set; }

        [XmlAttribute, DefaultValue(1)]
        public int Every { get; set; } = 1;

        [XmlAttribute, DefaultValue(0)]
        public int MaxItems { get; set; }
        /*
        [XmlAttribute, DefaultValue(false)]
        public bool Desc { get; set; }
        */
        protected virtual object ExecuteElement(Core.Context context, Core.ElementReference element, object data) { return data; }

        public override int GetItemCount(Core.Context context, object data = null)
        {
            return new Core.ElementReference(Selector, IsGlobal ? null : data as Core.ElementReference).GetCount(context);
        }

        protected override object OnExecute(Core.Context context, int index, object data)
        {
            var elementReference = new Core.ElementReference(Selector, IsGlobal ? null : data as Core.ElementReference, index);
            return ExecuteElement(context, elementReference, data);
        }
    }

    public class Select : ElementAction
    {
        protected override object ExecuteElement(Core.Context context, Core.ElementReference element, object data) { return element; }
    }

    public class Call : ElementAction
    {
        [XmlAttribute]
        public string MethodName { get; set; }
        protected override object ExecuteElement(Core.Context context, Core.ElementReference element, object data)
        {
            return element.Eval(context, MethodName);
        }
    }

    public enum PropertyType { Property, Attribute, Style }
    public class GetProperty : ElementAction
    {
        [XmlAttribute, DefaultValue(PropertyType.Property)]
        public PropertyType PropertyType { get; set; }

        [XmlAttribute]
        public string PropertyName { get; set; }

        protected override object ExecuteElement(Core.Context context, Core.ElementReference element, object data)
        {
            string format = null;
            switch (PropertyType)
            {
                case PropertyType.Property: format = "{0}"; break;
                case PropertyType.Attribute: format = "attributes['{0}']"; break;
                case PropertyType.Style: format = "style['{0}']"; break;
            }
            return element.Eval(context, string.Format(format, PropertyName));
        }
    }

    public class SetProperty : ElementAction
    {
        [XmlAttribute, DefaultValue(PropertyType.Property)]
        public PropertyType PropertyType { get; set; }

        [XmlAttribute]
        public string PropertyName { get; set; }

        [XmlAttribute]
        public string Value { get; set; }
        protected override object ExecuteElement(Core.Context context, Core.ElementReference element, object data)
        {
            string format = null;
            switch (PropertyType)
            {
                case PropertyType.Property: format = "{0}"; break;
                case PropertyType.Attribute: format = "attributes['{0}']"; break;
                case PropertyType.Style: format = "style['{0}']"; break;
            }
            element.Eval(context, string.Format(format + " = '{1}'", PropertyName, Value));
            return data;
        }
    }

}
