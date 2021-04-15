using System.Reflection;
// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.ComponentModel;
using Crow.Cairo;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Glfw;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;

namespace Crow.Coding
{
	/// <summary>
	/// Scrolling text box optimized for monospace fonts, for coding
	/// </summary>
	public class RoslynEditor2 : Editor, IEditableTextWidget {		

		#region CTOR
		public RoslynEditor2 () : base () {

		}
        #endregion

		SourceText buffer;
		CSProjectItem CSProjectItm => ProjectNode as CSProjectItem;
		public SyntaxTree SyntaxTree {
			get => CSProjectItm.SyntaxTree;
			set => CSProjectItm.SyntaxTree = value;
		}

		volatile bool isDirty = false;
		int tabSize = 4;
		int longestLineCharCount = 0, longestLineIdx = 0, lastVisualColumn = -1;
		int leftMargin;
		internal int visibleLines = 1;
		internal double lineHeight;
		protected FontExtents fe;
		protected TextExtents te;
		Point mouseLocalPos;
		int hoverPos, selStartPos, hoverLine = -1, hoverColumn = -1, currentPos;

		TextSpan selection = default;
		
		int visibleColumns = 1;

		int currentLine, currentColumn, executingLine = -1;
		Color selBackground, selForeground;

		void measureLeftMargin () {}
		void findLongestLineAndUpdateMaxScrollX () {
			longestLineCharCount = 0;
			longestLineIdx = 0;
			for (int i = 0; i < buffer.Lines.Count; i++) {
				TextLine tl = buffer.Lines[i];
				int length = tl.TabulatedLength (tabSize);
				if (length <= longestLineCharCount)
					continue;
				longestLineCharCount = length;
				longestLineIdx = i;
			}
			updateMaxScrollX ();
		}
		/// <summary>
		/// Updates visible line in widget, adapt max scroll y and updatePrintedLines
		/// </summary>
		void updateVisibleLines () {
			visibleLines = (int)Math.Floor ((double)ClientRectangle.Height / lineHeight);
			NotifyValueChanged ("VisibleLines", visibleLines);
			updateMaxScrollY ();
			RegisterForGraphicUpdate ();
		}
		void updateVisibleColumns () {
			visibleColumns = (int)Math.Floor ((double)(ClientRectangle.Width - leftMargin) / fe.MaxXAdvance);
			NotifyValueChanged ("VisibleColumns", visibleColumns);
			updateMaxScrollX ();
		}
		void updateMaxScrollX () {
			MaxScrollX = Math.Max (0, longestLineCharCount - visibleColumns);
			if (longestLineCharCount > 0)
				NotifyValueChanged ("ChildWidthRatio", Slot.Width * visibleColumns / longestLineCharCount);
		}
		void updateMaxScrollY () {
			if (buffer == null) 
				MaxScrollY = 0;
			else {
				int unfoldedLines = buffer.Lines.Count;
				MaxScrollY = Math.Max (0, unfoldedLines - visibleLines);
				NotifyValueChanged ("ChildHeightRatio", Slot.Height * visibleLines / unfoldedLines);
			}
		}
		
		#region Editor overrides
		protected override void updateEditorFromProjFile () {
			Debug.WriteLine ("\t\tSourceEditor updateEditorFromProjFile");

			buffer = SyntaxTree.GetText ();

			updateMaxScrollY ();
			measureLeftMargin ();
			findLongestLineAndUpdateMaxScrollX ();

			isDirty = false;			

			RegisterForGraphicUpdate ();
		}
		protected override void updateProjFileFromEditor () {
			Debug.WriteLine ("\t\tSourceEditor updateProjFileFromEditor");

			char[] chars = new char[buffer.Length];
			buffer.CopyTo (0, chars, 0, buffer.Length);			
			projFile.UpdateSource (this, new string (chars));
			EditorIsDirty = false;
		}
		protected override bool EditorIsDirty {
			get => isDirty;
			set { isDirty = value; }
		}
		protected override bool IsReady => projFile != null;
		#endregion


