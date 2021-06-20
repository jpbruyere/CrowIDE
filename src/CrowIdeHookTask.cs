using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CrowIdeBuildTasks
{
	public class CrowIdeHookBuildEvent : CustomBuildEventArgs {
		public string HookedItemsName;
		public string ProjectFullPath;
		public ITaskItem[] HookedItems;
		public CrowIdeHookBuildEvent (string hookedItemsName, string projectFullPath, ITaskItem[] hookedItems)
			: base ($"CrowIde Hook: Project:{projectFullPath} Items:{hookedItemsName}", "ResolvedReferences", "HookTask") {
				HookedItemsName = hookedItemsName;
				ProjectFullPath = projectFullPath;
				HookedItems = hookedItems;
			}
	}
    public class HookTask : Task
    {
		public ITaskItem[] HookedItems {
			get;
			set;
		}
		public ITaskItem ProjectFullPath {
			get;
			set;
		}
		public ITaskItem HookedItemsName {
			get;
			set;
		}


        public override bool Execute () {
			BuildEngine.LogCustomEvent (new CrowIdeHookBuildEvent (HookedItemsName.ToString(), ProjectFullPath.ToString(), HookedItems));			
			return true;
        }
    }
}
