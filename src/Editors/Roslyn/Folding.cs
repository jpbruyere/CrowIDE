// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Crow.Coding
{
    public class Fold : IEquatable<Fold>
	{
		public bool IsFolded;
		public readonly SyntaxKind Kind;
		public string Identifier;
		public readonly int LineStart;
		public readonly int LineEnd;
		public Fold (int start, int end, SyntaxKind kind, string identifier = "") {
			Kind = kind;
			Identifier = identifier;
			LineStart = start;
			LineEnd = end;
			IsFolded = false;
		}
		public bool SimilarNode (Fold other)
			=> other == null ? false : Kind == other.Kind && Identifier == other.Identifier;
		public override string ToString () => $"{LineStart} -> {LineEnd} (folded:{IsFolded})";

        public bool Equals (Fold other)
			=> other == null ? false : Kind == other.Kind && Identifier == other.Identifier && LineStart == other.LineStart && LineEnd == other.LineEnd;        
    }
	public class FoldingManager : CSharpSyntaxWalker
	{
		RoslynEditor editor;
		Dictionary<int, Fold> refs;
		object mutex = new object ();

		Stack<int> regions;

		public bool TryGetFold (int line, out Fold fold) {
			lock (mutex) {
				if (refs.ContainsKey (line)) {
					fold = refs[line];
					return true;
				}
				fold = null;
				return false;
			}
		}
        #region CTOR
        public FoldingManager (RoslynEditor editor) : base (SyntaxWalkerDepth.StructuredTrivia)
		{
			this.editor = editor;

		}
		#endregion

		public void updatefolds (TextChange change, SyntaxNode node) {
			
        }

		public void UpdateFolds (SyntaxNode node) {
			/*Console.WriteLine ("***************************");
			Console.WriteLine ("* Upadte folds            *");
			Console.WriteLine ("***************************");*/
			
			lock (mutex) {
				//Dictionary<int, Fold> oldFolds = refs;
				refs = new Dictionary<int, Fold> ();
				regions = new Stack<int> ();
				Visit (node);
				regions = null;

				/*Dictionary<int,Fold>.ValueCollection.Enumerator olds = oldFolds.Values.GetEnumerator ();
				if (!olds.MoveNext ())
					return;
				foreach (Fold fold in refs.Values) {
					if (olds.Current.SimilarNode(fold)) {
						fold.IsFolded = olds.Current.IsFolded;
						if (!olds.MoveNext ())
							return;
                    }
						


                }*/
			}
        }
		/*int tabs = 0;
        public override void DefaultVisit (SyntaxNode node) {
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			Console.WriteLine ($"{new string (' ', tabs)} {node.Kind()} {lps.Start.Line} -> {lps.End} {node.GetFirstToken()}");
			tabs++;
            base.DefaultVisit (node);
			tabs--;
        }*/
        public override void VisitRegionDirectiveTrivia (RegionDirectiveTriviaSyntax node) {
			regions.Push (node.GetLocation ().GetLineSpan ().StartLinePosition.Line);				
			base.VisitRegionDirectiveTrivia (node);
        }
        public override void VisitEndRegionDirectiveTrivia (EndRegionDirectiveTriviaSyntax node) {
			if (regions.TryPop (out int start))
				refs[start] = (new Fold (start, node.GetLocation ().GetLineSpan ().StartLinePosition.Line, SyntaxKind.RegionDirectiveTrivia));			
            base.VisitEndRegionDirectiveTrivia (node);
        }
		void addRef(CSharpSyntaxNode node) {			
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			if (lps.Start.Line < lps.End.Line)
				refs[lps.Start.Line] = (new Fold (lps.Start.Line, lps.End.Line, node.Kind()));
		}

		public override void VisitDocumentationCommentTrivia (DocumentationCommentTriviaSyntax node) {			
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			if (lps.Start.Line < lps.End.Line)
				refs[lps.Start.Line] = (new Fold (lps.Start.Line, lps.End.Line-1, node.Kind()));
			base.VisitDocumentationCommentTrivia (node);			
        }
        public override void VisitNamespaceDeclaration (NamespaceDeclarationSyntax node) {
			addRef (node);
			base.VisitNamespaceDeclaration (node);
        }
        public override void VisitClassDeclaration (ClassDeclarationSyntax node) {
			addRef (node);
			base.VisitClassDeclaration (node);
        }
        public override void VisitMethodDeclaration (MethodDeclarationSyntax node) {			
			SyntaxToken tok = node.Identifier;
			if (!tok.IsMissing) {
				int startL = tok.GetLocation ().GetLineSpan ().StartLinePosition.Line;
				int endL = node.GetLocation ().GetLineSpan ().EndLinePosition.Line;
				if (startL < endL)
					refs[startL] = (new Fold (startL, endL, node.Kind (), tok.ToString ()));
			}			
			base.VisitMethodDeclaration (node);
        }
        public override void VisitPropertyDeclaration (PropertyDeclarationSyntax node) {
			SyntaxToken tok = node.Identifier;
			if (!tok.IsMissing) {
				int startL = tok.GetLocation ().GetLineSpan ().StartLinePosition.Line;
				int endL = node.GetLocation ().GetLineSpan ().EndLinePosition.Line;
				if (startL < endL)
					refs[startL] = (new Fold (startL, endL, node.Kind (), tok.ToString ()));
			}
			base.VisitPropertyDeclaration (node);
        }
        public override void VisitDelegateDeclaration (DelegateDeclarationSyntax node) {
			addRef (node);
			base.VisitDelegateDeclaration (node);
        }
        public override void VisitConstructorDeclaration (ConstructorDeclarationSyntax node) {
			addRef (node);
			base.VisitConstructorDeclaration (node);
        }
        public override void VisitDestructorDeclaration (DestructorDeclarationSyntax node) {
			addRef (node);
			base.VisitDestructorDeclaration (node);
        }
        public override void VisitIfStatement (IfStatementSyntax node) {
			addRef (node);
			base.VisitIfStatement (node);
        }
        public override void VisitWhileStatement (WhileStatementSyntax node) {
			addRef (node);
			base.VisitWhileStatement (node);
        }
        public override void VisitForStatement (ForStatementSyntax node) {
			addRef (node);
			base.VisitForStatement (node);
        }
        /*public override void VisitBlock (BlockSyntax node) {
			addRef (node);
			base.VisitBlock (node);
        }*/
    }
}
