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

namespace Crow.Coding
{
	/// <summary>
	/// Scrolling text box optimized for monospace fonts, for coding
	/// </summary>
	public class RoslynEditor : Editor {
		internal Dictionary<string, TextFormatting> formatting = new Dictionary<string, TextFormatting> ();

		#region CTOR
		public RoslynEditor () : base () {

			formatting["default"] = new TextFormatting (Colors.Jet, Colors.Transparent);
			formatting["keyword"] = new TextFormatting (Colors.Blue, Colors.Transparent);
			formatting["DocumentationComment"] = new TextFormatting (Colors.MediumSeaGreen, Colors.Transparent, false, true);
			formatting["Documentation"] = new TextFormatting (Colors.GreenYellow, Colors.Transparent);

			formatting["trivia"] = new TextFormatting (Colors.Grey, Colors.Transparent, false, true);

			formatting["PreprocessorDirective"] = new TextFormatting (Colors.DeepPink, Colors.Transparent, true);
			formatting["PreprocessorMessage"] = new TextFormatting (Colors.Black, Colors.Transparent, true);
			formatting["PredefinedType"] = new TextFormatting (Colors.DarkCyan, Colors.Transparent, false);
			formatting["AccessibilityModifier"] = new TextFormatting (Colors.RoyalBlue, Colors.Transparent, false);
			formatting["DisabledText"] = new TextFormatting (Colors.Grey, Colors.Transparent);

			formatting["identifier"] = new TextFormatting (Colors.Onyx, Colors.Transparent, true);
			formatting["LiteralExpression"] = new TextFormatting (Colors.FireBrick, Colors.Transparent, false, true);

			formatting["error"] = new TextFormatting (Colors.Red, Colors.Transparent);
			/*formatting["PrimaryFunction"] = new TextFormatting (Colors.DarkRed, Colors.Transparent, true);
			formatting["name"] = new TextFormatting (Colors.DarkRed, Colors.Transparent, true);*/

			formatting["TypeSyntax"] = new TextFormatting (Colors.DarkCyan, Colors.Transparent);
			formatting["TypeDeclaration"] = new TextFormatting (Colors.Lavender, Colors.Transparent);


			formatting["Punctuation"] = new TextFormatting (Colors.Black, Colors.Transparent, false);
			
			
			formatting["ContextualKeyword"] = new TextFormatting (Colors.DarkBlue, Colors.Transparent, true);
			
			
			formatting["InstanceExpression"] = new TextFormatting (Colors.Jet, Colors.Transparent);
			
			formatting["NamespaceMemberDeclaration"] = new TextFormatting (Colors.Jet, Colors.Transparent);

			printer = new SyntaxNodePrinter (this);
			foldingManager = new FoldingManager (this);

			/*formatting ["constant"] = new TextFormatting (Color.Blue, Color.Transparent, true);
			formatting ["primitive"] = new TextFormatting (Color.DarkCyan, Color.Transparent);
			formatting ["operator"] = new TextFormatting (Color.DarkRed, Color.Transparent, true);
			formatting ["modifier"] = new TextFormatting (Color.RoyalBlue, Color.Transparent);
			formatting ["typekind"] = new TextFormatting (Color.OliveDrab, Color.Transparent);
			formatting ["async"] = new TextFormatting (Color.YellowGreen, Color.Transparent);
			formatting ["linq"] = new TextFormatting (Color.Yellow, Color.Transparent);
			formatting ["preproc"] = new TextFormatting (Color.DarkOrange, Color.Transparent, true);
			formatting ["comment"] = new TextFormatting (Color.Green, Color.Transparent, false, true);

			formatting ["PredefinedType"] = new TextFormatting (Color.Red, Color.Transparent, true);
			formatting ["identifier"] = new TextFormatting (Color.Jet, Color.Transparent, true);
			formatting ["litteral"] = new TextFormatting (Color.Crimson, Color.Transparent, false, true);*/

			/*formatting.Add ((int)XMLParser.TokenType.AttributeName, new TextFormatting (Color.DarkSlateGrey, Color.Transparent));
			formatting.Add ((int)XMLParser.TokenType.ElementName, new TextFormatting (Color.DarkBlue, Color.Transparent));
			formatting.Add ((int)XMLParser.TokenType.ElementStart, new TextFormatting (Color.Black, Color.Transparent));
			formatting.Add ((int)XMLParser.TokenType.ElementEnd, new TextFormatting (Color.Black, Color.Transparent));
			formatting.Add ((int)XMLParser.TokenType.ElementClosing, new TextFormatting (Color.Black, Color.Transparent));

			formatting.Add ((int)XMLParser.TokenType.AttributeValueOpening, new TextFormatting (Color.Crimson, Color.Transparent));
			formatting.Add ((int)XMLParser.TokenType.AttributeValueClosing, new TextFormatting (Color.Crimson, Color.Transparent));
			formatting.Add ((int)XMLParser.TokenType.AttributeValue, new TextFormatting (Color.FireBrick, Color.Transparent, false, true));
			formatting.Add ((int)XMLParser.TokenType.XMLDecl, new TextFormatting (Color.ForestGreen, Color.Transparent));
			formatting.Add ((int)XMLParser.TokenType.Content, new TextFormatting (Color.DimGrey, Color.Transparent, false, true));

			formatting.Add ((int)BufferParser.TokenType.BlockComment, new TextFormatting (Color.Grey, Color.Transparent, false, true));
			formatting.Add ((int)BufferParser.TokenType.LineComment, new TextFormatting (Color.Grey, Color.Transparent, false, true));
			formatting.Add ((int)BufferParser.TokenType.OperatorOrPunctuation, new TextFormatting (Color.Black, Color.Transparent));
			formatting.Add ((int)8300, new TextFormatting (Color.Teal, Color.Transparent));*/
		}
		#endregion

