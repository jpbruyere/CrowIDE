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

namespace Crow.Coding
{
	public class ProjectView : TreeNode
	{
		bool isLoaded = false;
		ProjectInSolution solutionProject;
		Project project;

		Crow.Command cmdSave, cmdOpen, cmdCompile, cmdSetAsStartProj, cmdNewFile;

		void registerCrowIdeCustomTarget () {
			//$(MSBuildProjectExtensionsPath)$(MSBuildProjectFile).*.targets
			ProjectProperty msbuildProjExtPath = project.GetProperty ("MSBuildProjectExtensionsPath");
			ProjectProperty msbuildProjFile = project.GetProperty ("MSBuildProjectFile");

			ProjectProperty msbuildExtPath = project.GetProperty ("MSBuildUserExtensionsPath");
			ProjectProperty msbuildToolsVersion = project.GetProperty ("MSBuildToolsVersion");

			string targetPath = Path.Combine (
				msbuildExtPath.EvaluatedValue,
				msbuildToolsVersion.EvaluatedValue,
				"Microsoft.Common.targets",
				"ImportAfter"				
			);

			if (!Directory.Exists (targetPath))
				Directory.CreateDirectory (targetPath);
			targetPath = Path.Combine (targetPath, "CrowIde.targets");
			//if (!File.Exists(targetPath)) {
				string targetSource = Path.Combine(Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location), "src", "CustomTargets.txt");
				File.Copy (targetSource, targetPath, true);
				project.ReevaluateIfNecessary ();
            //}

			

			
			//"$(MSBuildUserExtensionsPath)\$(MSBuildToolsVersion)\*

		}

		#region CTOR
		public ProjectView (SolutionView sol, ProjectInSolution sp)
		{
			solutionProject = sp;
			solution = sol;			

			ProjectRootElement projectRootElt = ProjectRootElement.Open (solutionProject.AbsolutePath);
			project = new Project (solutionProject.AbsolutePath, null, null, sol.IDE.projectCollection);

			ProjectProperty msbuildProjExtPath = project.GetProperty ("MSBuildProjectExtensionsPath");
			ProjectProperty msbuildProjFile = project.GetProperty ("MSBuildProjectFile");

			//registerCrowIdeCustomTarget ();

			string[] props = { "EnableDefaultItems", "EnableDefaultCompileItems", "EnableDefaultNoneItems", "EnableDefaultEmbeddedResourceItems" };

			foreach (string pr in props) {
				ProjectProperty pp = project.AllEvaluatedProperties.Where (ep => ep.Name == pr).FirstOrDefault();
				if (pp == null)
					project.SetGlobalProperty (pr, "true");
			}

			project.ReevaluateIfNecessary ();						

			cmdSave = new Crow.Command (new Action (() => Save ())) { Caption = "Save", Icon = new SvgPicture ("#Icons.save.svg"), CanExecute = true };
			cmdOpen = new Crow.Command (new Action (() => populateTreeNodes ())) { Caption = "Open", Icon = new SvgPicture ("#Icons.open.svg"), CanExecute = false };
			cmdCompile = new Crow.Command (new Action (() => Compile ("Restore"))) {
				Caption = "Restore",
			};
			cmdSetAsStartProj = new Crow.Command (new Action (() => setAsStartupProject ())) {
				Caption = "Set as Startup Project"
			};
			cmdNewFile = new Crow.Command (new Action (() => AddNewFile ())) {
				Caption = "Add New File",
				Icon = new SvgPicture ("#Icons.blank-file.svg"),
				CanExecute = true
			};

			Commands = new CommandGroup (cmdOpen, cmdSave, cmdSetAsStartProj, cmdCompile, cmdNewFile);

			populateTreeNodes ();
			
			//getcompilation ();
		}
		#endregion

		public SolutionView solution;
		public CompilerResults CompilationResults;
		public List<ProjectView> dependantProjects = new List<ProjectView> ();
		public ProjectView ParentProject = null;

