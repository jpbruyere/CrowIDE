// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System.Collections;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Crow.Coding
{
	public class CSProjectItem : ProjectFileNode
	{
		#region CTOR
		public CSProjectItem (ProjectItemNode pi) : base (pi)
		{
			syntaxTree = CSharpSyntaxTree.ParseText (Source, Project.parseOptions, FullPath);			
		}
		#endregion

		SyntaxTree syntaxTree;
		int executingLine = -1;
		public SyntaxTree SyntaxTree {
			get => syntaxTree;
			set {
				if (syntaxTree == value)
					return;				
				if (Project?.Compilation != null)
					Project.Compilation = Project.Compilation.ReplaceSyntaxTree (syntaxTree, value);
				syntaxTree = value;
				NotifyValueChanged ("SyntaxTree", syntaxTree);
			}
		}

		public int ExecutingLine {
			get { return executingLine; }
			set {
				if (executingLine == value)
					return;
				executingLine = value;
				NotifyValueChanged ("ExecutingLine", executingLine);				
			}
		}
	}
}
