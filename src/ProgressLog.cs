// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Microsoft.CodeAnalysis.MSBuild;

namespace Crow.Coding
{
	public class ProgressLog : IProgress<ProjectLoadProgress>
	{
		CrowIDE ide;
		public ProgressLog (CrowIDE ide) {
			this.ide = ide;
        }
		public void Report (ProjectLoadProgress value)
		{
			ide.ProgressNotify (1, String.Format($"{value.ElapsedTime} {value.Operation} {value.TargetFramework}"));			
		}
	}
}