		internal Microsoft.CodeAnalysis.ProjectId projectId;

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
		public string AssemblyName => project.AllEvaluatedProperties.Where (p => p.Name == "AssemblyName").FirstOrDefault ().EvaluatedValue;
		public string OutputType => project.AllEvaluatedProperties.Where (p => p.Name == "OutputType").FirstOrDefault ().EvaluatedValue;
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
		public bool AllowUnsafeBlocks {
			get {
				return false;
				/*return nodeProps["AllowUnsafeBlocks"] == null ? false :
               bool.Parse (nodeProps["AllowUnsafeBlocks"]?.InnerText);*/
			}
		}
		public bool NoStdLib {
			get {
				return false;
				/*return nodeProps["NoStdLib"] == null ? false :
              bool.Parse (nodeProps["NoStdLib"]?.InnerText);*/
			}
		}
		public bool TreatWarningsAsErrors {
			get {
				return false;
				/*return nodeProps["TreatWarningsAsErrors"] == null ? false :
              bool.Parse (nodeProps["TreatWarningsAsErrors"]?.InnerText);*/
			}
		}
		public bool SignAssembly {
			get { return false; }// projectRootElt.Properties.Where (p => p.Name == "SignAssembly").FirstOrDefault ().Value; }
		}
		public string TargetFrameworkVersion => project.AllEvaluatedProperties.Where (p => p.Name == "TargetFrameworkVersion").FirstOrDefault ().EvaluatedValue;
		public string Description => project.AllEvaluatedProperties.Where (p => p.Name == "Description").FirstOrDefault ().EvaluatedValue;
		public string OutputPath => project.AllEvaluatedProperties.Where (p => p.Name == "OutputPath").FirstOrDefault ().EvaluatedValue;
		public string IntermediateOutputPath => project.AllEvaluatedProperties.Where (p => p.Name == "IntermediateOutputPath").FirstOrDefault ().EvaluatedValue;
		public string StartupObject => project.AllEvaluatedProperties.Where (p => p.Name == "StartupObject").FirstOrDefault ().EvaluatedValue;
		public bool DebugSymbols => false;// nodeProps["DebugSymbols"] == null ? false : bool.Parse (nodeProps["DebugSymbols"]?.InnerText); }        
		public int WarningLevel => 0;
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
		void populateTreeNodes ()
		{
			ProjectNode root = new ProjectNode (this, ItemType.VirtualGroup, RootNamespace);
			ProjectNode refs = new ProjectNode (this, ItemType.ReferenceGroup, "References");
			root.AddChild (refs);

			/*Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine ($"Evaluated Globals properties for {DisplayName}");
			foreach (ProjectProperty item in project.AllEvaluatedProperties.OrderBy(p=>p.Name)) {
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write ($"\t{item.Name,-40} = ");
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.WriteLine ($"{item.EvaluatedValue}");
			}*/
			//ProjectInstance pInst = project.CreateProjectInstance ();
			string[] defaultTargets = { "Build", "Rebuild", "Pack", "Clean"  };
			
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
				if (pti.Name.StartsWith('_'))
					continue;
				/*
				Console.WriteLine ($"Depends: {pti.Children.Count} {pti.Name} ret: {pti.Returns} {pti.BeforeTargets} -> {pti.AfterTargets}");*/
				Commands.Add (new Crow.Command (new Action (() => Compile (pti.Name)))
				{
					Caption = pti.Name,
				});
            }
			

