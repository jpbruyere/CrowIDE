using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Crow;

namespace Crow.Coding.Debugging
{
	public class DebuggerObject : IValueChange {
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public void NotifyValueChanged(string MemberName, object _value) 				
			=> ValueChanged.Raise(this, new ValueChangeEventArgs(MemberName, _value));
		
		public void NotifyValueChanged(object _value, [CallerMemberName] string caller = null)
			=> NotifyValueChanged(caller, _value);
		#endregion
		
	}

	[DebuggerDisplay("{Id}:{Name} Running:{IsRunning})")]
	public class ThreadInfo
	{
		public int Id;
		public string Name;
		public bool IsStopped;
		public bool IsRunning => !IsStopped;

		public ThreadInfo(MITupple frame)
		{
			Id = int.Parse(frame.GetAttributeValue("id"));
			Name = frame.GetAttributeValue("name");
			IsStopped = frame.GetAttributeValue("state") == "stopped";
		}
		public override string ToString() => $"{Id}:{Name} Running:{IsRunning})";
	}

	public class CLRAddress
	{
		public string ModuleID;
		public string MethodToken;
		public long IlOffset;
		public long NativeOffset;
		public CLRAddress(MITupple clrAddress)
		{
			ModuleID = clrAddress.GetAttributeValue("module-id");
			MethodToken = clrAddress.GetAttributeValue("method-token");
			IlOffset = long.Parse(clrAddress.GetAttributeValue("il-offset"));
			NativeOffset = long.Parse(clrAddress.GetAttributeValue("native-offset"));
		}
	}

	[DebuggerDisplay("{level}:{File}({Line},{Column})")]
	public class StackFrame
	{
		public int Level;
		public string File;
		public string FileFullName;
		public int Line;
		public int Column;
		public int LineEnd;
		public int ColumnEnd;
		public string Function;
		public string Address;

		public bool IsDefined => !string.IsNullOrEmpty(File);
		public bool HasCLRAddress => ClrAddress != null;
		public CLRAddress ClrAddress;

		public StackFrame(MITupple frame)
		{
			if (frame.TryGetAttributeValue("level", out MIAttribute level))
				this.Level = int.Parse(level.Value);
			File = frame.GetAttributeValue("file");
			FileFullName = frame.GetAttributeValue("fullname")?.Replace("\\\\", "\\");
			int.TryParse(frame.GetAttributeValue("line"), out Line);
			int.TryParse(frame.GetAttributeValue("col"), out Column);
			int.TryParse(frame.GetAttributeValue("end-line"), out LineEnd);
			int.TryParse(frame.GetAttributeValue("end-col"), out ColumnEnd);
			Function = frame.GetAttributeValue("func");
			Address = frame.GetAttributeValue("addr");
			MITupple clrAddrs = frame["clr-addr"] as MITupple;
			if (clrAddrs != null)
				ClrAddress = new CLRAddress(clrAddrs);
		}
		public override string ToString() => $"{Level}:{File}({Line},{Column} {Function})";
	}
}