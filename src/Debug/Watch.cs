using System;
using System.Diagnostics;
using static Crow.Coding.NetcoredbgDebugger;

namespace Crow.Coding.Debugging
{
	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
	public class Watch : DebuggerObject, ISelectable {
		#region ISelectable implementation
		bool isSelected;
		public event EventHandler Selected;
		public event EventHandler Unselected;
		
		public virtual bool IsSelected {
			get { return isSelected; }
			set {
				if (isSelected == value)
					return;
				isSelected = value;

				if (isSelected)
					Selected.Raise (this, null);
				else
					Unselected.Raise (this, null);

				NotifyValueChanged (isSelected);
			}
		}
		#endregion

		static int curId;
		NetcoredbgDebugger dbg;
		bool isExpanded;
		string name;
		string expression;
		string value;
		bool isEditable;
		int numChild;
		string type;
		int threadId;

		ObservableList<Watch> children = new ObservableList<Watch>();

		public CommandGroup Commands => new CommandGroup (
			new Command ("Update Value", () => UpdateValue()),
			new Command ("Delete", () => Delete())
		);

		public bool HasChildren => NumChild > 0;

		public bool IsExpanded {
			get => isExpanded;
			set {
				if (isExpanded == value)
					return;
				isExpanded = value;
				NotifyValueChanged(isExpanded);

				if (isExpanded && HasChildren && Children.Count == 0)
					dbg.WatchChildrenRequest (this);
			}
		}

		public ObservableList<Watch> Children {
			get => children;
			set {
				if (children == value)
					return;
				children = value;
				NotifyValueChanged (children);				
			}
		}
		public string Name {
			get => name;
			set {
				if (name == value)
					return;
				name = value;
				NotifyValueChanged(name);
			}
		}
		public string Expression {
			get => expression;
			set {
				if (expression == value)
					return;
				expression = value;
				NotifyValueChanged(expression);
			}
		}
		public string Value {
			get => value;
			set {
				if (this.value == value)
					return;
				this.value = value;
				NotifyValueChanged(this.value);
			}
		}
		public bool IsEditable {
			get => isEditable;
			set {
				if (isEditable == value)
					return;
				isEditable = value;
				NotifyValueChanged(isEditable);
			}
		}
		public int NumChild {
			get => numChild;
			set {
				if (numChild == value)
					return;
				numChild = value;
				NotifyValueChanged(numChild);
				NotifyValueChanged ("HasChildren", HasChildren);
			}
		}
		public string Type {
			get => type;
			set {
				if (type == value)
					return;
				type = value;
				NotifyValueChanged(type);
			}
		}
		public int ThreadId {
			get => threadId;
			set {
				if (threadId == value)
					return;
				threadId = value;
				NotifyValueChanged(threadId);
			}
		}

		public void Create()
		{
		}
		public void Delete()
		{			
			dbg.CreateNewRequest (new Request<Watch> (this, $"-var-delete {Name}"));
			dbg.Watches.Remove (this);
		}
		public void UpdateValue () {
			string strThread = dbg.CurrentThread == null ? "" : $"--thread {dbg.CurrentThread.Id}";
			string strLevel = dbg.CurrentFrame == null ? "" : $"--frame {dbg.CurrentFrame.Level}";
			dbg.CreateNewRequest (new Request<Watch> (this, $"-var-evaluate-expression {Name} {strThread} {strLevel}"));
			foreach (Watch w in Children)
				w.UpdateValue ();
		}
		public Watch(NetcoredbgDebugger debugger, string expression)
		{
			dbg = debugger;
			Name = $"watch_{curId++}";
			Expression = expression;
		}
		public Watch(NetcoredbgDebugger debugger, MITupple variable)
		{
			dbg = debugger;
			Update (variable);
		}
		public void Update (MITupple variable)
		{
			Name = variable.GetAttributeValue("name");
			Expression = variable.GetAttributeValue("exp");
			Value = variable.GetAttributeValue("value");
			IsEditable = variable.GetAttributeValue("attributes") == "editable";
			Type = variable.GetAttributeValue("type");
			NumChild = int.Parse(variable.GetAttributeValue("numchild"));
			ThreadId = int.Parse(variable.GetAttributeValue("thread-id"));
			NotifyValueChanged ("HasChildren", HasChildren);
		}


		public override string ToString() => $"{Name}:{Expression} = {Value} [{Type}]";
		string GetDebuggerDisplay() => ToString();
	}
}
