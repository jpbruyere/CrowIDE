// Copyright (c) 2013-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using BreakPoint = Crow.Coding.Debugging.BreakPoint;

namespace Crow.Coding
{
	public class SolutionView: IValueChange, ITaskHost
	{
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged(string MemberName, object _value)
		{
			ValueChanged.Raise(this, new ValueChangeEventArgs(MemberName, _value));
		}
		#endregion

		string path;
		SolutionFile solutionFile;		
		TreeNode selectedItem = null;
		object selectedItemElement = null;
		Microsoft.CodeAnalysis.Diagnostic currentDiagnostic;
		ObservableList<ProjectItemNode> openedItems = new ObservableList<ProjectItemNode>();
		ObservableList<GraphicObjectDesignContainer> toolboxItems;


		#region Project file navigation back/forth
		Stack<ProjectFileLocation> navigationStack_Backward = new Stack<ProjectFileLocation>();
		Stack<ProjectFileLocation> navigationStack_Forward = new Stack<ProjectFileLocation>();

		bool disableFileLocationRecording;
		public void RecordCurrentLocation () {
			if (disableFileLocationRecording)
				return;
			if (SelectedItem is ProjectFileNode pfn)
				navigationStack_Backward.Push (new ProjectFileLocation (pfn, pfn.CurrentPosition));
			navigationStack_Forward.Clear ();
		}
		public void NavigateBackward () {
			if (navigationStack_Backward.TryPop (out ProjectFileLocation pfl)) {
				if (SelectedItem is ProjectFileNode pfn)
					navigationStack_Forward.Push (new ProjectFileLocation (pfn, pfn.CurrentPosition));
				disableFileLocationRecording = true;
				tryGoTo (pfl);
				disableFileLocationRecording = false;
			}
		}
		public void NavigateForward () {
			if (navigationStack_Forward.TryPop (out ProjectFileLocation pfl)) {
				if (SelectedItem is ProjectFileNode pfn)
					navigationStack_Backward.Push (new ProjectFileLocation (pfn, pfn.CurrentPosition));
				disableFileLocationRecording = true;
				tryGoTo (pfl);
				disableFileLocationRecording = false;
			}
		}

		void tryGoTo(Microsoft.CodeAnalysis.Location location)
		{
			Microsoft.CodeAnalysis.FileLinePositionSpan flp = location.GetLineSpan ();
			if (TryGetProjectFileFromPath(flp.Path, out ProjectFileNode pf))
			{
				if (!pf.IsOpened)
					pf.Open();
				pf.IsSelected = true;				
				pf.CurrentLine = flp.StartLinePosition.Line;
			}
			else
			{
				Console.WriteLine ($"[Diagnostic]File Not Found:{flp.Path}");
			}
		}		
		void tryGoTo(ProjectFileLocation location)
		{
			if (!location.ProjectFile.IsOpened)
				location.ProjectFile.Open();
			location.ProjectFile.IsSelected = true;				
			location.ProjectFile.CurrentPosition = location.AbsolutePosition;
		}

		#endregion


		public readonly CrowIDE IDE;
		public ProjectCollection projectCollection { get; private set; }
		public BuildParameters buildParams { get; private set; }
		public Configuration UserConfig { get; private set; }

		

		//public ObservableList<ProjectView> Projects = new ObservableList<ProjectView> ();
		public ObservableList<TreeNode> Children = new ObservableList<TreeNode> ();

		public List<ProjectView> Projects => allSolutionNodes.OfType<ProjectView>().ToList();
		IEnumerable<SolutionNode> allSolutionNodes {
			get {
				foreach (SolutionNode node in Children.OfType<SolutionNode>().SelectMany (child => child.Flatten).OfType<SolutionNode>())
					yield return node;
			}
		}

		public ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics
			=> Projects.Where (p => p.HasDiagnostics).SelectMany (p => p.Diagnostics).ToImmutableArray();
		public Microsoft.CodeAnalysis.Diagnostic CurrentDiagnostic {
			 get => currentDiagnostic;
			 set {
				 if (currentDiagnostic == value)
				 	return;
				 currentDiagnostic = value;
				 NotifyValueChanged ("CurrentDiagnostic", currentDiagnostic);
				 if (currentDiagnostic != null && currentDiagnostic.Location.IsInSource)
				 	tryGoTo (currentDiagnostic.Location);
			 }
		}

		internal void RaiseDiagnosticsValueChanged () => NotifyValueChanged ("Diagnostics", Diagnostics);


