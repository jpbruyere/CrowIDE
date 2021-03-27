// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Linq;
using System.CodeDom.Compiler;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Execution;
using System.Reflection;
using Microsoft.Build.Evaluation;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Project = Microsoft.Build.Evaluation.Project;
using Microsoft.Build.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Crow.Coding
{
	public class ProjectView : TreeNode
	{
		bool isLoaded = false;
		ProjectInSolution solutionProject;
		Project project;

		Crow.Command cmdSave, cmdOpen, cmdSetAsStartProj, cmdNewFile;

		#region CTOR
		public ProjectView (SolutionView sol, ProjectInSolution sp) {
			solutionProject = sp;
			solution = sol;

			ProjectRootElement projectRootElt = ProjectRootElement.Open (solutionProject.AbsolutePath);
			project = new Project (solutionProject.AbsolutePath, null, null, sol.IDE.projectCollection);

			ProjectProperty msbuildProjExtPath = project.GetProperty ("MSBuildProjectExtensionsPath");
			ProjectProperty msbuildProjFile = project.GetProperty ("MSBuildProjectFile");

			string[] props = { "EnableDefaultItems", "EnableDefaultCompileItems", "EnableDefaultNoneItems", "EnableDefaultEmbeddedResourceItems" };

			foreach (string pr in props) {
				ProjectProperty pp = project.AllEvaluatedProperties.Where (ep => ep.Name == pr).FirstOrDefault ();
				if (pp == null)
					project.SetProperty (pr, "true");
			}

			project.ReevaluateIfNecessary ();
			
			initCommands ();

			parseOptions = CSharpParseOptions.Default;

			ProjectProperty langVersion = project.GetProperty ("LangVersion");
			if (langVersion != null && Enum.TryParse<LanguageVersion> (langVersion.EvaluatedValue, out LanguageVersion lv))
				parseOptions = parseOptions.WithLanguageVersion (lv);
			else
				parseOptions = parseOptions.WithLanguageVersion (LanguageVersion.Default);

			ProjectProperty constants = project.GetProperty ("DefineConstants");
			if (constants != null)
				parseOptions = parseOptions.WithPreprocessorSymbols (constants.EvaluatedValue.Split (';'));

			populateTreeNodes ();

			Task.Run (() => getcompilation ());
		}
        #endregion

		

        void initCommands () {
			cmdSave = new Crow.Command (new Action (() => Save ())) { Caption = "Save", Icon = new SvgPicture ("#Icons.save.svg"), CanExecute = true };
			cmdOpen = new Crow.Command (new Action (() => populateTreeNodes ())) { Caption = "Open", Icon = new SvgPicture ("#Icons.open.svg"), CanExecute = false };
			cmdSetAsStartProj = new Crow.Command (new Action (() => setAsStartupProject ())) {
				Caption = "Set as Startup Project"
			};
			cmdNewFile = new Crow.Command (new Action (() => AddNewFile ())) {
				Caption = "Add New File",
				Icon = new SvgPicture ("#Icons.blank-file.svg"),
				CanExecute = true
			};

			Commands = new CommandGroup (cmdOpen, cmdSave, cmdSetAsStartProj, cmdNewFile);

			string[] defaultTargets = {"Restore", "Build", "Rebuild", "Pack", "Clean"  };	
			foreach (string target in defaultTargets)
			{
				Commands.Add (new Crow.Command (new Action (() => Compile (target))) {
						Caption = target});

			}
			Commands.Add (new Command(new Action(() => solution.IDE.loadWindow ("#ui.winProjectProperties.crow",solution.IDE)))
				{ Caption = "Properties", CanExecute = true});
		}				

		public SolutionView solution;
		public CompilerResults CompilationResults;
		//public List<ProjectView> dependantProjects = new List<ProjectView> ();
		//public ProjectView ParentProject = null;
		
		public override string DisplayName => solutionProject.ProjectName;

		public bool IsLoaded {
			get { return isLoaded; }
			set {
				if (isLoaded == value)
					return;
				isLoaded = value;
				NotifyValueChanged ("IsLoaded", isLoaded);
			}
		}
		public bool IsStartupProject {
			get { return solution.StartupProject == this; }
		}
		public string FullPath => project.FullPath;
		public string RootDir => project.DirectoryPath;
	
		#region Project properties
		public string ToolsVersion {
			get { return project.ToolsVersion; }
		}
		public string DefaultTargets {
			get { return project.Xml.DefaultTargets; }
		}
		public string ProjectGuid {
			get { return solutionProject.ProjectGuid; }
		}
		public ICollection<ProjectProperty> Properties => project.Properties;
		public string AssemblyName => project.GetPropertyValue ("AssemblyName");
		public string OutputPath => project.AllEvaluatedProperties.Where (p => p.Name == "OutputPath").FirstOrDefault ().EvaluatedValue;
		public string IntermediateOutputPath => project.AllEvaluatedProperties.Where (p => p.Name == "IntermediateOutputPath").FirstOrDefault ().EvaluatedValue;
		public string OutputType => project.AllEvaluatedProperties.Where (p => p.Name == "OutputType").FirstOrDefault ().EvaluatedValue;
		public string OutputAssembly => Path.Combine (project.GetPropertyValue ("OutputPath"), project.GetPropertyValue ("TargetFrameworks"), AssemblyName + AssemblyExtension);
		public string AssemblyExtension => RuntimeInformation.IsOSPlatform (OSPlatform.Windows) ? ".exe" : "";
		public OutputKind OutputKind {
			get {
                switch (OutputType) {
				case "Library":
					return OutputKind.DynamicallyLinkedLibrary;
				case "Exe":
					return OutputKind.ConsoleApplication;
				case "WinExe":
					return OutputKind.WindowsApplication;
				default:
					return OutputKind.ConsoleApplication;
                }
            }
        }
		public string RootNamespace => project.AllEvaluatedProperties.Where (p => p.Name == "RootNamespace").FirstOrDefault ().EvaluatedValue;
		public bool AllowUnsafeBlocks =>
			bool.Parse (project.AllEvaluatedProperties.Where (p => p.Name == "AllowUnsafeBlocks").FirstOrDefault ().EvaluatedValue);
		public bool NoStdLib =>
			bool.Parse (project.AllEvaluatedProperties.Where (p => p.Name == "NoStdLib").FirstOrDefault ().EvaluatedValue);
		public bool TreatWarningsAsErrors =>
			bool.Parse (project.AllEvaluatedProperties.Where (p => p.Name == "TreatWarningsAsErrors").FirstOrDefault ().EvaluatedValue);
		public bool SignAssembly =>
			bool.Parse (project.AllEvaluatedProperties.Where (p => p.Name == "SignAssembly").FirstOrDefault ().EvaluatedValue);
		public string TargetFrameworkVersion => project.AllEvaluatedProperties.Where (p => p.Name == "TargetFrameworkVersion").FirstOrDefault ().EvaluatedValue;
		public string Description => project.AllEvaluatedProperties.Where (p => p.Name == "Description").FirstOrDefault ().EvaluatedValue;
		public string StartupObject => project.AllEvaluatedProperties.Where (p => p.Name == "StartupObject").FirstOrDefault ().EvaluatedValue;
		public bool DebugSymbols =>
			bool.Parse (project.AllEvaluatedProperties.Where (p => p.Name == "DebugSymbols").FirstOrDefault ().EvaluatedValue);
		public int WarningLevel =>
			int.Parse (project.AllEvaluatedProperties.Where (p => p.Name == "WarningLevel").FirstOrDefault ().EvaluatedValue);
		public string Name => project.GetProperty ("MSBuildProjectName").EvaluatedValue;
		#endregion


		public void AddNewFile ()
		{
			Window.Show (solution.IDE, "#ui.NewFile.crow", true).DataSource = this;
		}


		void printProperty (ProjectProperty pp, int depth = 0)
		{
			Console.WriteLine ($"{new string ('\t', depth)}{ pp.EvaluatedValue} ({pp.Project.FullPath})");
			if (pp.Predecessor != null)
				printProperty (pp.Predecessor, ++depth);
		}
		void printEvaluatedItems(Project p)
		{
			foreach (ProjectItem pn in p.AllEvaluatedItems) {
				if (pn.ItemType == "Compile")
					Console.ForegroundColor = ConsoleColor.Yellow;
				else
					Console.ForegroundColor = ConsoleColor.DarkGray;
				Console.WriteLine ($"{pn.ItemType}:{pn.EvaluatedInclude}");

			}
		}
		void printEvamiatedProperties () {
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine ($"Evaluated Globals properties for {DisplayName}");
			foreach (ProjectProperty item in project.AllEvaluatedProperties.OrderBy (p => p.Name)) {
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write ($"\t{item.Name,-40} = ");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.WriteLine ($"{item.EvaluatedValue}");
			}
		}
		void printEvamiatedProperties (ProjectInstance pi) {
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine ($"Evaluated Globals properties for {DisplayName}");
			foreach (ProjectPropertyInstance item in pi.Properties.OrderBy (p => p.Name)) {
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write ($"\t{item.Name,-40} = ");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.WriteLine ($"{item.EvaluatedValue}");
			}
		}
		void addTargetBuildCommandFromProjectItems () {
			foreach (ProjectTargetInstance pti in project.Targets.Values) {
				/*Console.WriteLine ($"{pti.Name} => In: {pti.Inputs} Out:{pti.Outputs} ret:{pti.Returns}  {pti.Children.Count}");
                foreach (ProjectTargetInstanceChild ptic in pti.Children) {
					if (ptic is ProjectTaskInstance p) {
						Console.WriteLine ($"\tTask Instance: {p.Name} Out count:{p.Outputs.Count}");
					} else if (ptic is ProjectItemGroupTaskInstance pg) {
						Console.WriteLine ($"\tGroup Task Instance: count: {pg.Items.Count}");
					}
				}*/
				/*
                foreach (ProjectTaskInstance ti in pti.Tasks) {
					Console.WriteLine ($"\t{ti.Name} {ti.Outputs} {ti.Parameters}");
					Console.WriteLine ($"\t\tParameters");
					Console.WriteLine ($"\t\t==========");
					foreach (KeyValuePair<string, string> p in ti.Parameters) {
						Console.WriteLine ($"\t\t{p.Key} = {p.Value}");
					}
					Console.WriteLine ($"\t\tOutputs");
					Console.WriteLine ($"\t\t=======");
					foreach (ProjectTaskInstanceChild p in ti.Outputs) {						
						Console.WriteLine ($"\t\t{p}");
					}
				}*/

				/*if (!defaultTargets.Contains (pti.Name))
					continue;*/
				if (pti.Name.StartsWith ('_'))
					continue;
				/*
				Console.WriteLine ($"Depends: {pti.Children.Count} {pti.Name} ret: {pti.Returns} {pti.BeforeTargets} -> {pti.AfterTargets}");*/
				Commands.Add (new Crow.Command (new Action (() => Compile (pti.Name)))
				{
					Caption = pti.Name,
				});
			}
		}
		void populateTreeNodes ()
		{
			ProjectNode root = new ProjectNode (this, ItemType.VirtualGroup, RootNamespace);
			ProjectNode refs = new ProjectNode (this, ItemType.ReferenceGroup, "References");
			root.AddChild (refs);

			foreach (ProjectItem pn in project.AllEvaluatedItems) {								
				solution.IDE.ProgressNotify (1);

				switch (pn.ItemType) {
				case "ProjectReferenceTargets":
					Commands.Add (new Crow.Command (new Action (() => Compile (pn.EvaluatedInclude))) {
						Caption = pn.EvaluatedInclude,
					});
					break;
				case "Reference":
				case "PackageReference":
				case "ProjectReference":
					refs.AddChild (new ProjectItemNode (this, pn));
					break;
				case "Compile":
				case "None":
				case "EmbeddedResource":
					ProjectNode curNode = root;
					try {
						string file = pn.EvaluatedInclude;
						string treePath = file;
						if (pn.HasMetadata ("Link"))
							treePath = pn.GetMetadataValue ("Link");							
						string [] folds = treePath.Split (new char [] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
						for (int i = 0; i < folds.Length - 1; i++) {
							ProjectNode nextNode = curNode.Childs.OfType<ProjectNode>().FirstOrDefault (n => n.DisplayName == folds [i] && n.Type == ItemType.VirtualGroup);
							if (nextNode == null) {
								nextNode = new ProjectNode (this, ItemType.VirtualGroup, folds [i]);
								curNode.AddChild (nextNode);
							}
							curNode = nextNode;
						}
						ProjectItemNode pi = new ProjectItemNode (this, pn);

						switch (Path.GetExtension (file)) {
						case ".cs":
							pi = new CSProjectItem (pi);
							break;
						case ".crow":
						case ".template":
						case ".goml":
						case ".itemp":
						case ".imtl":
							pi = new ImlProjectItem (pi);
							break;
						case ".style":
							pi = new StyleProjectItem (pi);
							break;
						default:
							pi = new ProjectFileNode (pi);
							break;
						}
						curNode.AddChild (pi);

					} catch (Exception ex) {
						Console.ForegroundColor = ConsoleColor.DarkRed;
						Console.WriteLine (ex);
						Console.ResetColor ();
					}

					break;
				default:
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine ($"Unhandled Item Type: {pn.ItemType} {pn.EvaluatedInclude}");
					Console.ResetColor ();
					break;
				}
			}
			root.SortChilds ();
			foreach (var item in root.Childs) {
				Childs.Add (item);
				item.Parent = this;
			}

			IsLoaded = true;
		}

		public void Save ()
		{
			
		}

		void setAsStartupProject ()
		{
			solution.StartupProject = this;
		}

       
		public void Compile (string target = "Build")
		{
			/*var nativeSharedMethod = typeof (SolutionFile).Assembly.GetType ("Crow.Build.Shared.NativeMethodsShared");
			var isMonoField = nativeSharedMethod.GetField ("_isMono", BindingFlags.Static | BindingFlags.NonPublic);
			isMonoField.SetValue (null, true);*/
			solution.IDE.projectCollection.SetGlobalProperty ("CrowIDEResolveCache", resolvedCacheFile);
			ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild (project);			
			BuildRequestData request = new BuildRequestData (pi, new string[] { target }, null);			
			BuildResult result = BuildManager.DefaultBuildManager.Build (solution.buildParams, request);			

		}

		public bool TryGetProjectFileFromPath (string path, out ProjectFileNode pi, bool caseSensitive = false)
		{
			if (path.StartsWith ("#", StringComparison.Ordinal))
				pi = Flatten.OfType<ProjectFileNode> ().FirstOrDefault
					(f => f.Type == ItemType.EmbeddedResource && f.LogicalName == path.Substring (1));
			else
				pi = Flatten.OfType<ProjectFileNode> ().FirstOrDefault (pp => string.Equals (pp.FullPath, path, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));

			if (pi != null)
				return true;

			foreach (ProjectItemNode pr in Flatten.OfType<ProjectItemNode> ().Where (pn => pn.Type == ItemType.ProjectReference)) {
				ProjectView p =  solution.Projects.FirstOrDefault (pp => string.Equals (pp.FullPath, pr.FullPath, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase));
				if (p == null)
					continue;
				if (p.TryGetProjectFileFromPath (path, out pi))
					return true;
			}
			return false;
		}

		public void GetDefaultTemplates ()
		{
			IEnumerable<ProjectFileNode> tmpFiles =
			    Flatten.OfType<ProjectFileNode> ().Where (pp => pp.Type == ItemType.EmbeddedResource && pp.Extension == ".template");
			foreach (ProjectFileNode pi in tmpFiles) {
				string clsName = Path.GetFileNameWithoutExtension (pi.RelativePath);
				if (solution.DefaultTemplates.ContainsKey (clsName))
				        continue;
				solution.DefaultTemplates[clsName] = pi.FullPath;
			}
			//    
			//}
			//foreach (ProjectFile pi in tmpFiles.Where (pp => pp.Type == ItemType.EmbeddedResource)) {
			//    string resId = pi.ResourceID;
			//    string clsName = resId.Substring (0, resId.Length - 9);
			//    if (solution.DefaultTemplates.ContainsKey (clsName))
			//        continue;
			//    solution.DefaultTemplates[clsName] = pi.Path;
			//}

			//foreach (Project p in ReferencedProjects)
			//p.GetDefaultTemplates ();
		}
		//		void searchTemplatesIn(Assembly assembly){
		//			if (assembly == null)
		//				return;
		//			foreach (string resId in assembly
		//				.GetManifestResourceNames ()
		//				.Where (r => r.EndsWith (".template", StringComparison.OrdinalIgnoreCase))) {
		//				string clsName = resId.Substring (0, resId.Length - 9);
		//				if (DefaultTemplates.ContainsKey (clsName))
		//					continue;
		//				DefaultTemplates[clsName] = "#" + resId;
		//			}
		//		}

		public List<ProjectView> ReferencedProjects {
			get {
				List<ProjectView> tmp = new List<ProjectView> ();
				/*foreach (ProjectReference pr in flattenNodes.OfType<ProjectReference> ()) {
                    Project p = solution.Projects.FirstOrDefault (pp => pp.ProjectGuid == pr.ProjectGUID);
                    if (p != null)
                        tmp.Add (p);
                }*/
				return tmp;
			}
		}

		public void GetStyling ()
		{
			try {
				/*foreach (ProjectFileNode pi in Flatten.OfType<ProjectFileNode> ().Where (pp => pp.Type == ItemType.EmbeddedResource && pp.Extension == ".style")) {
                    using (Stream s = new MemoryStream (System.Text.Encoding.UTF8.GetBytes (pi.Source))) {
                        new StyleReader (solution.Styling, s, pi.LogicalName);
                    }
                }*/
				foreach (ProjectFileNode pi in Flatten.OfType<ProjectFileNode> ().Where (pp => pp.Type == ItemType.EmbeddedResource && pp.Extension == ".style")) {
					using (Stream s = new MemoryStream (System.Text.Encoding.UTF8.GetBytes (pi.GetSourceWithoutOpening()))) {
						using (StyleReader sr = new StyleReader (s))
							sr.Parse (solution.StylingConstants, solution.Styling, pi.LogicalName);
					}
				}
			} catch (Exception ex) {
                Console.WriteLine (ex.ToString ());
            }
            foreach (ProjectItemNode pr in Flatten.OfType<ProjectItemNode> ().Where(pn=>pn.Type == ItemType.ProjectReference)) {
                ProjectView p = solution.Projects.FirstOrDefault (pp => pp.FullPath == pr.FullPath);
                if (p != null)
                    //throw new Exception ("referenced project not found");
                	p.GetStyling ();
            }

			//TODO:get styling from referenced assemblies
		}

		public IEnumerable<SyntaxTree> SyntaxTrees => Flatten.OfType<CSProjectItem> ().Select (pf => pf.SyntaxTree);
		
		CSharpCompilationOptions compileOptions;
		public CSharpParseOptions parseOptions;
		List<MetadataReference> metadataReferences;
		public Compilation Compilation;

		void updateCompileOptions () {			
			compileOptions = new CSharpCompilationOptions (
				this.OutputKind,
				allowUnsafe: this.AllowUnsafeBlocks,
				warningLevel: this.WarningLevel);
		}
		//retrieve resolved metadatareference in 'projectName.resolved' file produced by crowIde custom target		
		bool updateMetadataReferences () {
			metadataReferences = new List<MetadataReference> (10);

			string refsTxt = resolvedCacheFile;
			if (!File.Exists (refsTxt))
				return false;
			using (StreamReader sr = new StreamReader (refsTxt)) {
				while (!sr.EndOfStream) {
					string dll = sr.ReadLine ();
					if (File.Exists(dll))
						metadataReferences.Add (MetadataReference.CreateFromFile (dll));
				}
			}			
			return true;
		}

		string resolvedCacheFile {
			get {
				ProjectProperty objPath = project.GetProperty ("IntermediateOutputPath");
				ProjectProperty msbuildProjName = project.GetProperty ("MSBuildProjectName");

				return Path.Combine (objPath.EvaluatedValue, msbuildProjName.EvaluatedValue + ".resolved");
			}
        }		

		void getcompilation () {
            try {
				if (compileOptions == null)
					updateCompileOptions();
				if (metadataReferences == null){
					if (!updateMetadataReferences ())
						return;
				}
								
				Compilation = CSharpCompilation.Create (this.AssemblyName, SyntaxTrees, metadataReferences, compileOptions);				
			} catch (Exception e) {
				Console.WriteLine (e);
            }
        }

	}
}