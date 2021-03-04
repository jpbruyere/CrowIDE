// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crow.Cairo;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Crow.Coding
{
	
	public class SyntaxNodePrinter : CSharpSyntaxWalker
	{
		static int tabSize = 4;
		bool cancel, printLineNumbers;
		int firstLine, currentLine, currentCol, printedLines;
		Stack<Fold> skipped;
		int skippedLines;
		Context ctx;
		RoslynEditor editor;
		SemanticModel semanticModel;
		FontExtents fe;
		double y, lineNumWidth;
		Rectangle bounds;
		public int [] printedLinesNumbers;
		TextFormatting tf;

		public int SkippedLines => skippedLines;

		int visibleLines => editor.visibleLines;
		int scrollY => editor.ScrollY;

		public SyntaxNodePrinter (RoslynEditor editor) : base (SyntaxWalkerDepth.StructuredTrivia)
		{
			this.editor = editor;
			tf = editor.formatting["default"];
			/*if (printedLines == 0)
				checkPrintMargin ();*/
		}

		public void Draw (Context ctx, SyntaxNode node) {
			this.ctx = ctx;

			if (editor.Compilation != null)											
				semanticModel = editor.Compilation.GetSemanticModel (node.SyntaxTree);

			printLineNumbers = (editor.IFace as CrowIDE).PrintLineNumbers;
			printedLinesNumbers = new int[visibleLines];
			printedLines = (scrollY == 0) ? 0 : -1;//<0 until firstLine is reached

			bounds = editor.ClientRectangle;

			fe = ctx.FontExtents;
			fe.MaxXAdvance = ctx.TextExtents ("A").XAdvance;
			y = bounds.Top;
			currentCol = -1;// < 0 => margin no printed
			currentLine = 0;
			firstLine = scrollY;
			cancel = false;
			skipped = new Stack<Fold> ();
			skippedLines = 0;

			lineNumWidth = ctx.TextExtents (editor.totalLines.ToString ()).Width;
			

			Visit (node);			
        }
		void testPrintNodesBounds (SyntaxNode node) {
			TextSpan? nodeSpan = node.FullSpan.Intersection (editor.visibleSpan);
			if (nodeSpan?.Length > 0) {
				LinePositionSpan lps = node.SyntaxTree.GetLineSpan (nodeSpan.GetValueOrDefault ()).Span;
				SourceText buffer = node.SyntaxTree.GetText ();

				Rectangle cb = editor.ClientRectangle;
				Color selbg = new Color (0, 0, 0, 0.1);
				string k = node.Kind ().ToString ();
				if (k.EndsWith ("Token"))
					selbg = new Color (1, 0, 0, 0.1);
				else if (k.EndsWith ("Keyword"))
					selbg = new Color (0, 0, 1, 0.1);
				else if (k.EndsWith ("Trivia"))
					selbg = new Color (0, 1, 0, 0.1);


				TextLine startTl = buffer.Lines[lps.Start.Line];
				TextLine endTl = buffer.Lines[lps.End.Line];

				int visualColStart = buffer.TabulatedCol (tabSize, startTl.Start, startTl.Start + lps.Start.Character) - editor.ScrollX;
				int visualColEnd = buffer.TabulatedCol (tabSize, endTl.Start, endTl.Start + lps.End.Character) - editor.ScrollX;
				int visualLineStart = startTl.LineNumber - firstLine;
				Console.WriteLine ($"{visualColStart}->{visualColEnd}  {startTl.LineNumber} -> {endTl.LineNumber}");
				double xStart = cb.X + visualColStart * fe.MaxXAdvance + editor.leftMargin;
				double yStart = cb.Y + visualLineStart * (fe.Ascent + fe.Descent);
				RectangleD r = new RectangleD (xStart,
					yStart, (visualColEnd - visualColStart) * fe.MaxXAdvance, (fe.Ascent + fe.Descent));

				ctx.SetSource (selbg);

				if (startTl.LineNumber == endTl.LineNumber) {
					ctx.Rectangle (r);
					ctx.Fill ();
				} else {
					r.Width = Math.Min (cb.Width - xStart, buffer.TabulatedCol (tabSize, nodeSpan.GetValueOrDefault ().Start, startTl.GetEnd (nodeSpan.GetValueOrDefault ().Start) - editor.ScrollX) * fe.MaxXAdvance);
					ctx.Rectangle (r);
					ctx.Fill ();
					int visualLineEnd = endTl.LineNumber - firstLine;
					r.Left = cb.X + editor.leftMargin;
					for (int l = visualLineStart + 1; l < (visualLineEnd < 0 ? printedLinesNumbers.Length : visualLineEnd); l++) {
						r.Top += (fe.Ascent + fe.Descent);
						TextLine tl = buffer.Lines[printedLinesNumbers[l]];
						r.Width = Math.Min (cb.Width - editor.leftMargin, buffer.TabulatedCol (tabSize, tl.Start, tl.GetEnd () - editor.ScrollX) * fe.MaxXAdvance);
						ctx.Rectangle (r);
						ctx.Fill ();
					}
					if (visualLineEnd >= 0) {
						r.Top += (fe.Ascent + fe.Descent);
						r.Width = Math.Min (cb.Width - editor.leftMargin, Math.Max (1, visualColEnd) * fe.MaxXAdvance);
						ctx.Rectangle (r);
						ctx.Fill ();
					}
				}
			}
		}
		
		public override void DefaultVisit (SyntaxNode node) {
			if (node.ContainsDiagnostics) {

            }
			base.DefaultVisit (node);
		}
		public override void Visit (SyntaxNode node)
		{			
			if (cancel)
				return;
			if (semanticModel != null) {
				SymbolInfo symbInfo = semanticModel.GetSymbolInfo (node);				
				symbol = symbInfo.Symbol;
				/*if (symbol != null) {
					Console.WriteLine ($"Symbol: Kind:{symbol.Kind} {symbol.Name} {symbol.ContainingNamespace}.{symbol.ContainingType}.");
				}*/
			}

			base.Visit (node);
			symbol = null;
		}
		
		public override void VisitSkippedTokensTrivia(SkippedTokensTriviaSyntax node) {
			Console.WriteLine ($"\tskipped: {node.ToString()}");
			base.VisitSkippedTokensTrivia (node);

		}
		ISymbol symbol;
		
		public override void VisitToken (SyntaxToken token)
		{
			if (cancel)
				return;
			
			VisitLeadingTrivia (token);

			if (cancel)
				return;

			if (token.IsKind (SyntaxKind.XmlTextLiteralNewLineToken) || token.IsKind (SyntaxKind.EndOfFileToken))
				lineBreak ();
			else if (!dontPrintLine) {
				SyntaxKind kind = token.Kind ();

				if (!token.IsPartOfStructuredTrivia ()) {
					/*if (symbol != null) {						
						Console.WriteLine ($"Symbol: Kind:{symbol.Kind} {symbol.Name} {symbol.ContainingNamespace}.{symbol.ContainingType}.");
					}*/
					if (SyntaxFacts.IsPredefinedType (kind))
						tf = editor.formatting["PredefinedType"];
					else if (SyntaxFacts.IsAccessibilityModifier (kind))
						tf = editor.formatting["AccessibilityModifier"];
					/*else if (SyntaxFacts.IsName (kind))
						tf = editor.formatting["name"];*/
					else if (SyntaxFacts.IsKeywordKind (kind))
						tf = editor.formatting["keyword"];
					else if (SyntaxFacts.IsLiteralExpression (kind))
						tf = editor.formatting["LiteralExpression"];
					else if (kind == SyntaxKind.IdentifierToken) {
						if (SyntaxFacts.IsValidIdentifier (token.Text))
							tf = editor.formatting["identifier"];
						else
							tf = editor.formatting["default"];
					} else
						tf = editor.formatting["default"];					
				}

				print (token.ToString ().TabulatedText (tabSize, currentCol), kind);
			}

			VisitTrailingTrivia (token);
		}

        public override void VisitTrivia (SyntaxTrivia trivia)
		{
			if (cancel)
				return;

			SyntaxKind kind = trivia.Kind ();
			if (kind == SyntaxKind.EndOfLineTrivia) {
				lineBreak ();
				return;
			}

			if (trivia.HasStructure) {
				if (trivia.IsDirective)
					tf = editor.formatting["PreprocessorDirective"];
				else if (SyntaxFacts.IsPreprocessorDirective (kind) || SyntaxFacts.IsPreprocessorPunctuation (kind))
					tf = editor.formatting["error"];
				else if (kind == SyntaxKind.SingleLineDocumentationCommentTrivia || kind == SyntaxKind.MultiLineDocumentationCommentTrivia)
					tf = editor.formatting["DocumentationComment"];
				this.Visit ((CSharpSyntaxNode)trivia.GetStructure());
			} else if (trivia.IsKind (SyntaxKind.WhitespaceTrivia)) {
				if (dontPrintLine)
					return;
				checkPrintMargin ();//ensure margin is printed if line is empty
				currentCol = trivia.TabulatedCol (tabSize, currentCol);
			}else
				print (trivia);				
		}

        void checkPrintMargin ()
		{
			if (currentCol >= 0)
				return;
			if (printLineNumbers) {
				RectangleD mgR = new RectangleD (bounds.X, y, editor.leftMargin - RoslynEditor.leftMarginGap, Math.Ceiling ((fe.Ascent + fe.Descent)));
				/*if (cl.exception != null) {
					mgBg = Color.Red;
					if (CurrentLine == lineIndex)
						mgFg = Color.White;
					else
						mgFg = Color.LightGrey;
				}else */
				Color mgFg = Colors.Jet;
				Color mgBg = Colors.Grey;
				if (editor.CurrentLine == currentLine && editor.HasFocus) {
					mgFg = Colors.Black;
					mgBg = Colors.RoyalBlue;
				}
				string strLN = (currentLine + 1).ToString ();
				ctx.SetSource (mgBg);
				ctx.Rectangle (mgR);
				ctx.Fill ();
				ctx.SetSource (mgFg);

				ctx.MoveTo (bounds.X + (int)(lineNumWidth - ctx.TextExtents (strLN).Width), y + fe.Ascent);
				ctx.ShowText (strLN);
				ctx.Fill ();
			}
			if (editor.foldingEnabled) {
				if (editor.foldingManager.TryGetFold (currentLine, out Fold fold)) {
					Rectangle rFld = new Rectangle (bounds.X + editor.leftMargin - RoslynEditor.leftMarginGap - editor.foldMargin,
						(int)(y + (fe.Ascent + fe.Descent) / 2.0 - RoslynEditor.foldSize / 2.0), RoslynEditor.foldSize, RoslynEditor.foldSize);
					
					ctx.LineWidth = 1.0;

					ctx.Rectangle (rFld);
					ctx.SetSource (Colors.White);
					ctx.Fill ();
					ctx.SetSource (Colors.Black);
					ctx.Rectangle (rFld, 1.0);
					if (fold.IsFolded) {						
						ctx.MoveTo (rFld.Center.X + 0.5, rFld.Y + 2);
						ctx.LineTo (rFld.Center.X + 0.5, rFld.Bottom - 2);
					}

					ctx.MoveTo (rFld.Left + 2, rFld.Center.Y + 0.5);
					ctx.LineTo (rFld.Right - 2, rFld.Center.Y + 0.5);
					ctx.Stroke ();					
				}
			}
			currentCol = 0;
		}		
        //increment currentLine and if printed, store and increment printed lines
        void lineBreak ()
		{
			currentCol = -1;

			if (dontPrintLine) {
				incrementCurrentLine ();
				if (printedLines < 0 && currentLine >= firstLine + skippedLines)
					printedLines = 0;									
			} else {
				checkPrintMargin ();//ensure margin is printed if line is empty
				printedLinesNumbers[printedLines] = currentLine;
				printedLines++;
				y += (fe.Ascent + fe.Descent);
				cancel = printedLines == visibleLines;
				incrementCurrentLine ();
			}
		}

		void incrementCurrentLine () {			
			currentLine++;
			//Last line of SourceText.Lines has a line break, but no additional line is in the array.
			if (!cancel && currentLine == editor.totalLines)
				checkPrintMargin ();            
			if (skipped.Count > 0) {
				if (currentLine > skipped.Peek ().LineEnd)
					skipped.Pop ();
				else
					return;				
			}

			if (editor.foldingManager.TryGetFold (currentLine, out Fold fold)) {				
				if (fold.IsFolded)
					skipped.Push (fold);
			}
		}
		bool skippedLine => skipped.Count > 0 ? currentLine > skipped.Peek().LineStart : false;
		bool dontPrintLine => printedLines < 0 || skippedLine;

		void print (SyntaxTrivia trivia) {
            SyntaxKind kind = trivia.Kind ();

            if (trivia.IsPartOfStructuredTrivia ()) {
                if (kind == SyntaxKind.PreprocessingMessageTrivia)
                    tf = editor.formatting["PreprocessorMessage"];
            } else {
                if (kind == SyntaxKind.DisabledTextTrivia)
                    tf = editor.formatting["DisabledText"];
                else
                    tf = editor.formatting["trivia"];
            }

            if (trivia.IsKind (SyntaxKind.DisabledTextTrivia) || trivia.IsKind (SyntaxKind.MultiLineCommentTrivia)) {				
                string[] lines = Regex.Split (trivia.ToString (), @"\r\n|\r|\n|\\\n");
                //foldable = lines.Length > 2;
                for (int i = 0; i < lines.Length - 1; i++) {
					if (!dontPrintLine)
						print (lines[i].TabulatedText (tabSize, currentCol), kind);
                    lineBreak ();
                    if (cancel)
                        return;
                }
				if (!dontPrintLine)
					print (lines[lines.Length - 1].TabulatedText (tabSize, currentCol), kind);
            } else if (!dontPrintLine)
				print (trivia.TabulatedText (tabSize, currentCol), kind);
        }
		void print (string lstr, SyntaxKind kind)
		{
			checkPrintMargin ();

			if (kind == SyntaxKind.IdentifierToken)
				tf.Bold = true;

			ctx.SelectFontFace (editor.Font.Name,
				tf.Italic ? FontSlant.Italic : FontSlant.Normal,
				tf.Bold ? FontWeight.Bold : FontWeight.Normal);
			ctx.SetSource (tf.Foreground);
			//Console.WriteLine (tf.Foreground);

			int diffX = currentCol - editor.ScrollX;

			string str = lstr;

			if (diffX < 0) {
				if (diffX + lstr.Length > 0)
					str = lstr.Substring (-diffX);
				else {
					currentCol += lstr.Length;
					return;
				}
			} else
				diffX = 0;

			ctx.MoveTo (bounds.X + editor.leftMargin + (currentCol - editor.ScrollX - diffX) * fe.MaxXAdvance, y + fe.Ascent);
			ctx.ShowText (str);
			currentCol += lstr.Length;
        }
    }
}
