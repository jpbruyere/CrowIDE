// Copyright (c) 2013-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System.Diagnostics;

namespace Crow.Coding.Debugging
{
	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
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
		string GetDebuggerDisplay() => ToString();
	}			
}
