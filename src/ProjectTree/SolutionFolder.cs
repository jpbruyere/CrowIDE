// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;

namespace Crow.Coding
{
	public class SolutionFolder : SolutionNode
	{
		
		public SolutionFolder (SolutionView sol, ProjectInSolution sp) : base (sol, sp)
		{
		}
		public override Picture Icon => new SvgPicture ("#Icons.folder.svg");
		public override string IconSub => IsExpanded.ToString ();

	}

}
