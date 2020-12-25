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
	public class Fold
	{
		public bool IsFolded;
		public readonly int LineStart;
		public readonly int LineEnd;
		public Fold (int start, int end) {
			LineStart = start;
			LineEnd = end;
			IsFolded = false;
		}
	}
	public class FoldingManager : CSharpSyntaxWalker
	{
		RoslynEditor editor;
		internal Dictionary<int, Fold> refs;
		Stack<int> regions;

		public FoldingManager (RoslynEditor editor) : base (SyntaxWalkerDepth.Node)
		{
			this.editor = editor;

		}

		public void UpdateFolds (SyntaxNode node) {
			refs = new Dictionary<int, Fold> ();
			regions = new Stack<int> ();
			Visit (node);
			regions = null;
        }
        public override void VisitRegionDirectiveTrivia (RegionDirectiveTriviaSyntax node) {
			regions.Push (node.GetLocation ().GetLineSpan ().StartLinePosition.Line);				
			base.VisitRegionDirectiveTrivia (node);
        }
        public override void VisitEndRegionDirectiveTrivia (EndRegionDirectiveTriviaSyntax node) {
			if (regions.TryPop (out int start)) {
				refs[start] = (new Fold (start, node.GetLocation ().GetLineSpan ().StartLinePosition.Line));
			}
            base.VisitEndRegionDirectiveTrivia (node);
        }
		void addRef(CSharpSyntaxNode node) {
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			if (lps.Start.Line < lps.End.Line)
				refs[lps.Start.Line] = (new Fold (lps.Start.Line, lps.End.Line));
		}
		public override void VisitClassDeclaration (ClassDeclarationSyntax node) {
			addRef (node);
			base.VisitClassDeclaration (node);
        }
        public override void VisitMethodDeclaration (MethodDeclarationSyntax node) {
			addRef (node);
			base.VisitMethodDeclaration (node);
        }
        public override void VisitPropertyDeclaration (PropertyDeclarationSyntax node) {
			addRef (node);
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