		#region Public Crow Properties
		public int CurrentLine {
			get { return currentLine; }
			set {
				if (currentLine == value)
					return;
				currentLine = value;
				if (currentLine < ScrollY)
					ScrollY = currentLine;
				else if (currentLine >= ScrollY + visibleLines)
					ScrollY = currentLine - visibleLines + 1;
				NotifyValueChanged ("CurrentLine", currentLine);
				RegisterForRedraw ();
			}
		}
		public int CurrentColumn {
			get { return currentColumn; }
			set {
				if (currentColumn == value)
					return;
				currentColumn = value;
				if (currentColumn < ScrollX)
					ScrollX = currentColumn;
				NotifyValueChanged ("CurrentColumn", currentColumn);
				RegisterForRedraw ();
			}
		}
		public int ExecutingLine {
			get { return executingLine; }
			set {
				if (executingLine == value)
					return;
				executingLine = value;
				NotifyValueChanged ("ExecutingLine", executingLine);
				RegisterForRedraw ();
			}
		}

		internal bool printLineNumbers => (this.IFace as CrowIDE).PrintLineNumbers;

		[DefaultValue("RoyalBlue")]
		public virtual Color SelectionBackground {
			get { return selBackground; }
			set {
				if (value == selBackground)
					return;
				selBackground = value;
				NotifyValueChanged ("SelectionBackground", selBackground);
				RegisterForRedraw ();
			}
		}
		[DefaultValue("White")]
		public virtual Color SelectionForeground {
			get { return selForeground; }
			set {
				if (value == selForeground)
					return;
				selForeground = value;
				NotifyValueChanged ("SelectionForeground", selForeground);
				RegisterForRedraw ();
			}
		}
		#endregion

		int getTabulatedColumn (int col, int line) {
			int start = buffer.Lines[line].Start;
			SourceText st = buffer.GetSubText (TextSpan.FromBounds (start, start + col));

			int tc = 0;
			bool prevCharIsTab = false;
			
            for (int i = 0; i < col; i++) {
				if (st[i] == '\t') {
					if (prevCharIsTab) {
						tc += tabSize;
						continue;
					}
					tc += tabSize - tc % tabSize;
					prevCharIsTab = true;
					continue;
				}
				prevCharIsTab = false;
				tc++;
            }
			Console.WriteLine ($"getTabulatedColumn ({col}, {line}) = {tc}");
			return tc;
		}
		int getTabulatedColumn (Point pos) => getTabulatedColumn (pos.X, pos.Y);

		void move (int hDelta, int vDelta = 0)
		{
			if (buffer?.Length == 0)
				return;

			if (IFace.Shift) {
				if (selection.IsEmpty)
					selStartPos = CurrentPos;
			}else
				selection = default;

			if (hDelta != 0) {
				lastVisualColumn = -1;
				CurrentPos += hDelta;
				if (CurrentPos < 0)
					CurrentPos = 0;
				else if (CurrentPos >= buffer.Length)
					CurrentPos = buffer.Length - 1;
				TextLine tl = buffer.Lines.GetLineFromPosition (CurrentPos);
				if (CurrentPos > tl.End) {
					if (hDelta > 0 && tl.LineNumber < buffer.Lines.Count - 1) 						
						CurrentPos = buffer.Lines[tl.LineNumber + 1].Start;
					 else
						CurrentPos = tl.End;
                }				
			}

			if (vDelta != 0) {
				LinePosition lp = buffer.Lines.GetLinePosition (CurrentPos);
				int nextL = lp.Line + vDelta;
				if (nextL < 0)
					nextL = 0;
				else if (nextL >= buffer.Lines.Count)
					nextL = buffer.Lines.Count - 1;

				if (nextL != lp.Line) {
					if (lastVisualColumn < 0)
						lastVisualColumn = buffer.TabulatedCol (tabSize, buffer.Lines[lp.Line].Start, CurrentPos);
					CurrentPos = buffer.Lines[nextL].AbsoluteCharPosFromTabulatedColumn (lastVisualColumn, tabSize);
				}
			}
			LinePosition lPos = buffer.Lines.GetLinePosition (CurrentPos);
			CurrentLine = lPos.Line;
			CurrentColumn = lPos.Character;

			if (IFace.Shift)
				selection = (selStartPos < CurrentPos) ?
					TextSpan.FromBounds (selStartPos, CurrentPos) :
					TextSpan.FromBounds (CurrentPos, selStartPos);
			IFace.forceTextCursor = true;			
		}

