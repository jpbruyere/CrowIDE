using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CrowIdeBuildTasks
{
	public class ResolvedReferencesBuildEvent : CustomBuildEventArgs {
		public ITaskItem[] ResolvedReferences;
		public ResolvedReferencesBuildEvent (string projectFullPath, ITaskItem[] references)
			: base (projectFullPath, "ResolvedReferences", "HookTask") {
				ResolvedReferences = references;
			}
	}
    public class HookTask : Task
    {
		public ITaskItem[] ResolvedReferences {
			get;
			set;
		}
		public ITaskItem ProjectFullPath {
			get;
			set;
		}


        public override bool Execute () {
			BuildEngine.LogCustomEvent (new ResolvedReferencesBuildEvent (ProjectFullPath.ToString(), ResolvedReferences));
			Log.LogMessage (MessageImportance.High, $"Resolved References");
			return true;
        }
    }
}
