// Copyright (c) 2020-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Microsoft.Build.Construction;

namespace Crow.Coding
{
	public class SolutionNode : TreeNode {
		protected ProjectInSolution solutionProject;
		public SolutionView Solution { get ; private set; }

		public SolutionNode (SolutionView sol, ProjectInSolution sp) {
			solutionProject = sp;
			Solution = sol;
		}

		public override string DisplayName => solutionProject.ProjectName;
		public string ProjectGuid => solutionProject.ProjectGuid;

	}
}