		#region GraphicObject overrides		
		public override Font Font {
			get { return base.Font; }
			set {
				base.Font = value;
				using (Context gr = new Context (IFace.surf)) {
					gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
					gr.SetFontSize (Font.Size);

					fe = gr.FontExtents;
					fe.MaxXAdvance = gr.TextExtents ("A").XAdvance;					
				}
				lineHeight = fe.Ascent + fe.Descent;
				MaxScrollY = 0;
				RegisterForGraphicUpdate ();
			}
		}
		public override int measureRawSize(LayoutingType lt)
		{
			if (lt == LayoutingType.Height)
				return (int)Math.Ceiling(lineHeight * buffer.Lines.Count) + Margin * 2;

			return (int)(fe.MaxXAdvance * longestLineCharCount) + Margin * 2 + leftMargin;
		}
		public override void OnLayoutChanges (LayoutingType layoutType)
		{
			base.OnLayoutChanges (layoutType);

			if (layoutType == LayoutingType.Height)
				updateVisibleLines ();
			else if (layoutType == LayoutingType.Width)
				updateVisibleColumns ();
		}

		protected override void UpdateCache(Context ctx)
		{
			DbgLogger.StartEvent(DbgEvtType.GOUpdateCache, this);


			paintCache (ctx, Slot + Parent.ClientRectangle.Position);		
			DbgLogger.EndEvent (DbgEvtType.GOUpdateCache);
		}
		

		class SyntaxTreePrinter {
			public int tabSize;
			public int firstPrintedLine, lastPrintedLine, currentLine, currentCol, scrollX;
			public double x, lineHeight;
			Context ctx;
			public FontExtents fe;
			public SemanticModel semanticModel;
			Dictionary<string, TextFormatting> formatting;
			TextFormatting tf;		
			string fontName;

			public SyntaxTreePrinter (CrowIDE ide, string fontName) {
				formatting = ide.SyntaxTheme;
				this.fontName = fontName;
			}

			public void Draw (SourceText buffer, SyntaxTree syntaxTree, Context ctx, int firstLine, int lastLine) {
				this.ctx = ctx;
				firstPrintedLine = firstLine;
				lastPrintedLine = lastLine;
				currentLine = 0;
				currentCol = 0;
				tf = formatting["default"];
				
				SyntaxToken tok = syntaxTree.GetRoot().FindToken (buffer.Lines[firstPrintedLine].Start);
				currentLine = buffer.Lines.GetLineFromPosition (tok.FullSpan.Start).LineNumber;

				processToken (tok);
			}
			bool cancel => currentLine > lastPrintedLine;
			bool shouldPrint => currentLine >= firstPrintedLine;
			void processToken (SyntaxToken tok) {
				if (tok.HasLeadingTrivia) {
					foreach (SyntaxTrivia trivia in tok.LeadingTrivia) {
						processTrivia (trivia);
						if (cancel)
							return;
					}
				}

				if (cancel)
					return;

				if (tok.IsKind (SyntaxKind.XmlTextLiteralNewLineToken) || tok.IsKind (SyntaxKind.EndOfFileToken))
					lineBreak ();
				else if (shouldPrint)
					print (tok);

				if (cancel)
					return;

				if (tok.HasTrailingTrivia) {
					foreach (SyntaxTrivia trivia in tok.TrailingTrivia) {
						processTrivia (trivia);
						if (cancel)
							return;
					}
				}

				if (cancel)
					return;

				if (tok.FullSpan.End < tok.SyntaxTree.Length)
					processToken (tok.GetNextToken(true, true, true, true));
			}

			void processTrivia (SyntaxTrivia trivia) {
				SyntaxKind kind = trivia.Kind ();
				if (kind == SyntaxKind.EndOfLineTrivia) {
					lineBreak ();
					return;
				}				
				if (trivia.HasStructure)
					processToken (trivia.GetStructure().GetFirstToken());
				else if (trivia.IsKind (SyntaxKind.WhitespaceTrivia))
					currentCol = trivia.TabulatedCol (tabSize, currentCol);
				else if (trivia.IsKind (SyntaxKind.DisabledTextTrivia)) {
					tf = formatting["DisabledText"];
					printMultilineTrivia (trivia);
				}else if (trivia.IsKind (SyntaxKind.MultiLineCommentTrivia)) {					
					tf = formatting["trivia"];
					printMultilineTrivia (trivia);
	            } else if (shouldPrint) {
					tf = formatting["trivia"];
					print (trivia.TabulatedText (tabSize, currentCol));				
				}
			}
			void lineBreak () {
				currentCol = 0;
				currentLine++;

			}
			
