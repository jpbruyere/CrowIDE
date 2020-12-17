using System;
using Crow;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;

namespace Crow.Coding
{
	public class StyleParser : BufferParser
	{
		enum States {constants, classNames, members}
		public StyleParser (CodeBuffer _buffer) : base(_buffer)
		{
		}

		#region Character ValidityCheck
		static Regex rxValidChar = new Regex(@"\p{Lu}|\p{Ll}|\p{Lt}|\p{Lm}|\p{Lo}|\p{Nl}|\p{Mn}|\p{Mc}|\p{Nd}|\p{Pc}|\p{Cf}");
		static Regex rxNameStartChar = new Regex(@"_|\p{Lu}|\p{Ll}|\p{Lt}|\p{Lm}|\p{Lo}");															
		static Regex rxNameChar = new Regex(@"\p{Lu}|\p{Ll}|\p{Lt}|\p{Lm}|\p{Lo}|\p{Nl}|\p{Mn}|\p{Mc}|\p{Nd}|\p{Pc}|\p{Cf}");
		static Regex rxDecimal = new Regex(@"[0-9]+");
		static Regex rxHexadecimal = new Regex(@"[0-9a-fA-F]+");

		public bool nextCharIsValidCharStartName
		{
			get { return rxNameStartChar.IsMatch(new string(new char[]{Peek()})); }
		}
		public bool nextCharIsValidCharName
		{
			get { return rxNameChar.IsMatch(new string(new char[]{Peek()})); }
		}
		#endregion

		States curState;		

		public override void ParseCurrentLine ()
		{
			//Debug.WriteLine (string.Format("parsing line:{0}", currentLine));
			CodeLine cl = buffer [currentLine];
			Token prevLastToken = cl.LastToken;
			cl.Tokens = new List<Token> ();
			

			//retrieve current parser state from previous line
			if (currentLine > 0)
				curState = (States)buffer[currentLine - 1].EndingState;
			else
				curState = States.constants;

			States previousEndingState = (States)cl.EndingState;

			while (! eol) {
				SkipWhiteSpaces ();

				if (eol)
					break;

				if (Peek () == '\n') {
					if (currentTok != TokenType.Unknown)
						throw new ParserException (currentLine, currentColumn, "Unexpected end of line");
					Read ();
					eol = true;
					continue;
				}

				switch (Peek()) {
				case '/':
					readToCurrTok (true);
					switch (Peek ()) {
					case '/':
						currentTok += ReadLine ();
						saveAndResetCurrentTok (TokenType.LineComment);
						break;
					default:
						currentTok += ReadLine ();
						saveAndResetCurrentTok (TokenType.Unknown);
						break;
					}
					break;
				case ',':
					if (curState == States.members || previousNonTrivaTok != TokenType.Type)
						throw new ParserException (currentLine, currentColumn, "Unexpected char ','");
					readAndResetCurrentTok (TokenType.OperatorOrPunctuation, true);
					curState = States.classNames;
					break;
				case '{':
					if (curState == States.members)
						throw new ParserException (currentLine, currentColumn, "Unexpected char '{'");					
					readAndResetCurrentTok (TokenType.OpenBlock, true);
					curState = States.members;
					break;
				case '}':
					if (curState != States.members || !(previousNonTrivaTok == ";" || previousNonTrivaTok == TokenType.OpenBlock))
						throw new ParserException (currentLine, currentColumn, "Unexpected char '}'");
					readAndResetCurrentTok (TokenType.CloseBlock, true);
					curState = States.constants;
					break;
				case '=':
					if (curState == States.classNames || previousNonTrivaTok != TokenType.Type)
						throw new ParserException (currentLine, currentColumn, "Unexpected char '='");
					setPreviousTokOfTypeTo (TokenType.Type, TokenType.Identifier);					
					readAndResetCurrentTok (TokenType.OperatorOrPunctuation, true);					
					break;
				case '"':
					if (previousNonTrivaTok != "=")
						throw new ParserException (currentLine, currentColumn, "Unexpected char '\"'");					
					readAndResetCurrentTok (TokenType.StringLitteralOpening, true);

					while (!eol) {
						currentTok += ReadLineUntil ("\"");
						if (currentTok.Content [currentTok.Content.Length - 1] == '\\')
							readToCurrTok ();
						else
							break;
					}
					if (eol)
						throw new ParserException (currentLine, currentColumn, "Unexpected end of line");
					saveAndResetCurrentTok (TokenType.StringLitteral);
					readAndResetCurrentTok (TokenType.StringLitteralClosing, true);					
					break;
				case ';':
					if (curState == States.classNames || previousNonTrivaTok != TokenType.StringLitteralClosing)
						throw new ParserException (currentLine, currentColumn, "Unexpected end of statement");					
					readAndResetCurrentTok (TokenType.StatementEnding, true);					
					break;
				default:
					if (currentTok.Type != TokenType.Unknown)
						throw new ParserException (currentLine, currentColumn, "error curtok not null");
					/*if (curState == States.value)
						throw new ParserException (currentLine, currentColumn, "expecting value enclosed in '\"'");*/
					Token prev = previousNonTrivaTok;
					if (curState == States.constants && !(prev == ";" || prev.IsNull || prev == TokenType.CloseBlock))
						throw new ParserException (currentLine, currentColumn, "expecting end of statement");
					if (curState == States.members && !(prev == ";" || prev == TokenType.OpenBlock))
						throw new ParserException (currentLine, currentColumn, "expecting end of statement");
					if (curState == States.classNames && prev != ",")
						throw new ParserException (currentLine, currentColumn, "expecting ','");


					if (nextCharIsValidCharStartName) {						
						readToCurrTok (true);
						while (nextCharIsValidCharName)
							readToCurrTok ();
					}
					saveAndResetCurrentTok (TokenType.Type);
					break;
				}
			}

			if ((cl.EndingState != (int)curState || cl.LastToken != prevLastToken) && currentLine < buffer.LineCount - 1)
				buffer [currentLine + 1].Tokens = null;

			cl.EndingState = (int)curState;
		}
		public override void SyntaxAnalysis ()
		{
			initSyntaxAnalysis ();
			Node currentNode = RootNode;

			for (int i = 0; i < buffer.LineCount; i++) {
				CodeLine cl = buffer[i];
				if (cl.Tokens == null)
					continue;
				cl.SyntacticNode = null;

				int tokPtr = 0;
				while (tokPtr < cl.Tokens.Count) {
					switch (cl.Tokens [tokPtr].Type) {
					case TokenType.OpenBlock:						
						currentNode = addChildNode (currentNode, cl, tokPtr, "style");
						break;
					case TokenType.CloseBlock:						
						closeNodeAndGoUp (ref currentNode, cl, "style");
						break;
					}
					tokPtr++;
				}
			}
		}
	}
}

