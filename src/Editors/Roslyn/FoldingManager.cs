// Copyright (c) 2020-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System;

namespace Crow.Coding
{	
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
		public Fold Root {
			get => rootFold; 
			internal set {
				rootFold = value;
			}
		}
		public List<Fold> AllFolds => rootFold == null ? null : rootFold.Children;
		public bool Initialized => rootFold != null;

		public bool TryGetFold (int line, out Fold fold) {
			lock (mutex) {
				fold = null;
				if (rootFold != null) {					
					return rootFold.TryGetFold (line, ref fold);
				}
				return false;
			}
		}
		/// <summary>
		/// Try get fold with the given ending line
		/// </summary>
		public bool TryGetFoldEndingOnLine (int line, out Fold fold) {
			lock (mutex) {
				fold = null;
				if (rootFold != null) {					
					return rootFold.TryGetFoldEndingOnLine (line, ref fold);
				}
				return false;
			}
		}		
		public Fold GetFoldContainingLine (int line) {	
			lock (mutex) {
				return rootFold == null ? null : rootFold.GetFoldContainingLine (line);
			}
		}
		public Fold GetFoldContainingLineSpan (int lineStart, int lineEnd, bool inclusive = true) {	
			lock (mutex) {
				return rootFold == null ? null : rootFold.GetFoldContainingLineSpan (lineStart, lineEnd, inclusive);
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
					return rootFold == null ? 0 : rootFold.GetHiddenLinesCount ();
				}
			}
		}			

		public int GetHiddenLinesAtScroll (int targetScroll){
			lock (mutex) {				
				return rootFold == null ? 0 : rootFold.GetHiddenLines (targetScroll, 0);
			}
		}
		public int GetHiddenLinesUntilLine (int targetLine){
			lock (mutex) {				
				return rootFold == null ? 0 : rootFold.GetHiddenLinesUntilLine (targetLine, 0);
			}
		}

		public void updatefolds (SourceText oldText, TextChange change, SyntaxNode newSyntaxNode) {
			Console.WriteLine ("update fold");
			lock (mutex) {
				LinePositionSpan lps = oldText.Lines.GetLinePositionSpan (change.Span);
				Fold fold = GetFoldContainingLineSpan (lps.Start.Line, lps.End.Line, false);
				fold.IsFolded = false;

				int lineShrink = lps.End.Line - lps.Start.Line;
				int lineDiff = -lineShrink;

				if (lineShrink > 0) {
					IEnumerable<Fold> intersectingFolds = fold.GetChildFoldsIntersectingSpan (lps.Start.Line, lps.End.Line);
					foreach (Fold f in intersectingFolds)
						fold.Children.Remove (f);
				}

				if (!string.IsNullOrEmpty (change.NewText)) {
					string[] newLinesStr = Regex.Split (change.NewText, @"\r\n|\r|\n|\\\n");
					lineDiff += newLinesStr.Length - 1;
				}

				if (lineDiff != 0) {
					Fold firstFoldAfterSpan = fold.Children.FirstOrDefault (f => f.LineStart > lps.Start.Line);
					if (firstFoldAfterSpan != null) {
						for (int i = fold.Children.IndexOf(firstFoldAfterSpan); i < fold.Children.Count; i++)
							fold.Children[i].ShiftPosition (lineDiff);
					}
					fold.Length += lineDiff;
					Fold ancestor = fold.Parent;
					while (ancestor != null) {						
						ancestor.Length += lineDiff;
						firstFoldAfterSpan = ancestor.Children.FirstOrDefault (f => f.LineStart > lps.Start.Line);
						if (firstFoldAfterSpan != null) {
							for (int i = ancestor.Children.IndexOf(firstFoldAfterSpan); i < ancestor.Children.Count; i++)
								ancestor.Children[i].ShiftPosition (lineDiff);
						}
						ancestor = ancestor.Parent;
					}


					/*Fold startFold = GetFoldContainingLine (lps.Start.Line);
					Fold endFold = GetFoldContainingLine (lps.End.Line);*/
				}

			/*if (lineShrink > 0) {
				
				curFold = fold;
				SyntaxNode node = newSyntaxNode.FindNode (change.Span, false, true);
			}*/

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
        }



		public void CreateFolds (SyntaxNode node) {	
			Console.WriteLine ("create fold");		
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
		Fold addContainerFold (SyntaxNode node) {
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			Fold fold = new Fold (
				lps.Start.Line,
				lps.End.Line, node.Kind(), node.ToFullString());
			if (lps.Start.Line < curFold.LineStart) {
				curFold.Parent.Children.Remove (curFold);
				curFold.Parent.AddChild (fold);
				fold.AddChild (curFold);				
			} else {
				curFold.AddChild (fold);				
				curFold = fold;
			}			
			return fold;
		}
		void addRef(RegionDirectiveTriviaSyntax node) {
			Fold reg = addContainerFold (node);
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
		public override void VisitTrivia (SyntaxTrivia trivia) {
			if (trivia.IsKind (SyntaxKind.MultiLineCommentTrivia)) {
				LinePositionSpan lps = trivia.GetLocation ().GetLineSpan ().Span;
				int endL = lps.End.Character == 0 ? lps.End.Line - 1 : lps.End.Line;
				if (lps.Start.Line < endL) {
					Fold doc = new Fold (lps.Start.Line, endL, trivia.Kind ());
					if (endL < curFold.LineStart) {
						curFold.Parent.Children.Remove (curFold);
						curFold.Parent.AddChild (doc);
						curFold.Parent.AddChild (curFold);
					} else
						curFold.AddChild (doc);
					if (AutoFoldComments)
						doc.IsFolded = true;
				}				
			}
			base.VisitTrivia (trivia);
		}
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
		public override void VisitSkippedTokensTrivia(SkippedTokensTriviaSyntax node) {
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			int endL = lps.End.Line;
			if (lps.Start.Line < endL) {
				Fold doc = new Fold (lps.Start.Line, endL, node.Kind ());
				if (endL < curFold.LineStart) {
					curFold.Parent.Children.Remove (curFold);
					curFold.Parent.AddChild (doc);
					curFold.Parent.AddChild (curFold);
				} else
					curFold.AddChild (doc);				
				/*if (AutoFoldComments)
					doc.IsFolded = true;*/
			}			
			base.VisitSkippedTokensTrivia (node);
		}
		public override void VisitIfDirectiveTrivia(IfDirectiveTriviaSyntax node) {
			base.VisitIfDirectiveTrivia (node);
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