		public Command CMDDebugStart, CMDDebugPause, CMDDebugStop, CMDDebugStepIn, CMDDebugStepOver, CMDDebugStepOut;
		void initCommands () {
			CMDDebugStart = new Command ("Start", debug_start, "#Icons.debug-play.svg");
			CMDDebugPause = new Command ("Pause", () => DbgSession.Pause (), "#Icons.debug-pause.svg", false);
			CMDDebugStop = new Command ("Stop", () => debug_stop (), "#Icons.debug-stop.svg", false);
			CMDDebugStepIn = new Command ("Step in", () => DbgSession.StepIn (), "#Icons.debug-step-into.svg", false);
			CMDDebugStepOut = new Command ("Step out", () => DbgSession.StepOut (), "#Icons.debug-step-out.svg", false);
			CMDDebugStepOver = new Command ("Step over", () => DbgSession.StepOver (), "#Icons.debug-step-over.svg", false);
		}




		//public string Directory => Path.GetDirectoryName (path);

		public Dictionary<string, string> StylingConstants;
		public Dictionary<string, Style> Styling;
		public Dictionary<string, string> DefaultTemplates;

		public List<Style> Styles => Styling.Values.ToList();
		public List<StyleContainer> StylingContainers;
		//TODO: check project dependencies if no startup proj

		public IEnumerable<string> Configurations => solutionFile.SolutionConfigurations.Select (sc => sc.ConfigurationName).Distinct ().ToList ();
		public IEnumerable<string> Platforms => solutionFile.SolutionConfigurations.Select (sc => sc.PlatformName).Distinct ().ToList ();


		#region CTOR
		/// <summary>
		/// Creates new solution.
		/// </summary>
		public SolutionView (CrowIDE ide, string path)
		{
			initCommands ();

			this.IDE = ide;

			projectCollection = new ProjectCollection (
				ide.GlobalProperties,
				new ILogger [] { ide.MainIdeLogger },
				ToolsetDefinitionLocations.Default
			);

			this.path = path;

			solutionFile = SolutionFile.Parse (path);
			UserConfig = new Configuration (path + ".user");

			IDE.ProgressNotify (10);

			ActiveConfiguration = solutionFile.GetDefaultConfigurationName ();
			ActivePlatform = solutionFile.GetDefaultPlatformName ();

			projectCollection.SetGlobalProperty ("SolutionDir", Path.GetDirectoryName (path) + Path.DirectorySeparatorChar);			
			projectCollection.SetGlobalProperty ("DefaultItemExcludes", "obj/**/*;bin/**/*");

			IDE.ProgressNotify (10);

			//ide.projectCollection.HostServices
			buildParams = new BuildParameters (projectCollection) {
				Loggers = projectCollection.Loggers,
				LogInitialPropertiesAndItems = true,
				LogTaskInputs = true,				
				/*UseSynchronousLogging = true*/
			};

			//projectCollection.IsBuildEnabled = false;

			BuildManager.DefaultBuildManager.ResetCaches ();

			IDE.ProgressNotify (10);
			//ide.projectCollection.SetGlobalProperty ("RoslynTargetsPath", Path.Combine (Startup.msbuildRoot, @"Roslyn\"));
			//ide.projectCollection.SetGlobalProperty ("MSBuildSDKsPath", Path.Combine (Startup.msbuildRoot, @"Sdks\"));
			//ide.projectCollection.SetGlobalProperty ("MSBuildExtensionsPath", @"C:\Program Files\dotnet\sdk\5.0.100");
			//ide.projectCollection.SetGlobalProperty ("MSBuildBinPath", @"C:\Program Files\dotnet\sdk\5.0.100");
			//ide.projectCollection. ("MSBuildToolsPath", @"C:\Program Files\dotnet\sdk\5.0.100");
			//ide.projectCollection.to
			//------------

			IList<TreeNode> targetChildren = null;
			foreach (ProjectInSolution pis in solutionFile.ProjectsInOrder) {
				if (!string.IsNullOrEmpty (pis.ParentProjectGuid))
					targetChildren = allSolutionNodes.FirstOrDefault (sn => sn.ProjectGuid == pis.ParentProjectGuid).Childs;
				else
					targetChildren = this.Children;

				switch (pis.ProjectType) {
				case SolutionProjectType.KnownToBeMSBuildFormat:					
					targetChildren.Add (new ProjectView (this, pis));
					break;
				case SolutionProjectType.SolutionFolder:					
					targetChildren.Add (new SolutionFolder (this, pis));
					break;
				/*case SolutionProjectType.Unknown:
					break;
				case SolutionProjectType.WebProject:
					break;
				case SolutionProjectType.WebDeploymentProject:
					break;
				case SolutionProjectType.EtpSubProject:
					break;
				case SolutionProjectType.SharedProject:
					break;					*/
				default:
					targetChildren.Add (new SolutionNode (this, pis));
					break;
				}
				IDE.ProgressNotify (10);
			}

			ReloadStyling ();

			ReloadDefaultTemplates ();
		}
		#endregion

