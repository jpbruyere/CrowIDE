// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Crow
{
	public static partial class Extensions
	{
		public static string GetIcon(this Widget go){
			return "#Icons." + go.GetType().FullName + ".svg";
		}
		public static List<Widget> GetChildren(this Widget go){
			Type goType = go.GetType();
			if (typeof (Group).IsAssignableFrom (goType))
				return (go as Group).Children;
			if (typeof(Container).IsAssignableFrom (goType))
				return new List<Widget>( new Widget[] { (go as Container).Child });
			if (typeof(TemplatedContainer).IsAssignableFrom (goType))
				return new List<Widget>( new Widget[] { (go as TemplatedContainer).Content });
			if (typeof(TemplatedGroup).IsAssignableFrom (goType))
				return (go as TemplatedGroup).Items;

			return new List<Widget>();
		}
		/*public static string TabulatedText (this SyntaxToken st, int tabSize, int currentColumn = 0) =>
			st.ToString ().TabulatedText (tabSize, currentColumn);*/
		public static string TabulatedText (this SyntaxTrivia st, int tabSize, int currentColumn = 0) =>
			st.ToString ().TabulatedText (tabSize, Math.Max (0, currentColumn));
		public static int TabulatedCol (this SyntaxTrivia st, int tabSize, int currentColumn = 0) {
			int tc = currentColumn;
			bool prevCharIsTab = false;
			string str = st.ToString ();

			for (int i = 0; i < str.Length; i++) {
				if (str[i] == '\t') {
					if (prevCharIsTab)
						tc += tabSize;
					else {
						tc += tabSize - tc % tabSize;
						prevCharIsTab = true;
					}
				} else {
					prevCharIsTab = false;
					tc++;
				}
			}
			return tc;
		}
		public static string TabulatedText (this string str, int tabSize, int currentColumn = 0) {
			if (string.IsNullOrEmpty (str))
				return "";
			currentColumn = Math.Max (0, currentColumn);
			bool prevCharIsTab = false;
			StringBuilder sb = new StringBuilder ();

			for (int i = 0; i < str.Length; i++) {
				if (str[i] == '\t') {
					if (prevCharIsTab)
						sb.Append (' ', tabSize);
					else {
						sb.Append (' ', tabSize - (sb.Length + currentColumn) % tabSize);
						prevCharIsTab = true;
					}
				} else {
					prevCharIsTab = false;
					sb.Append (str[i]);
				}
			}
			return sb.ToString ();
		}
		public static int TabulatedLength (this TextLine tl, int tabSize) =>
			TabulatedCol (tl.Text, tabSize, tl.Start, tl.End);
		public static int TabulatedCol(this SourceText st, int tabSize, int start, int end) {
			int tc = 0;
			bool prevCharIsTab = false;			

			for (int i = start; i < end; i++) {
				if (st[i] == '\t') {
					if (prevCharIsTab)
						tc += tabSize;
					else {
						tc += tabSize - tc % tabSize;
						prevCharIsTab = true;
					}
				} else {
					prevCharIsTab = false;
					tc++;
				}
			}
			return tc;
		}
		public static int AbsoluteCharPosFromTabulatedColumn (this TextLine tl, int visualColumn, int tabSize = 4) {
			int i = 0;
			int buffCol = tl.Start;
			bool prevCharIsTab = false;
			while (i < visualColumn && buffCol < tl.End) {
				if (tl.Text[buffCol] == '\t')
					if (prevCharIsTab)
						i += tabSize;
					else {
						i += tabSize - i % tabSize;
						prevCharIsTab = true;
					}
				else {
					i++;
					prevCharIsTab = false;
				}
				buffCol++;
			}
			return buffCol;
		}
		public static int GetCharPosFromVisualColumn (this TextLine tl, int visualColumn, int tabSize = 4)
		{
			int i = 0;
			int buffCol = tl.Start;
			bool prevCharIsTab = false;
			while (i < visualColumn && buffCol < tl.End) {
				if (tl.Text[buffCol] == '\t')
					if (prevCharIsTab)
						i += tabSize;
					else {
						i += tabSize - i % tabSize;
						prevCharIsTab = true;
					}
				else {
					i++;
					prevCharIsTab = false;
				}
				buffCol++;
			}			
			return buffCol - tl.Start;
		}
		/// <summary>
		/// return End pos of TextLine including linebreak if Start = End without line break
		/// usefull for selection drawing of empty lines.
		/// </summary>
		public static int GetEnd (this TextLine tl) => tl.Start == tl.End ? tl.EndIncludingLineBreak : tl.End;
		public static int GetEnd (this TextLine tl, int customStartPosInLine) => customStartPosInLine == tl.End ? tl.EndIncludingLineBreak : tl.End;

		public static int GetWordStart (this SourceText st, int curPos) {
			if (st?.Length == 0)
				return 0;
			TextLine tl = st.Lines.GetLineFromPosition (curPos);			
			if (curPos < tl.Start + 2)
				return tl.Start;
			int ws = curPos - 1;
			if (st[ws].IsWhiteSpaceOrNewLine ()) {				
				while (ws > tl.Start && st[ws-1].IsWhiteSpaceOrNewLine ())
					ws--;
			} else {
				while (ws > tl.Start && !st[ws-1].IsWhiteSpaceOrNewLine ())
					ws--;
			}
			return ws;
		}
		public static int GetWordEnd (this SourceText st, int curPos) {
			if (curPos > st?.Length - 2)
				return  st?.Length - 1 ?? 0;
			int ws = curPos + 1;
			if (st[curPos].IsWhiteSpaceOrNewLine ()) {				
				while (ws < st.Length - 1 && st[ws].IsWhiteSpaceOrNewLine ())
					ws++;
			} else {
				while (ws < st.Length - 1 && !st[ws].IsWhiteSpaceOrNewLine ())
					ws++;
			}
			return ws;
		}
		public static ObservableList<object> GetChilNodesOrTokens (this SyntaxNode node) {
			ObservableList<object> tmp = new ObservableList<object> ();

			var childs = node.ChildNodesAndTokens().GetEnumerator();

			while (childs.MoveNext()) {
				var c = childs.Current;
				if (c.IsNode) {
					tmp.Add (c.AsNode ());
					continue;
				}
				SyntaxToken tok = c.AsToken ();
				if (tok.HasLeadingTrivia) {
					foreach (var trivia in tok.LeadingTrivia) 
						tmp.Add (trivia);
				}
				tmp.Add (tok);
				if (tok.HasTrailingTrivia) {
					foreach (var trivia in tok.TrailingTrivia) 						
						tmp.Add (trivia);					
				}
			}

			return tmp;
		}
		public static IEnumerable GetStructureAsList (this SyntaxTrivia trivia)
			=> new SyntaxNode[] { trivia.GetStructure () };
		//kind is a language extension, not found by crow.
		public static SyntaxKind CSKind (this SyntaxToken tok) => tok.Kind ();
		public static SyntaxKind CSKind (this SyntaxTrivia tok) => tok.Kind ();
	}
}
