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

		public FoldingManager (RoslynEditor editor) : base (SyntaxWalkerDepth.StructuredTrivia)
		{
			this.editor = editor;

		}

		public void UpdateFolds (SyntaxNode node) {
			refs = new Dictionary<int, Fold> ();
			Visit (node);
        }

        public override void Visit (SyntaxNode node) {
			LinePositionSpan lps = node.GetLocation ().GetLineSpan ().Span;
			if (lps.Start.Line < lps.End.Line)
				refs[lps.Start.Line] = (new Fold (lps.Start.Line, lps.End.Line));

            base.Visit (node);
        }
	}
}