		#region private and protected fields

		int tabSize = 4;
		string oldSource = "";
		
		volatile bool isDirty = false;

		internal const int leftMarginGap = 3;   //gap between items in margin and text
		internal const int foldSize = 9;        //folding rectangles size
		internal int foldMargin = 9;            // { get { return parser == null ? 0 : parser.SyntacticTreeMaxDepth * foldHSpace; }}//folding margin size

		internal bool foldingEnabled = true;


		[XmlIgnore] public int leftMargin { get; private set; } = 0;    //margin used to display line numbers, folding errors,etc...
		internal int visibleLines = 1;
		int visibleColumns = 1;		
		int[] printedLines;                     //printed line indices in source


		internal int hoverPos, selStartPos;//absolute char index in buffer source

		TextSpan selection = default;
		SourceText buffer = SourceText.From ("");
		SyntaxTree syntaxTree;
		SyntaxNodePrinter printer;
		internal FoldingManager foldingManager;
		internal int totalLines => buffer.Lines.Count;
		internal TextSpan visibleSpan => TextSpan.FromBounds (buffer.Lines[ScrollY].Start, buffer.Lines[ScrollY + visibleLines].End);

		//SourceText buffer => syntaxTree == null ?  : syntaxTree.TryGetText (out SourceText src) ? src : SourceText.From ("");
		public SyntaxTree SyntaxTree {
			get => syntaxTree;
			private set {
				if (syntaxTree == value)
					return;
				syntaxTree = value;
				/*CSProjectItem cspi = ProjectNode as CSProjectItem;
				if (cspi != null)
					cspi.SyntaxTree = syntaxTree;*/
				NotifyValueChanged ("SyntaxTree", syntaxTree);
			}
		}

		void updateFolds () {
			if (syntaxTree == null)
				return;
			//Console.WriteLine ("update folds");
			foldingManager.CreateFolds (syntaxTree.GetRoot ());
			RegisterForRedraw ();
		}
		//absolute char pos in text of start of folds		


		//Dictionary<int, TextFormatting> formatting = new Dictionary<int, TextFormatting>();

		Color selBackground;
		Color selForeground;

		protected Rectangle rText;
		protected FontExtents fe;
		protected TextExtents te;

		Point mouseLocalPos;
		#endregion

