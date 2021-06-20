// Copyright (c) 2020-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Crow.Cairo;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using BreakPoint = Crow.Coding.Debugging.BreakPoint;

namespace Crow.Coding
{
	
	public class SyntaxNodePrinter : CSharpSyntaxWalker
	{
		static int tabSize = 4;
		bool cancel, printLineNumbers;
		int firstLine, currentLine, currentCol, printedLinesIndex;
		Fold currentFold;		
		Context ctx;
		RoslynEditor editor;
		SemanticModel semanticModel;
		FontExtents fe;
		double y, lineHeight, lineNumWidth;
		Rectangle bounds;
		RectangleD marginRect;
		public int [] printedLinesNumbers;
		Dictionary<string, TextFormatting> formatting;
		TextFormatting curTextFormat, leftMarginTextFormat;		

		bool foldedLine, firstToken = true;
		int visibleLines;

		public SyntaxNodePrinter (RoslynEditor editor) : base (SyntaxWalkerDepth.StructuredTrivia)
		{
			this.editor = editor;			
			
		}
		BreakPoint[] breakPoints;
		public void Draw (Context ctx, SyntaxNode node, int rootNodeFirstLine = 0, int firstPrintedLine = 0) {
			this.ctx = ctx;

			visibleLines = editor.visibleLines;
			lineHeight = editor.lineHeight;

			if (editor.Compilation != null) {
				semanticModel = editor.Compilation.GetSemanticModel (node.SyntaxTree);				
			}
			breakPoints = editor.BreakPoints.Where (bp => bp.File == editor.ProjectNode && bp.IsEnabled).ToArray ();

			CrowIDE ide = editor.IFace as CrowIDE;

			printLineNumbers = ide.PrintLineNumbers;
			formatting = ide.SyntaxTheme;
			
			curTextFormat = formatting["default"];
			leftMarginTextFormat = formatting["leftMargin"];

			bounds = editor.ClientRectangle;
			marginRect = bounds;
			marginRect.Width = editor.leftMargin - RoslynEditor.leftMarginGap;

			if (curTextFormat.Background != Colors.Transparent) {
				ctx.Rectangle (bounds);
				ctx.SetSource (curTextFormat.Background);
				ctx.Fill ();
			}
			
			ctx.SetSource (leftMarginTextFormat.Background);
			ctx.Rectangle (marginRect);
			ctx.Fill ();

			marginRect.Height = lineHeight;

			fe = ctx.FontExtents;
			fe.MaxXAdvance = ctx.TextExtents ("A").XAdvance;
			y = bounds.Top;
			currentCol = -1;// < 0 => margin no printed
			printedLinesNumbers = new int[Math.Min (visibleLines, editor.totalLines)];
			printedLinesIndex = (firstPrintedLine == rootNodeFirstLine) ? 0 : -1;//<0 until firstLine is reached
			currentLine = rootNodeFirstLine;
			currentFold = editor.foldingManager.GetFoldContainingLine (currentLine);
			firstLine = firstPrintedLine;
			cancel = false;			
			lineNumWidth = ctx.TextExtents (editor.totalLines.ToString ()).Width;			

			Visit (node);

			if (currentCol >= 0 && currentLine < editor.totalLines)
				printedLinesNumbers[printedLinesIndex] = currentLine;
			else
				checkPrintMargin();//print last empty line
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
				double yStart = cb.Y + visualLineStart * lineHeight;
				RectangleD r = new RectangleD (xStart,
					yStart, (visualColEnd - visualColStart) * fe.MaxXAdvance, lineHeight);

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
						r.Top += lineHeight;
						TextLine tl = buffer.Lines[printedLinesNumbers[l]];
						r.Width = Math.Min (cb.Width - editor.leftMargin, buffer.TabulatedCol (tabSize, tl.Start, tl.GetEnd () - editor.ScrollX) * fe.MaxXAdvance);
						ctx.Rectangle (r);
						ctx.Fill ();
					}
					if (visualLineEnd >= 0) {
						r.Top += lineHeight;
						r.Width = Math.Min (cb.Width - editor.leftMargin, Math.Max (1, visualColEnd) * fe.MaxXAdvance);
						ctx.Rectangle (r);
						ctx.Fill ();
					}
				}
			}
		}
		
		/*public override void DefaultVisit (SyntaxNode node) {
			if (node.ContainsDiagnostics) {
            }
			base.DefaultVisit (node);
		}*/
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
			//testPrintNodesBounds (node);
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

			if (token.IsKind (SyntaxKind.EndOfFileToken))
				return;
			if (token.IsKind (SyntaxKind.XmlTextLiteralNewLineToken))
				lineBreak ();
			else if (SyntaxFacts.IsLiteralExpression (token.Kind ())) {
				curTextFormat = formatting["LiteralExpression"];
				print (Regex.Split (token.ToString (), @"\r\n|\r|\n|\\\n"), token.Kind ());
			} else if (!dontPrintLine) {
				SyntaxKind kind = token.Kind ();
				
				if (!token.IsPartOfStructuredTrivia ()) {										
					if (SyntaxFacts.IsPredefinedType (kind))
						curTextFormat = formatting["PredefinedType"];
					else if (SyntaxFacts.IsAccessibilityModifier (kind))
						curTextFormat = formatting["AccessibilityModifier"];
					else if (SyntaxFacts.IsKeywordKind (kind))
						curTextFormat = formatting["keyword"];
					else if (SyntaxFacts.IsLiteralExpression (kind))
						curTextFormat = formatting["LiteralExpression"];
					else if (kind == SyntaxKind.IdentifierToken) {
						if (semanticModel != null) {
							SymbolInfo symbInfo = semanticModel.GetSymbolInfo (token.Parent);
							symbol = symbInfo.Symbol;
						}

						if (symbol != null && formatting.ContainsKey (symbol.Kind.ToString ()))
							curTextFormat = formatting[symbol.Kind.ToString()];
						else {
							if (symbol != null)
								Console.WriteLine ($"Symbol with no syle: Kind:{symbol.Kind}");
							if (SyntaxFacts.IsValidIdentifier (token.Text))
								curTextFormat = formatting["identifier"];
							else
								curTextFormat = formatting["default"];
						}
					} else
						curTextFormat = formatting["default"];
					
				}

				print (token.ToString (), kind);
			
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
					curTextFormat = formatting["PreprocessorDirective"];
				else if (SyntaxFacts.IsPreprocessorDirective (kind) || SyntaxFacts.IsPreprocessorPunctuation (kind))
					curTextFormat = formatting["error"];
				else if (kind == SyntaxKind.SingleLineDocumentationCommentTrivia || kind == SyntaxKind.MultiLineDocumentationCommentTrivia)
					curTextFormat = formatting["DocumentationComment"];
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
			/*if (currentLine >= editor.totalLines-2)
				System.Diagnostics.Debugger.Break();*/
			marginRect.Top = y;
			RectangleD mgR = marginRect;
			mgR.Left += RoslynEditor.breakPointsGap;
			mgR.Width -= RoslynEditor.breakPointsGap;
			if (printLineNumbers) {								
				Color mgFg = leftMarginTextFormat.Foreground;
				Color mgBg = leftMarginTextFormat.Background;
				
				string strLN = (currentLine + 1).ToString ();
				if (currentLine == editor.CurrentLine){
					mgFg = Colors.White;					
					/*ctx.SetSource (Colors.Grey);
					ctx.Rectangle (mgR);
					ctx.Fill ();*/
				}
				ctx.SelectFontFace (editor.Font.Name, FontSlant.Normal, FontWeight.Normal);
				ctx.SetSource (mgFg);
				ctx.MoveTo (mgR.Left + (int)(lineNumWidth - ctx.TextExtents (strLN).Width), y + fe.Ascent);
				ctx.ShowText (strLN);						
			}
			mgR.Left = bounds.X + editor.leftMargin;
			mgR.Width = bounds.Width - editor.leftMargin;
			if (breakPoints != null) {
				if (breakPoints.Any (bp => bp.Line == currentLine)) {
					ctx.Arc (bounds.X + 8, y + lineHeight / 2.0, 5, 0, Math.PI * 2.0);
					ctx.Rectangle (mgR);
					ctx.SetSource (1, 0, 0, 0.4);
					ctx.Fill ();
				}
			}
			if (editor.ExecutingLine == currentLine) {
				ctx.Rectangle (mgR);
				ctx.SetSource (0, 0, 1, 0.3);
				ctx.Fill ();
			}
			foldedLine = false;
			if (editor.foldingEnabled && currentFold != null) {

				if (currentFold.LineStart == currentLine) {
					Rectangle rFld = new Rectangle (bounds.X + editor.leftMargin - RoslynEditor.leftMarginGap - editor.foldMargin,
						(int)(y + lineHeight / 2.0 - RoslynEditor.foldSize / 2.0), RoslynEditor.foldSize, RoslynEditor.foldSize);
					
					ctx.LineWidth = 1.0;

					ctx.Rectangle (rFld);
					ctx.SetSource (Colors.White);
					ctx.Fill ();
					ctx.SetSource (Colors.Black);
					ctx.Rectangle (rFld, 1.0);
					if (currentFold.IsFolded) {
						foldedLine = true;
						ctx.MoveTo (rFld.Center.X + 0.5, rFld.Y + 2);
						ctx.LineTo (rFld.Center.X + 0.5, rFld.Bottom - 2);
					}

					ctx.MoveTo (rFld.Left + 2, rFld.Center.Y + 0.5);
					ctx.LineTo (rFld.Right - 2, rFld.Center.Y + 0.5);
					ctx.Stroke ();					
				}
			}
			currentCol = 0;
			firstToken = true;
		}		
        //increment currentLine and if printed, store and increment printed lines
        void lineBreak ()
		{			
			if (dontPrintLine) {
				currentCol = -1;
				incrementCurrentLine ();				
				if (printedLinesIndex < 0 && currentLine >= firstLine)
					printedLinesIndex = 0;									
			} else {
				checkPrintMargin ();//ensure margin is printed if line is empty
				currentCol = -1;
				printedLinesNumbers[printedLinesIndex] = currentLine;
				printedLinesIndex++;
				y += lineHeight;
				cancel = printedLinesIndex == visibleLines;
				incrementCurrentLine ();
			}
		}

		void incrementCurrentLine () {			
			currentLine++;
			//Last line of SourceText.Lines has a line break, but no additional line is in the array.
			/*if (!cancel && currentLine == editor.totalLines)
				checkPrintMargin ();*/
			if (currentLine > currentFold.LineEnd)
				currentFold = currentFold.Parent;

			if (currentFold != null && !currentFold.IsFolded)
				currentFold = currentFold.GetFoldContainingLine (currentLine);
		}
		bool skippedLine => currentFold.IsFolded && currentLine > currentFold.LineStart;
		bool dontPrintLine => printedLinesIndex < 0 || skippedLine;

		//public override void VisitDocumentationCommentTrivia (DocumentationCommentTriviaSyntax node) {}
		
		void printFoldedLine (string str) {
			int diffX = currentCol - editor.ScrollX;
			if (diffX < 0) {
				if (diffX + str.Length <= 0) {
					currentCol += str.Length;					
					return;
				}
				str = str.Substring (-diffX);
				diffX = 0;
			}

			TextExtents te = ctx.TextExtents (str);
			double x = bounds.X + editor.leftMargin + diffX * fe.MaxXAdvance;
			RectangleD r = new RectangleD (
				x, y,
				te.Width, lineHeight);
			const double margin = 4;
			r.Inflate(0.5,0.5);
			r.Width += margin * 2.0;
			ctx.LineWidth = 1;
			ctx.SetSource (curTextFormat.Background);
			ctx.Rectangle (r);
			ctx.FillPreserve ();
			ctx.SetSource (curTextFormat.Foreground);
			ctx.Stroke();
			ctx.MoveTo (x + margin, y + fe.Ascent);
			ctx.ShowText (str);
		}
		
		public override void VisitRegionDirectiveTrivia(RegionDirectiveTriviaSyntax node)
		{
			
			if (!dontPrintLine && foldedLine) {
				curTextFormat = formatting["FoldedRegion"];
				string str = $"{node.EndOfDirectiveToken.LeadingTrivia.ToString()}";
				printFoldedLine (str);
				lineBreak();
			} else{
				curTextFormat = formatting["Region"];
				base.VisitRegionDirectiveTrivia(node);
			}

		}


		void print (SyntaxTrivia trivia) {
            SyntaxKind kind = trivia.Kind ();

            if (trivia.IsPartOfStructuredTrivia ()) {
                /*if (kind == SyntaxKind.PreprocessingMessageTrivia)
                    curTextFormat = formatting["PreprocessorMessage"];*/
            } else if (kind == SyntaxKind.DisabledTextTrivia)
				curTextFormat = formatting["DisabledText"];
			else
				curTextFormat = formatting["trivia"];

            if (trivia.IsKind (SyntaxKind.DisabledTextTrivia) || trivia.IsKind (SyntaxKind.MultiLineCommentTrivia)) {
                print (Regex.Split (trivia.ToString (), @"\r\n|\r|\n|\\\n"), kind);
                //foldable = lines.Length > 2;
            } else if (!dontPrintLine)
				print (trivia.TabulatedText (tabSize, currentCol), kind);
        }
		void print (string[] lines, SyntaxKind kind) {
			for (int i = 0; i < lines.Length - 1; i++) {
				if (!dontPrintLine)
					print (lines[i].TabulatedText (tabSize, currentCol), kind);
				lineBreak ();
				if (cancel)
					return;
			}
			if (!dontPrintLine)
				print (lines[lines.Length - 1].TabulatedText (tabSize, currentCol), kind);			
		}
		void print (string lstr, SyntaxKind kind)
		{
			checkPrintMargin ();

			ctx.SelectFontFace (editor.Font.Name,
				curTextFormat.Italic ? FontSlant.Italic : FontSlant.Normal,
				curTextFormat.Bold ? FontWeight.Bold : FontWeight.Normal);
			ctx.SetSource (curTextFormat.Foreground);

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
			firstToken = false;
        }
    }
}
