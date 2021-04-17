// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Crow.Coding
{
	[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class Fold : IEquatable<Fold>, IComparable<Fold>
	{
		public Fold Parent { get; private set; }
		public List<Fold> Children { get; private set; } = new List<Fold>();
		public bool IsFolded;
		public readonly SyntaxKind Kind;
		public string Identifier;		
		public int LineStart;
		public int LineEnd => LineStart + Length - 1;
		public void SetLineEnd (int lineEnd) {
			Length = lineEnd - LineStart + 1;
		}
		public int Length;
		public Fold (int lineStart, int lineEnd, SyntaxKind kind, string identifier = "") {			
			Kind = kind;
			Identifier = identifier;
			LineStart = lineStart;
			Length = lineEnd - lineStart + 1;
			IsFolded = false;
		}
		public void AddChild (Fold fold) {
			fold.Parent = this;
			Children.Add (fold);
		}
		public bool SimilarNode (Fold other)
			=> other == null ? false : Kind == other.Kind && Identifier == other.Identifier;
		

        public bool Equals (Fold other)
			=> other == null ? false : Kind == other.Kind && Identifier == other.Identifier && LineStart == other.LineStart && LineEnd == other.LineEnd;
		public int CompareTo (Fold other) => LineStart - other.LineStart;

		public override string ToString () => $"{Kind} {Identifier}:{LineStart} -> {LineEnd} (folded:{IsFolded})";
		string GetDebuggerDisplay() => ToString();


		public bool TryGetFold (int line, ref Fold fold) {
			if (LineStart == line) {
				fold = this;
				return true;
			} else if (LineStart > line || LineEnd <= line)
				return false;
			foreach (Fold child in Children) {
				if (child.TryGetFold(line, ref fold))
					return true;				
			}
			return false;
		}
		public bool ContainsLine (int line) => LineStart <= line && line <= LineEnd;
		public Fold GetFoldContainingLine (int line) {			
			foreach (Fold f in Children) {
				if (f.ContainsLine (line))
					return f.GetFoldContainingLine (line);

			}
			return this;
		}
		public void ToggleAllFolds (bool state) { 
			IsFolded = state;
			foreach (Fold f in Children)
				f.ToggleAllFolds (state);
		}
		public int GetFoldedLinesCount () {
			if (IsFolded)
				return Length - 1;
			int count = 0;
			foreach (Fold child in Children)
				count += child.GetFoldedLinesCount ();
			return count;			
		}		
		public int GetTarget (int targetCount, ref int hiddenLines) {
			if (IsFolded)
				hiddenLines += Length - 1;				
			else {
				foreach (Fold child in Children) {
					if (targetCount + hiddenLines <= child.LineStart)
						return targetCount + hiddenLines;
					child.GetTarget (targetCount, ref hiddenLines);
				}
			}
			return targetCount + hiddenLines;
		}	
		public int GetHiddenLines (int targetCount, int hiddenLines) {
			if (IsFolded)
				return hiddenLines + Length - 1;
			
			foreach (Fold child in Children) {
				if (targetCount + hiddenLines <= child.LineStart)
					return hiddenLines;
				hiddenLines = child.GetHiddenLines (targetCount, hiddenLines);
			}
		
			return hiddenLines;
		}
    }
	public class FoldingManager : CSharpSyntaxWalker
	{
		RoslynEditor editor;
        #region CTOR
        public FoldingManager (RoslynEditor editor) : base (SyntaxWalkerDepth.StructuredTrivia)
		{
			this.editor = editor;

		}
		#endregion

		Fold rootFold, curFold;
		object mutex = new object ();		

		bool autoFoldRegions, AutoFoldComments;		

		public List<Fold> AllFolds => rootFold == null ? null : rootFold.Children;

		public bool TryGetFold (int line, out Fold fold) {
			lock (mutex) {
				fold = null;
				if (rootFold != null) {					
					return rootFold.TryGetFold (line, ref fold);
				}
				return false;
			}
		}
		public Fold GetFoldContainingLine (int line) {	
			lock (mutex) {
				return rootFold == null ? null : rootFold.GetFoldContainingLine (line);
			}
		}
		public bool TryToogleFold (int line) {
			if (TryGetFold (line, out Fold fold))
				fold.IsFolded = !fold.IsFolded;
			else
				return false;
			return true;
		}
		public void ToggleAllFolds (bool state = true) {
			lock (mutex) {
				if (rootFold == null)
					return;
				foreach (Fold child in rootFold.Children)
					child.ToggleAllFolds (state);
			}
		}

		public int TotalFoldedLinesCount {
			get {
				lock (mutex) {
					return rootFold == null ? 0 : rootFold.GetFoldedLinesCount ();
				}
			}
		}			
		public int GetLineIndexAtScroll (int targetLine){
			lock (mutex) {
				int hiddenLines = 0;
				return rootFold == null ? 0 : rootFold.GetTarget (targetLine, ref hiddenLines);
			}
		}
		public int GetHiddenLinesAtScroll (int targetLine){
			lock (mutex) {				
				return rootFold == null ? 0 : rootFold.GetHiddenLines (targetLine, 0);
			}
		}


		public void updatefolds (TextChange change) {
			/*if (refs == null)
				return;
			var Enumerator = refs.Values.GetEnumerator();
			int i = 0;


            while (Enumerator.MoveNext()) {
				Fold f = Enumerator.Current;
				if (f.Span.End < change.Span.Start)
					continue;
				//f.sp
            }*/
			
        }



		public void CreateFolds (SyntaxNode node) {			
			CrowIDE ide = editor.IFace as CrowIDE;
			autoFoldRegions = ide.AutoFoldRegions;
			AutoFoldComments = ide.AutoFoldComments;			
			lock (mutex) {

				LinePositionSpan lps = node.GetText().Lines.GetLinePositionSpan(node.FullSpan);				
				rootFold = new Fold(lps.Start.Line , lps.End.Line, node.Kind());
				curFold = rootFold;
				Visit (node);
			}
        }
		
		void addRef(RegionDirectiveTriviaSyntax node) {
			int start = node.GetLocation ().GetLineSpan ().Span.Start.Line;
			Fold reg = new Fold (
				start,
				start, node.Kind(), node.ToFullString());
			if (start < curFold.LineStart) {
				curFold.Parent.Children.Remove (curFold);
				curFold.Parent.AddChild (reg);
				reg.AddChild (curFold);				
			} else {
				curFold.AddChild (reg);				
				curFold = reg;
			}
			if (autoFoldRegions)
				reg.IsFolded = true;
		}
		bool addRef(CSharpSyntaxNode node, bool mayHaveChildNode = true) {
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;			
			if (lps.Start.Line < lps.End.Line) {
				Fold f = new Fold (lps.Start.Line, lps.End.Line, node.Kind());
				curFold.AddChild (f);
				if (mayHaveChildNode) {
					curFold = f;
					return true;
				}
			}
			return false;
		}
		bool addRef (CSharpSyntaxNode node, SyntaxToken identifier, bool mayHaveChildNode = true) {
			if (!identifier.IsMissing) {
				int startL = identifier.GetLocation ().GetLineSpan ().StartLinePosition.Line;
				int endL = node.GetLocation ().GetLineSpan ().EndLinePosition.Line;
				if (startL < endL) {
					Fold f = new Fold (startL, endL, node.Kind (), identifier.ToString ());
					curFold.AddChild (f);
					if (mayHaveChildNode) {
						curFold = f;
						return true;
					}
				}
			}
			return false;
		}
		/*public override void VisitToken (SyntaxToken token)
		{
			if (token.HasLeadingTrivia) {
				foreach (SyntaxTrivia triviaNode in token.LeadingTrivia) {
					if (triviaNode.IsDirective) {						
						if (triviaNode.GetStructure() is RegionDirectiveTriviaSyntax rdts) {
							if (foldStack.Peek().Kind == token.Parent.Kind()) {
								Fold save = foldStack.Pop();
								foldStack.Peek().Children.Remove (save);
								addRef (rdts);
								foldStack.Peek().AddChild (save);
								foldStack.Push (save);								
							} else {
								addRef (rdts);
							}
								
						}else if (triviaNode.GetStructure() is EndRegionDirectiveTriviaSyntax erdts) {
							if (foldStack.Peek().Kind == token.Parent.Kind()) {
								Fold save = foldStack.Pop();
								Fold regionFold = foldStack.Pop();
								regionFold.SetLineEnd (erdts.GetLocation ().GetLineSpan ().Span.End.Line);
								if (regionFold.Length == 0)
									Debugger.Break();								
								//foldStack.Peek().AddChild (save);
								foldStack.Push (save);
							} else {
								Fold regionFold = foldStack.Pop();
								regionFold.SetLineEnd (erdts.GetLocation ().GetLineSpan ().Span.End.Line);
								if (regionFold.Length == 0)
									Debugger.Break();
							}							
						}
					}					
				}
			}
			if (token.HasTrailingTrivia) {
				foreach (SyntaxTrivia triviaNode in token.TrailingTrivia) {
					if (triviaNode.IsDirective) {						
						if (triviaNode.GetStructure() is RegionDirectiveTriviaSyntax rdts)
							addRef (rdts);
						else if (triviaNode.GetStructure() is EndRegionDirectiveTriviaSyntax erdts)
							foldStack.Pop().SetLineEnd (erdts.GetLocation ().GetLineSpan ().Span.End.Line);
					}					
				}
			}			
		}*/
		public override void VisitRegionDirectiveTrivia(RegionDirectiveTriviaSyntax node)
		{
			addRef (node);
			base.VisitRegionDirectiveTrivia(node);
		}
		public override void VisitEndRegionDirectiveTrivia(EndRegionDirectiveTriviaSyntax node)
		{
			int endL = node.GetLocation ().GetLineSpan ().Span.End.Line;
			if (endL < curFold.LineStart) {
				curFold.Parent.Children.Remove (curFold);
				curFold.Parent.SetLineEnd (endL);
				curFold.Parent.Parent.AddChild (curFold);				
			} else {
				curFold.SetLineEnd (endL);
				curFold = curFold.Parent;
			}
			base.VisitEndRegionDirectiveTrivia(node);
		}

		public override void VisitDocumentationCommentTrivia (DocumentationCommentTriviaSyntax node) {			
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			int endL = lps.End.Character == 0 ? lps.End.Line - 1 : lps.End.Line;
			if (lps.Start.Line < endL) {
				Fold doc = new Fold (lps.Start.Line, endL, node.Kind ());
				if (endL < curFold.LineStart) {
					curFold.Parent.Children.Remove (curFold);
					curFold.Parent.AddChild (doc);
					curFold.Parent.AddChild (curFold);
				} else
					curFold.AddChild (doc);				
				if (AutoFoldComments)
					doc.IsFolded = true;
			}			
			base.VisitDocumentationCommentTrivia (node);			
        }
        public override void VisitNamespaceDeclaration (NamespaceDeclarationSyntax node) {
			bool pop = addRef (node);
			base.VisitNamespaceDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitClassDeclaration (ClassDeclarationSyntax node) {
			bool pop = addRef (node, node.Identifier);
			base.VisitClassDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitStructDeclaration (StructDeclarationSyntax node) {
			bool pop = addRef (node, node.Identifier);
			base.VisitStructDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitMethodDeclaration (MethodDeclarationSyntax node) {
			bool pop = addRef (node, node.Identifier);
			base.VisitMethodDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitEnumDeclaration (EnumDeclarationSyntax node) {
			bool pop = addRef (node, node.Identifier);
			base.VisitEnumDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitPropertyDeclaration (PropertyDeclarationSyntax node) {
			bool pop = addRef (node, node.Identifier);
			base.VisitPropertyDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitDelegateDeclaration (DelegateDeclarationSyntax node) {
			bool pop = addRef (node, node.Identifier);
			base.VisitDelegateDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitConstructorDeclaration (ConstructorDeclarationSyntax node) {
			bool pop = addRef (node);
			base.VisitConstructorDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitDestructorDeclaration (DestructorDeclarationSyntax node) {
			bool pop = addRef (node);
			base.VisitDestructorDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitIfStatement (IfStatementSyntax node) {
			bool pop = addRef (node);
			base.VisitIfStatement (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitWhileStatement (WhileStatementSyntax node) {
			bool pop = addRef (node);
			base.VisitWhileStatement (node);
			if (pop)
				curFold = curFold.Parent;
        }
        public override void VisitForStatement (ForStatementSyntax node) {
			bool pop = addRef (node);
			base.VisitForStatement (node);
			if (pop)
				curFold = curFold.Parent;
        }
		public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node) {
			bool pop = addRef (node);
			base.VisitOperatorDeclaration (node);
			if (pop)
				curFold = curFold.Parent;
		} 
    }
}