		internal void measureLeftMargin () {
			leftMargin = 0;
			if (printLineNumbers)
				leftMargin += (int)Math.Ceiling ((double)buffer?.Lines.Count.ToString ().Length * fe.MaxXAdvance) + 6;
			if (foldingEnabled)
				leftMargin += foldMargin;
			if (leftMargin > 0)
				leftMargin += leftMarginGap;
		}

		int longestLineCharCount = 0, longestLineIdx = 0;

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

			//			Debug.WriteLine ("SourceEditor: Find Longest line and update maxscrollx: {0} visible cols:{1}", MaxScrollX, visibleColumns);
		}
		/// <summary>
		/// Updates visible line in widget, adapt max scroll y and updatePrintedLines
		/// </summary>
		void updateVisibleLines () {
			visibleLines = (int)Math.Floor ((double)ClientRectangle.Height / (fe.Ascent + fe.Descent));
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
			int unfoldedLines = buffer.Lines.Count - printer.SkippedLines;			
			MaxScrollY = Math.Max (0, unfoldedLines - visibleLines);
			NotifyValueChanged ("ChildHeightRatio", Slot.Height * visibleLines / unfoldedLines);
		}
		
		void toogleFolding (int line) {
			if (foldingManager.TryGetFold (line, out Fold fold)) {
				fold.IsFolded = !fold.IsFolded;
				RegisterForRedraw ();
			}
		}

		#region Editor overrides
		protected override void updateEditorFromProjFile () {
			Debug.WriteLine ("\t\tSourceEditor updateEditorFromProjFile");

			//buffer.editMutex.EnterWriteLock ();
			loadSource ();
			//buffer.editMutex.ExitWriteLock ();

			isDirty = false;
			oldSource = projFile.Source;
			/*CurrentLine = requestedLine;
			CurrentColumn = requestedCol;*/
			//projFile.RegisteredEditors [this] = true;
		}
		protected override void updateProjFileFromEditor () {
			Debug.WriteLine ("\t\tSourceEditor updateProjFileFromEditor");

			char[] chars = new char[buffer.Length];
			buffer.CopyTo (0, chars, 0, buffer.Length);
			projFile.UpdateSource (this, new string (chars));
		}
		protected override bool EditorIsDirty {
			get { return isDirty; }
			set { isDirty = value; }
		}
		protected override bool IsReady {
			get { return projFile != null; }
		}
		#endregion

		#region Buffer events handlers
		void Buffer_BufferCleared (object sender, EventArgs e) {
			editorMutex.EnterWriteLock ();

			longestLineCharCount = 0;
			longestLineIdx = 0;
			measureLeftMargin ();
			MaxScrollX = MaxScrollY = 0;

			RegisterForGraphicUpdate ();
			notifyPositionChanged ();
			isDirty = true;

			editorMutex.ExitWriteLock ();
		}
		void Buffer_LineAdditionEvent (object sender, CodeBufferEventArgs e) {
			/*for (int i = 0; i < e.LineCount; i++) {
				int lptr = e.LineStart + i;
				int charCount = buffer[lptr].PrintableLength;
				if (charCount > buffer.longestLineCharCount) {
					buffer.longestLineIdx = lptr;
					buffer.longestLineCharCount = charCount;
				}else if (lptr <= buffer.longestLineIdx)
					buffer.longestLineIdx++;
				if (parser == null)
					continue;
				parser.TryParseBufferLine (e.LineStart + i);
			}

			if (parser != null)
				parser.reparseSource ();*/

			measureLeftMargin ();


			updateMaxScrollY ();
			RegisterForGraphicUpdate ();
			notifyPositionChanged ();
			isDirty = true;
		}
		void Buffer_LineRemoveEvent (object sender, CodeBufferEventArgs e) {
			/*bool trigFindLongestLine = false;
			for (int i = 0; i < e.LineCount; i++) {
				int lptr = e.LineStart + i;
				if (lptr <= buffer.longestLineIdx)
					trigFindLongestLine = true;
			}
			if (trigFindLongestLine)
				findLongestLineAndUpdateMaxScrollX ();*/

			measureLeftMargin ();

			updateMaxScrollY ();
			RegisterForGraphicUpdate ();
			notifyPositionChanged ();
			isDirty = true;
		}
		void Buffer_LineUpadateEvent (object sender, CodeBufferEventArgs e) {
			/*bool trigFindLongestLine = false;
			for (int i = 0; i < e.LineCount; i++) {

				int lptr = e.LineStart + i;
				if (lptr == buffer.longestLineIdx)
					trigFindLongestLine = true;
				else if (buffer[lptr].PrintableLength > buffer.longestLineCharCount) {
					buffer.longestLineCharCount = buffer[lptr].PrintableLength;
					buffer.longestLineIdx = lptr;
				}
			}
			if (trigFindLongestLine)
				findLongestLineAndUpdateMaxScrollX ();*/

			RegisterForGraphicUpdate ();
			notifyPositionChanged ();
			isDirty = true;
		}
		void Buffer_PositionChanged (object sender, EventArgs e) {
			//Console.WriteLine ("Position changes: ({0},{1})", buffer.CurrentLine, buffer.CurrentColumn);
			/*int cc = buffer.CurrentTabulatedColumn;

			if (cc > visibleColumns + ScrollX) {
				ScrollX = cc - visibleColumns;
			} else if (cc < ScrollX)
				ScrollX = cc;
			
			RegisterForGraphicUpdate ();
			updateOnScreenCurLineFromBuffCurLine ();
			notifyPositionChanged ();*/
		}