		#region Debugging session
		NetcoredbgDebugger dbgSession;
		public ObservableList<BreakPoint> BreakPoints = new ObservableList<BreakPoint> ();
		public NetcoredbgDebugger DbgSession {
			get => dbgSession;
			set {
				if (dbgSession == value)
					return;
				dbgSession = value;
				NotifyValueChanged ("DbgSession", dbgSession);
			}
		}
		void debug_start () {
			if (DbgSession == null) {
				DbgSession = new NetcoredbgDebugger (StartupProject);				
				DbgSession.Start ();
			} else if (DbgSession.CurrentState == NetcoredbgDebugger.Status.Stopped)
				DbgSession.Continue ();
			else if (DbgSession.CurrentState == NetcoredbgDebugger.Status.Exited)
				DbgSession.Start ();
			else
                System.Diagnostics.Debugger.Break ();
		}
		void debug_stop () {
			DbgSession.Stop ();			
		}
		#endregion

		public void Build (params string [] targets)
		{
			BuildRequestData buildRequest = new BuildRequestData (path, IDE.GlobalProperties, CrowIDE.DEFAULT_TOOLS_VERSION, targets, null);
			BuildResult buildResult = BuildManager.DefaultBuildManager.Build (buildParams, buildRequest);
		}

		public void ReloadStyling () {
			IDE.ProgressMessage = "Load Styling";
			Styling = new Dictionary<string, Style> ();
			StylingConstants = new Dictionary<string, string> ();
			if (StartupProject != null)
				StartupProject.GetStyling ();
			StylingContainers = new List<StyleContainer> ();
			foreach (string k in Styling.Keys) 
				StylingContainers.Add (new StyleContainer (k, Styling [k]));
			foreach (ImlProjectItem pf in openedItems.OfType<ImlProjectItem>()) {
				pf.SignalEditorOfType<ImlVisualEditor> ();
			}
		}
		public string[] AvailaibleStyles {
			get { return Styling == null ? new string[] {} : Styling.Keys.ToArray();}
		}
		public void ReloadDefaultTemplates () {
			DefaultTemplates = new Dictionary<string, string>();
			if (StartupProject != null)
				StartupProject.GetDefaultTemplates ();
		}
		public void updateToolboxItems () {
			Type[] crowItems = AppDomain.CurrentDomain.GetAssemblies ()
				.SelectMany (t => t.GetTypes ())
				.Where (t => t.IsClass && !t.IsAbstract && t.IsPublic &&					
					t.Namespace == "Crow" && t.IsSubclassOf(typeof(Widget)) &&
					t.GetCustomAttribute<DesignIgnore>(false) == null).ToArray ();
			ToolboxItems = new ObservableList<GraphicObjectDesignContainer> ();
			foreach (Type ci in crowItems) {
				toolboxItems.Add(new GraphicObjectDesignContainer(ci));
			}
		}
		public bool TryGetProjectFileFromPath (string path, out ProjectFileNode pi){
			pi = null;
			return StartupProject == null ? false :
				StartupProject.TryGetProjectFileFromPath (path, out pi);
		}
		public bool TryGetProjectFromFullPath (string projectFullPath, out ProjectView prj) {
			prj = Projects.FirstOrDefault(p=>p.FullPath == projectFullPath);
			return prj != null;
		}


		public ObservableList<ProjectItemNode> OpenedItems {
			get { return openedItems; }
			set {
				if (openedItems == value)
					return;
				openedItems = value;
				NotifyValueChanged ("OpenedItems", openedItems);
			}
		}

		public ObservableList<GraphicObjectDesignContainer> ToolboxItems {
			get { return toolboxItems; }
			set {
				if (toolboxItems == value)
					return;
				toolboxItems = value;
				NotifyValueChanged ("ToolboxItems", toolboxItems);
			}			
		}
		public TreeNode SelectedItem {
			get { return selectedItem; }
			set {
				if (selectedItem == value)
					return;

				Console.WriteLine ($"SolView.SelectedItem: {selectedItem} -> {value}");

				selectedItem = value;

				if (selectedItem is ProjectItemNode pin)
					UserConfig.Set ("SelectedProjItems", pin.SaveID);

				NotifyValueChanged ("SelectedItem", selectedItem);
			}
		}
		public object SelectedItemElement {
			get { return selectedItemElement; }
			set {
				if (selectedItemElement == value)
					return;
				selectedItemElement = value;
				Console.WriteLine ($"selected item element: {selectedItemElement}");
				NotifyValueChanged ("SelectedItemElement", selectedItemElement);
			}
		}
		public string DisplayName => Path.GetFileNameWithoutExtension (path);