			void printMultilineTrivia (SyntaxTrivia trivia) {
            	string[] lines = Regex.Split (trivia.ToString (), @"\r\n|\r|\n|\\\n");                
				for (int i = 0; i < lines.Length - 1; i++) {
					if (shouldPrint)
						print (lines[i].TabulatedText (tabSize, currentCol));
					lineBreak ();
					if (cancel)
						return;
				}
				if (shouldPrint)
					print (lines[lines.Length - 1].TabulatedText (tabSize, currentCol));
			}
			void print (SyntaxToken tok) {
				ISymbol symbol = null;
				if (semanticModel != null) {
					SymbolInfo symbInfo = semanticModel.GetSymbolInfo (tok.Parent);
					symbol = symbInfo.Symbol;
				}

				if (symbol != null && formatting.ContainsKey (symbol.Kind.ToString ())) {
					tf = formatting[symbol.Kind.ToString()];						
				} else {
					if (symbol != null)
						Console.WriteLine ($"Symbol: Kind:{symbol.Kind}");
					SyntaxKind kind = tok.Kind ();
					if (SyntaxFacts.IsPredefinedType (kind))
						tf = formatting["PredefinedType"];
					else if (SyntaxFacts.IsAccessibilityModifier (kind))
						tf = formatting["AccessibilityModifier"];
					/*else if (SyntaxFacts.IsName (kind))
						tf = editor.formatting["name"];*/
					else if (SyntaxFacts.IsKeywordKind (kind))
						tf = formatting["keyword"];
					else if (SyntaxFacts.IsLiteralExpression (kind))
						tf = formatting["LiteralExpression"];
					else if (kind == SyntaxKind.IdentifierToken) {
						if (SyntaxFacts.IsValidIdentifier (tok.Text))
							tf = formatting["identifier"];
						else
							tf = formatting["default"];
					} else
						tf = formatting["default"];
				}				
				print (tok.ToString ());
			}
			void print (SyntaxTrivia tok) {

			}
			
			void print (string lstr) {
				
				ctx.SelectFontFace (fontName,
					tf.Italic ? FontSlant.Italic : FontSlant.Normal,
					tf.Bold ? FontWeight.Bold : FontWeight.Normal);
				ctx.SetSource (tf.Foreground);

				int diffX = currentCol - scrollX;

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

				ctx.MoveTo (x + (currentCol - scrollX - diffX) * fe.MaxXAdvance, lineHeight * (currentLine - firstPrintedLine) + fe.Ascent);
				ctx.ShowText (str);
				currentCol += lstr.Length;
	        }			
		}

        protected override void onDraw (Context gr)
		{
			DbgLogger.StartEvent(DbgEvtType.GODraw, this);

			Rectangle rBack = new Rectangle (Slot.Size);
			CrowIDE ide = IFace as CrowIDE;
			gr.SetSource (ide.SyntaxTheme["default"].Background);									
			CairoHelpers.CairoRectangle (gr, rBack, CornerRadius);
			gr.Fill ();			

			if (!IsReady || buffer == null || visibleLines == 0) {
				DbgLogger.EndEvent (DbgEvtType.GODraw);
				return;
			}

			gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
			gr.SetFontSize (Font.Size);
			gr.FontOptions = Interface.FontRenderingOptions;
			gr.Antialias = Interface.Antialias;			

			Stopwatch sw = Stopwatch.StartNew();			
			editorMutex.EnterReadLock ();

			Rectangle cb = ClientRectangle;

			SyntaxTreePrinter stp = new SyntaxTreePrinter(ide, Font.Name) {
				tabSize = tabSize,
				x = cb.X + leftMargin,
				fe = this.fe,
				lineHeight = this.lineHeight,
				scrollX = this.ScrollX,
				semanticModel = CSProjectItm.Project.Compilation?.GetSemanticModel (SyntaxTree)					
			};
			gr.SetSource (Colors.DarkBlue);
			stp.Draw (buffer, SyntaxTree, gr, ScrollY, ScrollY + visibleLines);

			editorMutex.ExitReadLock ();
			sw.Stop();
			Console.WriteLine ($"SyntaxPrinter: {sw.ElapsedMilliseconds}(ms) {sw.ElapsedTicks}(ticks)");
			DbgLogger.EndEvent (DbgEvtType.GODraw);
		}
		#endregion

