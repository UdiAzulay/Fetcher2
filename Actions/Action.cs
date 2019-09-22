using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Fetcher2.Actions
{
    public class ActionList : ICollection<Action>
    {
        public ParentAction Owner { get; private set; }
        private List<Action> _items = new List<Action>();
        public ActionList(ParentAction owner) { Owner = owner; }
        public int Count { get { return _items.Count; } }
        public bool IsReadOnly { get { return false; } }
        public void Add(Action item)
        {
            if (item.Parent != null) item.Parent.Children.Remove(item);
            _items.Add(item);
            Owner.OnChildAdded(item);
        }

        public bool Remove(Action item)
        {
            if (item.Parent != Owner) return false;
            _items.Remove(item);
            Owner.OnChildRemoved(item);
            return true;
        }

        public void Clear()
        {
            foreach (var v in _items) Owner.OnChildRemoved(v);
            _items.Clear();
        }

        public bool Contains(Action item) { return _items.Contains(item); }
        public void CopyTo(Action[] array, int arrayIndex) { _items.CopyTo(array, arrayIndex); }
        public IEnumerator<Action> GetEnumerator() { return _items.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return _items.GetEnumerator(); }
    }

    public abstract class Action
    {
        [DefaultValue(""), XmlAttribute, Category("General")]
        public string ID { get; set; }
        [DefaultValue(""), Category("General")]
        public string Comment { get; set; }

        [XmlIgnore, Category("General")]
        public ParentAction Parent { get; protected internal set; }

        [XmlAttribute, DefaultValue(false), Category("Debug")]
        public bool Skip { get; set; }

        [XmlAttribute, DefaultValue(false), Category("Debug")]
        public bool Log { get; set; }
        [XmlAttribute, DefaultValue(false), Category("Debug")]
        public bool Break { get; set; }
        [XmlAttribute, DefaultValue(false), Category("Debug")]
        public bool Beep { get; set; }


        public virtual int GetItemCount(Core.Context context, object data = null) { return 1; }

        public object Execute(Core.Context context, int index, object data)
        {
            if (Break) context.Break();
            if (Beep) System.Media.SystemSounds.Beep.Play();
            var ret = OnExecute(context, index, data);
            if (Log) context.Log(ret?.ToString() ?? "[null]", ID);
            return ret;
        }
        protected abstract object OnExecute(Core.Context context, int index, object data);

        public static Action Create(string actionName)
        {
            return File.ActionsTypes.SingleOrDefault(v => v.Name == actionName).GetConstructor(new Type[] { }).Invoke(null) as Action;
        }

        protected virtual void OnRaiseChanged()
        {
            for (Action action = Parent; action != null; action = action.Parent) {
                if (action is File)
                {
                    action.OnRaiseChanged();
                    return;
                }
            }
        }
    }

    public abstract class ParentAction : Action
    {
        [Browsable(false)]
        public ActionList Children { get; private set; }
        public virtual bool CanHaveChild(Action action) { return true; }
        protected ParentAction() { Children = new ActionList(this); }
        protected internal void OnChildAdded(Action action)
        {
            action.Parent = this;
            OnRaiseChanged();
        }

        protected internal void OnChildRemoved(Action action)
        {
            action.Parent = null;
            OnRaiseChanged();
        }
    }
}
