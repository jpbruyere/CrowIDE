// Copyright (c) 2013-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System.Diagnostics;

namespace Crow.Coding.Debugging
{
	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
	public class BreakPoint : DebuggerObject
	{
		int index = -1;
		public string Func;
		string fileName;
		string fileFullName;
		int line;
		bool isEnabled;

		string type;
		string disp;
		string warning;

		public CSProjectItem File;
		public int Index {
			get => index;
			set {
				if (index == value)
					return;
				index = value;
				NotifyValueChanged(index);
			}
		}
		public int Line {
			get => line;
			set {
				if (line == value)
					return;
				line = value;
				NotifyValueChanged (line);
			}
		}
		public bool IsEnabled {
			get => isEnabled;
			set {
				if (isEnabled == value)
					return;
				isEnabled = value;
				NotifyValueChanged (isEnabled);
			}
		}
		public string Type {
			get => type;
			set {
				if (type == value)
					return;
				type = value;
				NotifyValueChanged (type);
			}
		}
		public string Disp {
			get => disp;
			set {
				if (disp == value)
					return;
				disp = value;
				NotifyValueChanged (disp);
			}
		}
		public string Warning {
			get => warning;
			set {
				if (warning == value)
					return;
				warning = value;
				NotifyValueChanged (warning);
			}
		}
		public string FileName {
			get => fileName;
			set {
				if (fileName == value)
					return;
				fileName = value;
				NotifyValueChanged (fileName);
			}
		}
		public string FileFullName {
			get => fileFullName;
			set {
				if (fileFullName == value)
					return;
				fileFullName = value;
				NotifyValueChanged (fileFullName);
			}
		}

		public BreakPoint(CSProjectItem file, int line, bool isEnabled = true)
		{
			File = file;
			Line = line;
			IsEnabled = isEnabled;
		}

		public void UpdateLocation (StackFrame frame) {
			FileName = frame.File;
			FileFullName = frame.FileFullName;
			Func = frame.Function;
			Line = frame.Line - 1;
		}
		public void Update (ProjectView project, MITupple bkpt) {
			Index = int.Parse (bkpt.GetAttributeValue("number"));
			Type = bkpt.GetAttributeValue("type");
			Disp = bkpt.GetAttributeValue("disp");
			IsEnabled = bkpt.GetAttributeValue("enabled") == "y";
			if (bkpt.TryGetAttributeValue("warning", out string warning))
				Warning = warning;
			else {
				Warning = null;
				Func = bkpt.GetAttributeValue("func");
				FileName = bkpt.GetAttributeValue("file");
				FileFullName = bkpt.GetAttributeValue("fullname")?.Replace("\\\\", "\\");
				if (project.TryGetProjectFileFromPath(FileFullName, out ProjectFileNode pf))
					File = pf as CSProjectItem;
				Line = int.Parse (bkpt.GetAttributeValue("line")) - 1;
			}			
		}

		public override string ToString() => $"{Index}:{Type} {File.FullPath}:{Line} enabled:{IsEnabled}";

		private string GetDebuggerDisplay() => ToString();
	}
}