		#region Mouse handling


        public int HoverLine {
			get { return hoverLine; }
			set {
				if (hoverLine == value)
					return;
				hoverLine = value;
				NotifyValueChanged ("HoverLine", hoverLine);
				//NotifyValueChanged ("HoverError", buffer [hoverLine].exception);
			}
		}
		public int HoverColumn {
			get { return hoverColumn; }
			set {
				if (hoverColumn == value)
					return;
				hoverColumn = value;
				NotifyValueChanged ("HoverColumn", hoverColumn);
				//NotifyValueChanged ("HoverError", buffer [hoverLine].exception);
			}
		}

		[XmlIgnore]public int CurrentPos {
			get => currentPos;
			set {
				if (currentPos == value)
					return;
				currentPos = value;
				NotifyValueChangedAuto (currentPos);
			}
		}

		public override void onMouseMove (object sender, MouseMoveEventArgs e)
		{
			//base.onMouseMove (sender, e);

			Rectangle screenSlot = ScreenCoordinates (Slot);
			mouseLocalPos = e.Position - screenSlot.TopLeft - ClientRectangle.TopLeft;

			if (buffer == null) {
				HoverLine = 0;
				HoverColumn = 0;
				return;
			}

			int hvl = (int)Math.Max (0, Math.Floor (mouseLocalPos.Y / lineHeight));
			HoverLine = Math.Min (buffer.Lines.Count - 1, Math.Min (visibleLines, hvl));
			int curVisualCol = ScrollX + (int)Math.Round ((mouseLocalPos.X - leftMargin) / fe.MaxXAdvance);
			
			int hcol = buffer.Lines[hoverLine].GetCharPosFromVisualColumn (curVisualCol, tabSize);
			HoverColumn = Math.Abs (hcol);

			hoverPos = buffer.Lines.GetPosition (new LinePosition (hoverLine, hoverColumn));
			NotifyValueChanged ("VisualColumn", curVisualCol);

			if (IFace.IsDown (MouseButton.Left)) {
				if (hoverPos != selStartPos)
					selection = (selStartPos < hoverPos) ?
						TextSpan.FromBounds (selStartPos, hoverPos) :
						TextSpan.FromBounds (hoverPos, selStartPos);
				RegisterForRedraw ();
			} else {
				if (mouseLocalPos.X < leftMargin)
					IFace.MouseCursor = MouseCursor.arrow;
				else
					IFace.MouseCursor = MouseCursor.ibeam;				
			}
		}
        public override void onMouseEnter (object sender, MouseMoveEventArgs e)
		{
			base.onMouseEnter (sender, e);
			if (e.X - ScreenCoordinates(Slot).X < leftMargin + ClientRectangle.X)
				IFace.MouseCursor = MouseCursor.arrow;
			else
				IFace.MouseCursor = MouseCursor.ibeam;
		}
		public override void onMouseLeave (object sender, MouseMoveEventArgs e)
		{
			base.onMouseLeave (sender, e);
			IFace.MouseCursor = MouseCursor.arrow;
		}
		public override void onMouseDown (object sender, MouseButtonEventArgs e)
		{
			if (mouseLocalPos.X >= leftMargin)
				base.onMouseDown (sender, e);			

			CurrentLine = HoverLine;
			CurrentColumn = HoverColumn;
			CurrentPos = selStartPos = hoverPos;

			RegisterForRedraw ();
			selection = default;
		}
		public override void onMouseUp (object sender, MouseButtonEventArgs e)
		{
			base.onMouseUp (sender, e);
		}

		public override void onMouseDoubleClick (object sender, MouseButtonEventArgs e)
		{
			//doubleClicked = true;
			base.onMouseDoubleClick (sender, e);

			selection = TextSpan.FromBounds (
				buffer.GetWordStart (CurrentPos),
				buffer.GetWordEnd (CurrentPos));
			RegisterForRedraw ();
		}
		#endregion