			foreach (ProjectItem pn in project.AllEvaluatedItems) {
				/*if (Path.GetFileName (pn.EvaluatedInclude) == "samples.style")
					System.Diagnostics.Debugger.Break ();*/
				solution.IDE.ProgressNotify (1);
				/*if (pn.EvaluatedInclude.EndsWith ("dll"))
                    System.Diagnostics.Debugger.Break ();
                foreach (ProjectMetadata md in pn.Metadata) {
					if (md.EvaluatedValue.EndsWith ("dll"))
						System.Diagnostics.Debugger.Break ();
					if (md.Location.File.EndsWith ("dll"))
						System.Diagnostics.Debugger.Break ();
				}*/
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
        //static Regex regexDirTokens = new Regex (@"\$\(([^\)]*)\)|([^\$]*)");

        //string getDirectoryWithTokens (string dir) {
        //    Match m = regexDirTokens.Match (dir);
        //    string tmp = "";
        //    while (m.Success) {
        //        if (m.Value == @"$(SolutionDir)")
        //            tmp = System.IO.Path.Combine (tmp, solution.SolutionFolder);
        //        else if (m.Value == @"$(Configuration)")
        //            tmp = System.IO.Path.Combine (tmp, "Debug");
        //        else
        //            tmp = System.IO.Path.Combine (tmp, m.Value);

        //        if (tmp.EndsWith (@"\") || tmp.EndsWith (@"/"))
        //            tmp = tmp.Remove (tmp.Length - 1);

        //        m = m.NextMatch ();
        //    }
        //    return tmp;
        //}
       
		public void Compile (string target = "Build")
		{
			/*var nativeSharedMethod = typeof (SolutionFile).Assembly.GetType ("Crow.Build.Shared.NativeMethodsShared");
			var isMonoField = nativeSharedMethod.GetField ("_isMono", BindingFlags.Static | BindingFlags.NonPublic);
			isMonoField.SetValue (null, true);

			Environment.SetEnvironmentVariable ("MSBUILD_EXE_PATH", "/usr/share/dotnet/sdk/3.1.101/MSBuild.dll");*/
			solution.IDE.projectCollection.SetGlobalProperty ("CrowIDEResolveCache", resolvedCacheFile);
			ProjectInstance pi = BuildManager.DefaultBuildManager.GetProjectInstanceForBuild (project);

			//ProjectInstance pi = new ProjectInstance (project.FullPath, solution.globalProperties, solution.toolsVersion);
			

		
			/*ILogger logger = new Microsoft.Build.Logging.ConsoleLogger {
				Verbosity = LoggerVerbosity.Diagnostic
			};*/
			//pi.Build (new string[] { "ResolveReferences" }, );
			

			BuildRequestData request = new BuildRequestData (pi, new string[] { target }, null);			
			BuildResult result = BuildManager.DefaultBuildManager.Build (solution.buildParams, request);
			
			

			/*Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine (result.ProjectStateAfterBuild.GetPropertyValue ("IntermediateOutputPath"));
			Console.WriteLine ($"\n****************** {target} *********************");
			Console.ForegroundColor = ConsoleColor.Blue;
			Console.WriteLine ($"Properties ({result.ProjectStateAfterBuild.Properties.Count}):");
			foreach (var item in result.ProjectStateAfterBuild.Properties.OrderBy(p=>p.Name)) {
				Console.WriteLine ($"\t{item.Name} = {item.EvaluatedValue}");
			}
			Console.ForegroundColor = ConsoleColor.Cyan;
			foreach (var item in result.ProjectStateAfterBuild.Items) {
				Console.WriteLine ($"{item.ItemType} {item.EvaluatedInclude}");
				foreach (var md in item.Metadata) {
					Console.WriteLine ($"\t{md.Name} = {md.EvaluatedValue}");
					
				}
			}*/
			/*
			Console.ForegroundColor = ConsoleColor.Red;
			foreach (string t in result.ResultsByTarget.Keys) {
				foreach (var item in result.ResultsByTarget[t].Items) {
					Console.WriteLine ($"{item}");
					foreach (string md in item.MetadataNames) {
						Console.WriteLine ($"\t{md} = {item.GetMetadata (md)}");
					}

				}				
			}*/
			/*Console.ForegroundColor = ConsoleColor.Blue;
			foreach (var item in result.ProjectStateAfterBuild.GetMetadataReferences()) {
				Console.WriteLine ($"\t{item.na} {item.ElementName} = {item.Include}");
			}*/
			/*Console.WriteLine ($"\n****************** {target} *********************");
			
			*/
			/*string[] dlls = solution.IDE.TargetResults.Select (t => (t as BuildMessageEventArgs).Message).ToArray ();
			foreach (string dll in dlls)
				Console.WriteLine (dll);*/
			/*var c = result.ProjectStateAfterBuild.GetCompilerCommandLineArgs ();
			var 

			//BuildSubmission submission = BuildManager.DefaultBuildManager.PendBuildRequest (request);
			BuildEventArgs[] bea = solution.IDE.BuildEvents.Where (be => be.Message.ToString ().Contains (".dll")).ToArray ();*/
			getcompilation ();

		}
		//    if (ParentProject != null)
		//        ParentProject.Compile ();

		//    CSharpCodeProvider cp = new CSharpCodeProvider ();
		//    CompilerParameters parameters = new CompilerParameters ();

		//    foreach (ProjectReference pr in flattenNodes.OfType<ProjectReference> ()) {
		//        Project p = solution.Projects.FirstOrDefault (pp => pp.ProjectGuid == pr.ProjectGUID);
		//        if (p == null)
		//            throw new Exception ("referenced project not found");
		//        parameters.ReferencedAssemblies.Add (p.Compile ());
		//    }

		//    string outputDir = getDirectoryWithTokens (this.OutputPath);
		//    string objDir = getDirectoryWithTokens (this.IntermediateOutputPath);

		//    Directory.CreateDirectory (outputDir);
		//    Directory.CreateDirectory (objDir);

		//    parameters.OutputAssembly = System.IO.Path.Combine (outputDir, this.AssemblyName);

		//    // True - exe file generation, false - dll file generation
		//    if (this.OutputType == "Library") {
		//        parameters.GenerateExecutable = false;
		//        parameters.CompilerOptions += " /target:library";
		//        parameters.OutputAssembly += ".dll";
		//    } else {
		//        parameters.GenerateExecutable = true;
		//        parameters.CompilerOptions += " /target:exe";
		//        parameters.OutputAssembly += ".exe";
		//        parameters.MainClass = this.StartupObject;
		//    }

		//    parameters.GenerateInMemory = false;
		//    parameters.IncludeDebugInformation = this.DebugSymbols;
		//    parameters.TreatWarningsAsErrors = this.TreatWarningsAsErrors;
		//    parameters.WarningLevel = this.WarningLevel;
		//    parameters.CompilerOptions += " /noconfig";
		//    if (this.AllowUnsafeBlocks)
		//        parameters.CompilerOptions += " /unsafe";
		//    parameters.CompilerOptions += " /delaysign+";
		//    parameters.CompilerOptions += " /debug:full /debug+";
		//    parameters.CompilerOptions += " /optimize-";
		//    parameters.CompilerOptions += " /define:\"DEBUG;TRACE\"";
		//    parameters.CompilerOptions += " /nostdlib";



		//    foreach (ProjectItem pi in flattenNodes.Where (p => p.Type == ItemType.Reference)) {

		//        if (string.IsNullOrEmpty (pi.HintPath)) {
		//            parameters.CompilerOptions += " /reference:/usr/lib/mono/4.5/" + pi.Path + ".dll";
		//            continue;
		//        }
		//        parameters.ReferencedAssemblies.Add (pi.Path);
		//        string fullHintPath = System.IO.Path.GetFullPath (System.IO.Path.Combine (RootDir, pi.HintPath.Replace ('\\', '/')));
		//        if (File.Exists (fullHintPath)) {
		//            string outPath = System.IO.Path.Combine (outputDir, System.IO.Path.GetFileName (fullHintPath));
		//            if (!File.Exists (outPath))
		//                File.Copy (fullHintPath, outPath);
		//        }
		//    }
		//    parameters.CompilerOptions += " /reference:/usr/lib/mono/4.5/System.Core.dll";
		//    parameters.CompilerOptions += " /reference:/usr/lib/mono/4.5/mscorlib.dll";
		//    //parameters.ReferencedAssemblies.Add ("System.Core");
		//    //parameters.ReferencedAssemblies.Add ("mscorlib.dll");


		//    IEnumerable<ProjectFile> pfs = flattenNodes.OfType<ProjectFile> ();

		//    foreach (ProjectFile pi in pfs.Where (p => p.Type == ItemType.EmbeddedResource)) {

		//        string absPath = pi.AbsolutePath;
		//        string logicName = pi.LogicalName;
		//        if (string.IsNullOrEmpty (logicName))
		//            parameters.CompilerOptions += string.Format (" /resource:{0},{1}", absPath, this.Name + "." + pi.Path.Replace ('/', '.'));
		//        else
		//            parameters.CompilerOptions += string.Format (" /resource:{0},{1}", absPath, logicName);
		//    }
		//    foreach (ProjectFile pi in pfs.Where (p => p.Type == ItemType.None)) {
		//        if (pi.CopyToOutputDirectory == CopyToOutputState.Never)
		//            continue;
		//        string source = pi.AbsolutePath;
		//        string target = System.IO.Path.Combine (outputDir, pi.Path);
		//        Directory.CreateDirectory (System.IO.Path.GetDirectoryName (target));

		//        if (File.Exists (target)) {
		//            if (pi.CopyToOutputDirectory == CopyToOutputState.PreserveNewest) {
		//                if (DateTime.Compare (
		//                        System.IO.File.GetLastWriteTime (source),
		//                        System.IO.File.GetLastWriteTime (target)) < 0)
		//                    continue;
		//            }
		//            File.Delete (target);
		//        }
		//        System.Diagnostics.Debug.WriteLine ("copy " + source + " to " + target);
		//        File.Copy (source, target);
		//    }
		//    string[] files = pfs.Where (p => p.Type == ItemType.Compile).Select (p => p.AbsolutePath).ToArray ();

		//    System.Diagnostics.Debug.WriteLine ("---- start compilation of :" + parameters.OutputAssembly);
		//    System.Diagnostics.Debug.WriteLine (parameters.CompilerOptions);

		//    CompilationResults = cp.CompileAssemblyFromFile (parameters, files);

		//    solution.UpdateErrorList ();

		//    return parameters.OutputAssembly;
		//}

		public bool TryGetProjectFileFromPath (string path, out ProjectFileNode pi)
		{
			if (path.StartsWith ("#", StringComparison.Ordinal))
				pi = Flatten.OfType<ProjectFileNode> ().FirstOrDefault 
					(f => f.Type == ItemType.EmbeddedResource && f.LogicalName == path.Substring (1));
			else
				pi = Flatten.OfType<ProjectFileNode> ().FirstOrDefault (pp => pp.FullPath == path);

			if (pi != null)
				return true;

			foreach (ProjectItemNode pr in Flatten.OfType<ProjectItemNode> ().Where (pn => pn.Type == ItemType.ProjectReference)) {
				ProjectView p = solution.Projects.FirstOrDefault (pp => pp.FullPath == pr.FullPath);
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
				string clsName = System.IO.Path.GetFileNameWithoutExtension (pi.RelativePath);
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

		public IEnumerable<SyntaxTree> SyntaxTrees {
			get {
				return Flatten.OfType<CSProjectItem> ().Select (pf => pf.SyntaxTree);				
			}
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

				CSharpCompilationOptions compileOpts = new CSharpCompilationOptions (this.OutputKind);
				List<MetadataReference> metaRefs = new List<MetadataReference> ();
				string refsTxt = resolvedCacheFile;
				if (!File.Exists (refsTxt))
					return;
				using (StreamReader sr = new StreamReader (refsTxt)) {
					while (!sr.EndOfStream) {
						string dll = sr.ReadLine ();
						if (File.Exists(dll))
							metaRefs.Add (MetadataReference.CreateFromFile (dll));
					}
				}
				SyntaxTree[] sts = SyntaxTrees.ToArray();
				Compilation comp = CSharpCompilation.Create (this.AssemblyName, sts, metaRefs);
				SyntaxTree st = sts[5];
				CompilationUnitSyntax root = st.GetCompilationUnitRoot ();
				
				SemanticModel model = comp.GetSemanticModel (st);

				SymbolInfo si = model.GetSymbolInfo (root.Usings[0].Name);
				var systemSymbol = (INamespaceSymbol)si.Symbol;
				foreach (INamespaceSymbol ns in systemSymbol.GetNamespaceMembers ()) {
					Console.WriteLine (ns);
				}
				Console.WriteLine (si.CandidateSymbols);
				
			} catch (Exception e) {
				Console.WriteLine (e);
            }
        }

	}
}

