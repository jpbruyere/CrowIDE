// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;

namespace Crow.Coding
{
	public struct Token
	{
		public BufferParser.TokenType Type;
		public string Content;
		public Point Start;
		public Point End;

		public string PrintableContent {
			get { return string.IsNullOrEmpty(Content) ? "" : Content.Replace("\t", new String(' ', Interface.TAB_SIZE)); }
		}

//		public Token (TokenType tokType, string content = ""){
//			Type = tokType;
//			Content = content;
//		}

		public bool IsNull { get { return IsEmpty && Type == BufferParser.TokenType.Unknown; }}
		public bool IsEmpty { get { return string.IsNullOrEmpty(Content); }}

		public bool IsTrivia => Type == BufferParser.TokenType.WhiteSpace ||
			Type == BufferParser.TokenType.NewLine ||
			Type == BufferParser.TokenType.LineComment;

		public static bool operator == (Token a, Token b) => Convert.ToInt32 (a.Type) == Convert.ToInt32 (b.Type) && a.Content == b.Content;
		public static bool operator != (Token a, Token b) => Convert.ToInt32 (a.Type) != Convert.ToInt32 (b.Type) || a.Content != b.Content;
		public static bool operator == (Token t, System.Enum tt) => Convert.ToInt32(t.Type) == Convert.ToInt32(tt);
		public static bool operator != (Token t, System.Enum tt)=> Convert.ToInt32(t.Type) != Convert.ToInt32(tt);
		public static bool operator == (Token t, string s) => t.Content == s;
		public static bool operator != (Token t, string s) => t.Content != s;
		public static bool operator == (System.Enum tt, Token t) => Convert.ToInt32 (t.Type) == Convert.ToInt32 (tt);
		public static bool operator != (System.Enum tt, Token t) => Convert.ToInt32(t.Type) != Convert.ToInt32(tt);

		public static Token operator +(Token t, char c){
			t.Content += c;
			return t;
		}
		public static Token operator +(Token t, string s){
			t.Content += s;
			return t;
		}
		public override string ToString ()
		{
			return string.Format ("[Tok{2}->{3}:{0}: {1}]", Type,Content,Start,End);
		}
	}
}