		#region Keyboard handling
		public override void onKeyDown (object sender, KeyEventArgs e)
		{
			//base.onKeyDown (sender, e);

			Key key = e.Key;

			if (IFace.Ctrl) {
				switch (key) {
				case Key.S:
					projFile.Save ();
					break;
				case Key.W:
					editorMutex.EnterWriteLock ();
					if (IFace.Shift)
						redo ();
					else
						undo ();
					editorMutex.ExitWriteLock ();
					break;
				default:
					base.onKeyDown (sender, e);
					return;
				}
			}

			switch (key) {
			case Key.Backspace:
				if (selection.IsEmpty) {
					if (CurrentPos < 1)
						return;
					selection = TextSpan.FromBounds (CurrentPos - 1, CurrentPos);
				}
				replaceSelection ("");
				break;			
			case Key.Delete:
				if (selection.IsEmpty) {
					if (CurrentPos >= buffer.Length)
						return;
					selection = TextSpan.FromBounds (CurrentPos, CurrentPos + 1);
				} else if (IFace.Shift)
					IFace.Clipboard = buffer.GetSubText (selection).ToString ();
				replaceSelection ("");
				break;
			case Key.Insert:
				if (selection.IsEmpty)
					selection = TextSpan.FromBounds (CurrentPos, CurrentPos);
				else if (IFace.Ctrl) {
					IFace.Clipboard = buffer.GetSubText (selection).ToString ();
					break;
				}				
				if (IFace.Shift)
					replaceSelection (IFace.Clipboard);
				break;
			case Key.Enter:
			case Key.KeypadEnter:
				if (!selection.IsEmpty)
					replaceSelection ("");
				selection = TextSpan.FromBounds (CurrentPos, CurrentPos);
				replaceSelection ("\n");
				break;
			case Key.Escape:
				selection = default;
				break;
			case Key.Home:
				if (IFace.Ctrl)
					move (-CurrentPos);
				else
					move (buffer.Lines[currentLine].Start - CurrentPos);
				break;
			case Key.End:
				if (IFace.Ctrl)
					move (buffer.Length - CurrentPos);
				else
					move (buffer.Lines[currentLine].End - CurrentPos);
				break;
			case Key.Left:
				if (IFace.Ctrl)
					movePreviousToken ();
				else
					move (-1);
				break;
			case Key.Right:
				if (IFace.Ctrl)
					moveNextToken ();
				else
					move (1);
				break;
			case Key.Up:
				move (0, -1);
				break;
			case Key.Down:
				move (0, 1);
				break;
			case Key.PageUp:
				move (0, -visibleLines);
				break;
			case Key.PageDown:
				move (0, visibleLines);
				break;
			case Key.Tab:
				if (selection.IsEmpty)
					selection = TextSpan.FromBounds (CurrentPos, CurrentPos);
				LinePositionSpan lps = buffer.Lines.GetLinePositionSpan (selection);
				if (IFace.Shift) {
					for (int i = lps.Start.Line; i <= lps.End.Line; i++) {
						int pos = buffer.Lines [i].Start;
						int delta = 0;
						if (buffer [pos] == '\t')
							delta = 1;
						else {
							while (delta <= tabSize && buffer [pos + delta] == ' ')
								delta++;
						}
						if (delta > 0)
							buffer = buffer.Replace (TextSpan.FromBounds (pos, pos + delta), "");
					}
					selection = TextSpan.FromBounds (buffer.Lines [lps.Start.Line].Start, buffer.Lines [lps.End.Line].End);
					RegisterForRedraw ();
				} else {
					if (lps.Start.Line == lps.End.Line)
						replaceSelection ("\t");
					else {
						for (int i = lps.Start.Line; i <= lps.End.Line; i++) {
							int pos = buffer.Lines [i].Start;
							buffer = buffer.Replace (TextSpan.FromBounds (pos, pos), "\t");
						}
						selection = TextSpan.FromBounds (buffer.Lines [lps.Start.Line].Start, buffer.Lines [lps.End.Line].End);
						RegisterForRedraw ();
					}
				}
				break;
			//case Key.F8:
			//	toogleFolding (buffer.CurrentLine);
			//	break;
			default:
				base.onKeyDown (sender, e);
				return;
			}
			RegisterForGraphicUpdate ();
		}
		void movePreviousToken () {
			if (SyntaxTree.TryGetRoot (out SyntaxNode node)) {
				SyntaxToken tok = node.FindToken (CurrentPos, true);
				if (tok.SpanStart == CurrentPos) {
                    tok = tok.GetPreviousToken (false, true, true, true);
					while (tok.IsWhiteSpaceOrNewLine ())
						tok = tok.GetPreviousToken (false, true, true, true);
					moveTo (tok.SpanStart);
				} else if (!tok.Span.Contains (CurrentPos) ||  tok.IsComment ()) {
					moveTo (buffer.GetWordStart (CurrentPos));
				} else
					moveTo (tok.SpanStart);
			}
		}
		void moveNextToken () {
			if (SyntaxTree.TryGetRoot (out SyntaxNode node)) {
				SyntaxToken tok = node.FindToken (CurrentPos, true);
				if (tok.Span.End == CurrentPos) {
					tok = tok.GetNextToken (false, true, true, true);
					while (tok.IsWhiteSpaceOrNewLine ())
						tok = tok.GetNextToken (false, true, true, true);
					moveTo (tok.Span.End);
				} else if (!tok.Span.Contains (CurrentPos) || tok.IsComment ()) {
					moveTo (buffer.GetWordEnd (CurrentPos));
				} else
					moveTo (tok.Span.End);
			}
		}