		void Buffer_SelectionChanged (object sender, EventArgs e) {
			RegisterForGraphicUpdate ();
		}
		void Buffer_FoldingEvent (object sender, CodeBufferEventArgs e) {

			updateMaxScrollY ();
			RegisterForGraphicUpdate ();
		}
		#endregion

		void notifyPositionChanged () {
			/*try {				
				NotifyValueChanged ("CurrentLine", CurrentLine+1);
				NotifyValueChanged ("CurrentColumn", buffer.CurrentColumn+1);
				NotifyValueChanged ("CurrentLineHasError", CurrentLineHasError);
				NotifyValueChanged ("CurrentLineError", CurrentLineError);
			} catch (Exception ex) {
				Console.WriteLine (ex.ToString ());
			}*/
		}

		int currentLine, currentColumn;
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
		public ParserException CurrentLineError {
			get { return null; }// buffer?.CurrentCodeLine?.exception; }
		}
		public bool CurrentLineHasError {
			get { return false; }
		}
		#endregion


		void loadSource () {

			try {
				buffer = SourceText.From (projFile.Source);
				//buffer.Container.TextChanged
				SyntaxTree = CSharpSyntaxTree.ParseText (buffer);
				Task.Run (() => updateFolds ());
			} catch (Exception ex) {
				Debug.WriteLine (ex.ToString ());
			}

			//projFile.RegisteredEditors [this] = true;

			updateMaxScrollY ();
			measureLeftMargin ();
			findLongestLineAndUpdateMaxScrollX ();


			RegisterForGraphicUpdate ();
		}

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
			return tc; //buffer [line].Content.Substring (0, col).Replace ("\t", new String (' ', Interface.TAB_SIZE)).Length;
		}
		int getTabulatedColumn (Point pos) => getTabulatedColumn (pos.X,pos.Y);

		int lastVisualColumn = -1;
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
				MaxScrollY = 0;
				RegisterForGraphicUpdate ();
			}
		}
		public override int measureRawSize(LayoutingType lt)
		{
			if (lt == LayoutingType.Height)
				return (int)Math.Ceiling((fe.Ascent+fe.Descent) * buffer.Lines.Count) + Margin * 2;

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

		protected override void onDraw (Context gr)
		{
			if (selection.IsEmpty)
				base.onDraw (gr);

			if (syntaxTree == null)
				return;

			gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
			gr.SetFontSize (Font.Size);
			gr.FontOptions = Interface.FontRenderingOptions;
			gr.Antialias = Interface.Antialias;

			Foreground.SetAsSource (IFace, gr);

			editorMutex.EnterReadLock ();

			syntaxTree = syntaxTree.WithChangedText (buffer);			

			Rectangle cb = ClientRectangle;

			int skippedLinesSave = printer.SkippedLines;
			printer.Draw (gr, syntaxTree.GetRoot ());
			printedLines = printer.printedLinesNumbers;
			if (skippedLinesSave != printer.SkippedLines)
				updateMaxScrollY ();

			#region draw text cursor	
			if (!selection.IsEmpty) {
				Color selbg = this.SelectionBackground;

				TextLine startTl = buffer.Lines.GetLineFromPosition (selection.Start);
				TextLine endTl = buffer.Lines.GetLineFromPosition (selection.End);

				if (endTl.LineNumber < ScrollY || startTl.LineNumber >= ScrollY + visibleLines) {
					editorMutex.ExitReadLock ();
					return;
				}
				int visualColStart = buffer.TabulatedCol (tabSize, startTl.Start, selection.Start) - ScrollX;
				int visualColEnd = buffer.TabulatedCol (tabSize, endTl.Start, selection.End) - ScrollX;
				int visualLineStart = Array.IndexOf (printedLines, startTl.LineNumber);

				double xStart = cb.X + visualColStart * fe.MaxXAdvance + leftMargin;
				double yStart = cb.Y + visualLineStart * (fe.Ascent + fe.Descent);
				RectangleD r = new RectangleD (xStart,
					yStart, (visualColEnd - visualColStart) * fe.MaxXAdvance, (fe.Ascent + fe.Descent));

				gr.Operator = Operator.DestOver;
				gr.SetSource (selbg);

				if (startTl == endTl) {
					gr.Rectangle (r);
					gr.Fill ();
				}else {					
					r.Width = Math.Min (cb.Width - xStart, buffer.TabulatedCol (tabSize, selection.Start, startTl.GetEnd (selection.Start) - ScrollX) * fe.MaxXAdvance);
					gr.Rectangle (r);
					gr.Fill ();
					int visualLineEnd = Array.IndexOf (printedLines, endTl.LineNumber);
					r.Left = cb.X + leftMargin;
					for (int l = visualLineStart + 1; l < (visualLineEnd < 0 ? printedLines.Length : visualLineEnd); l++) {
						r.Top += (fe.Ascent + fe.Descent);
						TextLine tl = buffer.Lines [printedLines [l]];
						r.Width = Math.Min(cb.Width - leftMargin, buffer.TabulatedCol (tabSize, tl.Start, tl.GetEnd () - ScrollX) * fe.MaxXAdvance);
						gr.Rectangle (r);
						gr.Fill ();
					}
					if (visualLineEnd >= 0) {
						r.Top += (fe.Ascent + fe.Descent);
						r.Width = Math.Min (cb.Width - leftMargin, Math.Max (1, visualColEnd) * fe.MaxXAdvance);
						gr.Rectangle (r);
						gr.Fill ();
					}
				}
				base.onDraw (gr);
				gr.Operator = Operator.Over;

			} else if (HasFocus && printedLines != null && CurrentPos >= 0) {
				//Draw cursor
				gr.LineWidth = 1.0;

				TextLine tl = buffer.Lines.GetLineFromPosition (CurrentPos);
				int visualCol = buffer.TabulatedCol (tabSize, tl.Start, CurrentPos) - ScrollX;
				int visualLine = Array.IndexOf (printedLines, tl.LineNumber);
				if (visualLine >= 0) {
					double cursorX = cb.X + visualCol * fe.MaxXAdvance + leftMargin;
					gr.MoveTo (0.5 + cursorX, cb.Y + visualLine * (fe.Ascent + fe.Descent));
					gr.LineTo (0.5 + cursorX, cb.Y + (visualLine + 1) * (fe.Ascent + fe.Descent));
					gr.Stroke ();
				}
			}
            #endregion

            foreach (Diagnostic diag in syntaxTree.GetDiagnostics ()) {
				printUnderline (gr, cb, diag.Location);
                foreach (Location al in diag.AdditionalLocations) {
					printUnderline (gr, cb, al);
				}
			} 
			editorMutex.ExitReadLock ();

		}
		#endregion

		void printUnderline (Context gr, Rectangle cb, Location loc) {
			FileLinePositionSpan flps = loc.GetLineSpan ();

			TextLine startTl = buffer.Lines[flps.StartLinePosition.Line];
			TextLine endTl = buffer.Lines[flps.EndLinePosition.Line];

			if (flps.EndLinePosition.Line < ScrollY || flps.StartLinePosition.Line >= ScrollY + visibleLines)
				return;
			int visualColStart = buffer.TabulatedCol (tabSize, startTl.Start, loc.SourceSpan.Start) - ScrollX;
			int visualColEnd = buffer.TabulatedCol (tabSize, endTl.Start, loc.SourceSpan.End) - ScrollX;
			int visualLineStart = Array.IndexOf (printedLines, startTl.LineNumber);

			double xStart = cb.X + visualColStart * fe.MaxXAdvance + leftMargin;
			double yStart = cb.Y + visualLineStart * (fe.Ascent + fe.Descent);
			RectangleD r = new RectangleD (xStart,
				yStart, (visualColEnd - visualColStart) * fe.MaxXAdvance, (fe.Ascent + fe.Descent));

			gr.LineWidth = 1;
			gr.SetSource (Colors.Red);

			if (startTl == endTl) {
				gr.MoveTo (r.BottomLeft);
				if (r.Width > 0)
					gr.LineTo (r.BottomRight);
				else
					gr.RelLineTo (10, 0);
				gr.Stroke ();
			} else {
				r.Width = Math.Min (cb.Width - xStart, buffer.TabulatedCol (tabSize, loc.SourceSpan.Start, startTl.GetEnd (loc.SourceSpan.Start) - ScrollX) * fe.MaxXAdvance);
				gr.MoveTo (r.BottomLeft);
				if (r.Width > 0)
					gr.LineTo (r.BottomRight);
				else
					gr.RelLineTo (10, 0);
				gr.Stroke ();
				int visualLineEnd = Array.IndexOf (printedLines, endTl.LineNumber);
				r.Left = cb.X + leftMargin;
				for (int l = visualLineStart + 1; l < (visualLineEnd < 0 ? printedLines.Length : visualLineEnd); l++) {
					r.Top += (fe.Ascent + fe.Descent);
					TextLine tl = buffer.Lines[printedLines[l]];
					r.Width = Math.Min (cb.Width - leftMargin, buffer.TabulatedCol (tabSize, tl.Start, tl.GetEnd () - ScrollX) * fe.MaxXAdvance);
					gr.MoveTo (r.BottomLeft);
					if (r.Width > 0)
						gr.LineTo (r.BottomRight);
					else
						gr.RelLineTo (10, 0);
					gr.Stroke ();
				}
				if (visualLineEnd >= 0) {
					r.Top += (fe.Ascent + fe.Descent);
					r.Width = Math.Min (cb.Width - leftMargin, Math.Max (1, visualColEnd) * fe.MaxXAdvance);
					gr.MoveTo (r.BottomLeft);
					if (r.Width > 0)
						gr.LineTo (r.BottomRight);
					else
						gr.RelLineTo (10, 0);
					gr.Stroke ();
				}
			}
		}

		#region Mouse handling

		int hoverLine = -1, hoverColumn = -1;
        private int currentPos;

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
		public override void onMouseMove (object sender, MouseMoveEventArgs e)
		{
			base.onMouseMove (sender, e);

			mouseLocalPos = e.Position - ScreenCoordinates(Slot).TopLeft - ClientRectangle.TopLeft;

			if (buffer == null || printedLines == null) {
				HoverLine = 0;
				HoverColumn = 0;
				return;
			}

			int hvl = (int)Math.Max (0, Math.Floor (mouseLocalPos.Y / (fe.Ascent + fe.Descent)));
			HoverLine = printedLines[Math.Min (printedLines.Length - 1, hvl)];
			int curVisualCol = ScrollX + (int)Math.Round ((mouseLocalPos.X - leftMargin) / fe.MaxXAdvance);
			HoverColumn = buffer.Lines[hoverLine].GetCharPosFromVisualColumn (curVisualCol, tabSize);
			hoverPos = buffer.Lines.GetPosition (new LinePosition (hoverLine, hoverColumn));

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

			/*if (syntaxTree != null) {
				try {
					SyntaxNode root = syntaxTree.GetRoot ();				
					SyntaxToken tok = root.FindToken (hoverPos, true);
					SyntaxNode sn = root.FindNode (TextSpan.FromBounds (hoverPos, hoverPos + 1), true, true);
					Console.WriteLine($"TOK:{tok.CSKind ().ToString ()}, {tok.RawKind.ToString ()}");
					Console.WriteLine($"node: {sn.Kind ()} {sn.ToString()}");
					NotifyValueChanged ("CurrentToken", tok);					
					if (Compilation == null)
						return;
					
					SemanticModel model = Compilation.GetSemanticModel (syntaxTree);
					SymbolInfo si = model.GetSymbolInfo (sn);
					Console.WriteLine ($"Symb: {si} {si.CandidateReason} {si.Symbol}");
					foreach (ISymbol s in si.CandidateSymbols)
					{
						Console.WriteLine (s);						
					}
					
				} catch (Exception ex) {					
					Console.WriteLine (ex);
				}

            }*/

			/*if (!HasFocus || !buffer.SelectionInProgress)
				return;

			//mouse is down
			updateCurrentPosFromMouseLocalPos();
			buffer.SetSelEndPos ();*/
		}
		CSProjectItem CSProjectItm => this.projFile as CSProjectItem;
		public Compilation Compilation {
			get => CSProjectItm.Project.Compilation;
			set {
				CSProjectItm.Project.Compilation = value;
			}
		}
		public override void onMouseDown (object sender, MouseButtonEventArgs e)
		{
			if (!Focusable)
				return;

			if (mouseLocalPos.X >= leftMargin)
				base.onMouseDown (sender, e);			

			/*if (doubleClicked) {
				doubleClicked = false;
				return;
			}*/

			if (mouseLocalPos.X < leftMargin) {
				toogleFolding (hoverLine);
				//toogleFolding (buffer.IndexOf (PrintedLines [(int)Math.Max (0, Math.Floor (mouseLocalPos.Y / (fe.Ascent+fe.Descent)))]));
				return;
			}
			CurrentLine = HoverLine;
			CurrentColumn = HoverColumn;
			CurrentPos = selStartPos = hoverPos;

			RegisterForRedraw ();
			selection = default;
		}
		public override void onMouseUp (object sender, MouseButtonEventArgs e)
		{
			base.onMouseUp (sender, e);
			/*if (buffer.SelectionIsEmpty)
				buffer.ResetSelection ();*/
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
					Console.WriteLine ("");
					break;
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
			//default:
				//break;
			}
			RegisterForGraphicUpdate ();
		}
		void movePreviousToken () {
			if (syntaxTree.TryGetRoot (out SyntaxNode node)) {
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
			if (syntaxTree.TryGetRoot (out SyntaxNode node)) {
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
			base.onKeyPress (sender, e);
			if (selection.IsEmpty)
				selection = TextSpan.FromBounds (CurrentPos, CurrentPos);
			string str = e.KeyChar.ToString ();
			replaceSelection (str);
		}
		Stack<TextChange> undoStack = new Stack<TextChange> ();
		Stack<TextChange> redoStack = new Stack<TextChange> ();

		void undo () {
			if (undoStack.TryPop (out TextChange tch)) {
				redoStack.Push (tch.Inverse (buffer));
				apply (tch);
			}
		}
		void redo () {
			if (redoStack.TryPop (out TextChange tch)) {
				undoStack.Push (tch.Inverse (buffer));
				apply (tch);
			}
		}

		void replaceSelection (string newText)
		{
			TextChange tch = new TextChange (selection, newText);
			undoStack.Push (tch.Inverse (buffer));
			apply (tch);
		}

		void apply (TextChange tch) {
			buffer = buffer.WithChanges (tch);
			SyntaxTree newTree = syntaxTree.WithChangedText (buffer);
			Compilation comp = Compilation;
			if (comp != null) 
				Compilation = comp.ReplaceSyntaxTree (syntaxTree, newTree);
			SyntaxTree = newTree;		

			if (string.IsNullOrEmpty (tch.NewText))
				CurrentPos = tch.Span.Start;
			else
				CurrentPos = tch.Span.Start + tch.NewText.Length;

			selection = default;
			Task.Run (() => updateFolds ());

			RegisterForRedraw ();
		}
		#endregion
	}
}