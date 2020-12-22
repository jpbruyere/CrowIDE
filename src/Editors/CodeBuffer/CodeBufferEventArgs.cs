// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;

namespace Crow.Coding
{
	public class CodeBufferEventArgs : EventArgs {
		public int LineStart;
		public int LineCount;

		public CodeBufferEventArgs(int lineNumber) {
			LineStart = lineNumber;
			LineCount = 1;
		}
		public CodeBufferEventArgs(int lineStart, int lineCount) {
			LineStart = lineStart;
			LineCount = lineCount;
		}
	}

}

