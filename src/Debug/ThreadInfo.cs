// Copyright (c) 2013-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System.Diagnostics;

namespace Crow.Coding.Debugging
{
	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
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
		string GetDebuggerDisplay() => ToString();
	}
}