		/*public List<System.CodeDom.Compiler.CompilerError> CompilerErrors {
			get {
				int errCount = 0;
				for (int i = 0; i < Projects.Count; i++) {
					if (Projects [i].CompilationResults != null)
						errCount += Projects [i].CompilationResults.Errors.Count;
				}
				System.CodeDom.Compiler.CompilerError[] tmp = new System.CodeDom.Compiler.CompilerError[errCount];

				int ptr = 0;
				for (int i = 0; i < Projects.Count; i++) {
					if (Projects [i].CompilationResults == null)
						continue;
					Projects [i].CompilationResults.Errors.CopyTo (tmp,ptr);
					ptr += Projects [i].CompilationResults.Errors.Count;
				}
				return new List<System.CodeDom.Compiler.CompilerError>(tmp);
			}
		}

		public void UpdateErrorList () {
			NotifyValueChanged ("CompilerErrors", CompilerErrors);
		}*/

		void saveOpenedItemsInUserConfig (){
			if (openedItems.Count == 0)
				UserConfig.Set ("OpenedItems", "");
			else
				UserConfig.Set ("OpenedItems", openedItems.Select(o => o.SaveID).Aggregate((a,b)=>$"{a};{b}"));
		}
		public void ReopenItemsSavedInUserConfig () {
			string tmp = UserConfig.Get<string> ("OpenedItems");
			if (string.IsNullOrEmpty (tmp))
				return;
			string sel = UserConfig.Get<string> ("SelectedProjItems");
			ProjectFileNode selItem = null;
			foreach (string f in tmp.Split(';')) {
				string [] s = f.Split ('|');
				ProjectFileNode pi = Projects.FirstOrDefault (p => p.DisplayName == s [0])?.Flatten.OfType<ProjectFileNode>().FirstOrDefault(pfn=>pfn.RelativePath == s[1]);
				if (pi == null)
					continue;
				pi.Open ();
				if (pi.SaveID == sel)
					selItem = pi;
			}
			
			//SelectedItem = selItem;
			if (selItem != null)
				selItem.IsSelected = true;
		}

		/*void onSelectedItemChanged (object sender, SelectionChangeEventArgs e){							
			SelectedItem = e.NewValue as ProjectItem;
			UserConfig.Set ("SelectedProjItems", SelectedItem?.AbsolutePath);
		}*/
		public void OpenItem (ProjectItemNode pi) {
			if (!openedItems.Contains (pi)) {
				lock(IDE.UpdateMutex)
					openedItems.Add (pi);
				saveOpenedItemsInUserConfig ();
			}
		}
		public void CloseItem (ProjectItemNode pi) {
			//lock (IDE.UpdateMutex)			
			lock(IDE.UpdateMutex)
				openedItems.Remove (pi);
			pi.IsSelected = false;
			saveOpenedItemsInUserConfig ();
		}

		public void CloseSolution () {
			while (openedItems.Count > 0) {
				openedItems.Remove (openedItems [0]);
			}
			while (toolboxItems?.Count > 0) {
				toolboxItems.Remove (toolboxItems [0]);
			}
			NotifyValueChanged ("Projects", null);

			projectCollection.UnloadAllProjects ();
		}

		public ProjectView StartupProject {
			get => Projects.FirstOrDefault (p => p.FullPath == UserConfig.Get<string> ("StartupProject")); 
			set {
				if (value == StartupProject)
					return;

				StartupProject?.NotifyValueChanged ("IsStartupProject", false);

				if (value == null)
					UserConfig.Set ("StartupProject", "");
				else {
					UserConfig.Set ("StartupProject", value.FullPath);
					value.NotifyValueChanged("IsStartupProject", true);
				}
				NotifyValueChanged ("StartupProject", StartupProject);
				ReloadStyling ();
				ReloadDefaultTemplates ();
			}
		}

		public string ActiveConfiguration {
			get => projectCollection.GetGlobalProperty ("Configuration")?.ToString();			
			set {
				if (ActiveConfiguration == value)
					return;				
				projectCollection.SetGlobalProperty ("Configuration", value);
				NotifyValueChanged ("ActiveConfiguration", value);
			}
		}
		public string ActivePlatform {
			get => projectCollection.GetGlobalProperty ("Platform")?.ToString();			
			set {
				if (ActivePlatform == value)
					return;				
				projectCollection.SetGlobalProperty ("Platform", value);
				NotifyValueChanged ("ActivePlatform", value);
			}
		}

		public override string ToString () => path;
	}
}
