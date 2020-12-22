// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;

namespace Crow.Text
{
	public class TextBufferEventArgs : EventArgs {
		public int LineStart;
		public int LineCount;

		public TextBufferEventArgs(int lineNumber) {
			LineStart = lineNumber;
			LineCount = 1;
		}
		public TextBufferEventArgs(int lineStart, int lineCount) {
			LineStart = lineStart;
			LineCount = lineCount;
		}
	}

}

