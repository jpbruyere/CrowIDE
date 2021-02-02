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
		}
		#endregion		

		public SyntaxTree SyntaxTree {
			get {
				if (IsOpened)
					return RegisteredEditors.Keys.OfType<RoslynEditor> ().FirstOrDefault ()?.SyntaxTree;
				return CSharpSyntaxTree.ParseText (Source,CSharpParseOptions.Default, FullPath);
			}
			//internal set { NotifyValueChanged ("SyntaxTree", SyntaxTree); NotifyValueChanged ("RootNode", RootNode); }
		}
		public SyntaxNode RootNode => SyntaxTree?.GetRoot ();
	}
}
