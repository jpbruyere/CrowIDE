// Copyright (c) 2020-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

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

		#region CTOR
		public Fold (int lineStart, int lineEnd, SyntaxKind kind, string identifier = "") {			
			Kind = kind;
			Identifier = identifier;
			LineStart = lineStart;
			Length = lineEnd - lineStart + 1;
			IsFolded = false;
		}
		#endregion

		public void AddChild (Fold fold) {
			fold.Parent = this;
			Children.Add (fold);
		}
		public bool SimilarNode (Fold other)
			=> other == null ? false : Kind == other.Kind && Identifier == other.Identifier;
		
		public void ShiftPosition (int delta) {
			LineStart += delta;
			foreach (Fold f in Children)
				f.ShiftPosition (delta);
		}

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
		public bool TryGetFoldEndingOnLine (int line, ref Fold fold) {
			if (LineEnd == line) {
				fold = this;
				return true;
			} else if (LineStart > line || LineEnd < line)
				return false;
			foreach (Fold child in Children) {
				if (child.TryGetFoldEndingOnLine(line, ref fold))
					return true;				
			}
			return false;
		}
		public bool ContainsLine (int line) => LineStart <= line && line <= LineEnd;
		public bool ContainsLineSpan (int lineStart, int lineEnd, bool inclusive = true) 
			=> inclusive ? (LineStart <= lineStart && lineEnd <= LineEnd) : (LineStart < lineStart && lineEnd < LineEnd);
		public Fold GetFoldContainingLine (int line) {			
			foreach (Fold f in Children) {
				if (f.ContainsLine (line))
					return f.GetFoldContainingLine (line);
			}
			return this;
		}
		public Fold GetFoldContainingLineSpan (int lineStart, int lineEnd, bool inclusive = true) {
			foreach (Fold f in Children) {
				if (f.ContainsLineSpan (lineStart, lineEnd, inclusive))
					return f.GetFoldContainingLineSpan (lineStart, lineEnd, inclusive);

			}
			return this;
		}
		public IEnumerable<Fold> GetChildFoldsIntersectingSpan (int lineStart, int lineEnd) {
			foreach (Fold f in Children)
			{
				if (lineEnd < f.LineStart)
					continue;
				if (lineStart > f.LineEnd)
					break;
				yield return f;
			}
		}
		public void ToggleAllFolds (bool state) { 
			IsFolded = state;
			foreach (Fold f in Children)
				f.ToggleAllFolds (state);
		}
		public int GetHiddenLinesCount () {
			if (IsFolded)
				return Length - 1;
			int count = 0;
			foreach (Fold child in Children)
				count += child.GetHiddenLinesCount ();
			return count;			
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
		public int GetHiddenLinesUntilLine (int targetCount, int hiddenLines) {
			if (IsFolded && LineEnd < targetCount)
				return hiddenLines + Length - 1;
			
			foreach (Fold child in Children) {
				if (child.LineStart > targetCount)
					return hiddenLines;
				hiddenLines = child.GetHiddenLinesUntilLine (targetCount, hiddenLines);
			}
		
			return hiddenLines;
		}
		public bool Equals (Fold other) {
			if (other == null)
				return false;
			if (Kind != other.Kind || Identifier != other.Identifier || LineStart != other.LineStart ||
				LineEnd != other.LineEnd || Children.Count != other.Children.Count)
				return false;
			for (int i = 0; i < Children.Count; i++) {
				if (!Children[i].Equals (other.Children[i]))
					return false;				
			}
			return true;
		}
		public int CompareTo (Fold other) => LineStart - other.LineStart;

		public override string ToString () => $"{Kind} {Identifier}:{LineStart} -> {LineEnd} (folded:{IsFolded})";
		string GetDebuggerDisplay() => ToString();
    }
}