		void moveTo (int newPos) =>	move (newPos - CurrentPos);
		public override void onKeyPress (object sender, KeyPressEventArgs e)
		{
			//base.onKeyPress (sender, e);
			if (selection.IsEmpty)
				selection = TextSpan.FromBounds (CurrentPos, CurrentPos);
			string str = e.KeyChar.ToString ();
			replaceSelection (str);
		}
		Stack<TextChange> undoStack = new Stack<TextChange> ();
		Stack<TextChange> redoStack = new Stack<TextChange> ();


		void replaceSelection (string newText)
		{
			TextChange tch = new TextChange (selection, newText);
			undoStack.Push (tch.Inverse (buffer));
			redoStack.Clear ();
			apply (tch);
		}

		void apply (TextChange tch) {
			editorMutex.EnterWriteLock();

			buffer = buffer.WithChanges (tch);

			SyntaxTree = SyntaxTree.WithChangedText (buffer);

			if (string.IsNullOrEmpty (tch.NewText))
				CurrentPos = tch.Span.Start;
			else
				CurrentPos = tch.Span.Start + tch.NewText.Length;

			selection = default;
			updateMaxScrollY();

			RegisterForRedraw ();
			EditorIsDirty = true;

			editorMutex.ExitWriteLock();

			CMDUndo.CanExecute = undoStack.Count > 0;
			CMDRedo.CanExecute = redoStack.Count > 0;
			IFace.forceTextCursor = true;			
		}

        protected override void undo () {
			if (undoStack.TryPop (out TextChange tch)) {
				redoStack.Push (tch.Inverse (buffer));
				apply (tch);
			}
		}

		protected override void redo () {
			if (redoStack.TryPop (out TextChange tch)) {
				undoStack.Push (tch.Inverse (buffer));
				apply (tch);
			}
		}

		protected override void cut () {
            throw new NotImplementedException ();
        }

        protected override void copy () {
            throw new NotImplementedException ();
        }

        protected override void paste () {
            throw new NotImplementedException ();
        }
        #endregion

		#region IEditableTextWidget implementatation		
		public virtual bool DrawCursor (Context ctx, out Rectangle rect) {
			Color cursorColor = Colors.Black;			
			Rectangle cb = ClientRectangle;
			if (!IsReady || buffer == null || visibleLines == 0) {
				rect = default;
				return false;
			}
			TextLine tl = buffer.Lines.GetLineFromPosition (CurrentPos);
			int visualCol = buffer.TabulatedCol (tabSize, tl.Start, CurrentPos) - ScrollX;
			int visualLine = tl.LineNumber - ScrollY;
			if (visualLine < 0) {
				rect = default;
				return false;
			}			
			Rectangle cursor = new Rectangle(
				cb.X + (int)(visualCol * fe.MaxXAdvance) + leftMargin,
				cb.Y + (int)(visualLine * lineHeight), 1, (int)lineHeight);
			Rectangle c = ScreenCoordinates (cursor + Slot.Position + ClientRectangle.Position);
			ctx.ResetClip ();
			ctx.SetSource (cursorColor);
			ctx.LineWidth = 1.0;
			ctx.MoveTo (0.5 + c.X, c.Y);
			ctx.LineTo (0.5 + c.X, c.Bottom);
			ctx.Stroke ();
			rect = c;
			return true;
		}
		#endregion
				
    }
}