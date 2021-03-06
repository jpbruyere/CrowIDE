﻿// Copyright (c) 2016-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Crow.IML;
using Glfw;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MSBuild;

namespace Crow.Coding
{
	public class CrowIDE : Interface
	{
		public static string DEFAULT_TOOLS_VERSION = "15.0";
		public static CrowIDE MainWin;

		#region Commands
		public static Picture IcoNew = new SvgPicture ("#Icons.blank-file.svg");
		public static Picture IcoOpen = new SvgPicture ("#Icons.open.svg");
		public static Picture IcoSave = new SvgPicture ("#Icons.save.svg");
		public static Picture IcoSaveAs = new SvgPicture ("#Icons.save.svg");
		public static Picture IcoQuit = new SvgPicture ("#Icons.sign-out.svg");
		public static Picture IcoUndo = new SvgPicture ("#Icons.undo.svg");
		public static Picture IcoRedo = new SvgPicture ("#Icons.redo.svg");

		public static Picture IcoCut = new SvgPicture ("#Icons.scissors.svg");
		public static Picture IcoCopy = new SvgPicture ("#Icons.copy-file.svg");
		public static Picture IcoPaste = new SvgPicture ("#Icons.paste-on-document.svg");
		public static Picture IcoHelp = new SvgPicture ("#Icons.question.svg");

		public static Picture IcoReference = new SvgPicture ("#Icons.cube.svg");
		public static Picture IcoPackageReference = new SvgPicture ("#Icons.package.svg");

		public static Picture IcoFileCS = new SvgPicture ("#Icons.cs-file.svg");
		public static Picture IcoFileXML = new SvgPicture ("#Icons.file-code.svg");


		public Command CMDNew, CMDOpen, CMDSave, CMDSaveAs, cmdCloseSolution, CMDQuit,
		CMDUndo, CMDRedo, CMDCut, CMDCopy, CMDPaste, CMDHelp, CMDAbout, CMDOptions,
		CMDViewGTExp, CMDViewProps, CMDViewProj, CMDViewProjProps, CMDViewErrors, CMDViewLog, CMDViewSolution, CMDViewEditor, CMDViewProperties,
		CMDViewToolbox, CMDViewSchema, CMDViewStyling,CMDViewDesign, CMDViewSyntaxTree,
		CMDBuild, CMDClean, CMDRestore;

		void initCommands () {
			CMDNew = new Command(new Action(newFile)) { Caption = "New", Icon = IcoNew, CanExecute = true};
			CMDOpen = new Command(new Action(openFileDialog)) { Caption = "Open...", Icon = IcoOpen };
			CMDSave = new Command(new Action(saveFileDialog)) { Caption = "Save", Icon = IcoSave, CanExecute = false};
			CMDSaveAs = new Command(new Action(saveFileDialog)) { Caption = "Save As...", Icon = IcoSaveAs, CanExecute = false};
			CMDQuit = new Command(new Action(Quit)) { Caption = "Quit", Icon = IcoQuit };
			CMDUndo = new Command(new Action(undo)) { Caption = "Undo", Icon = IcoUndo, CanExecute = false};
			CMDRedo = new Command(new Action(redo)) { Caption = "Redo", Icon = IcoRedo, CanExecute = false};
            CMDCut = new Command(new Action(cut)) { Caption = "Cut", Icon = IcoCut, CanExecute = false};
            CMDCopy = new Command(new Action(copy)) { Caption = "Copy", Icon = IcoCopy, CanExecute = false};
            CMDPaste = new Command(new Action(paste)) { Caption = "Paste", Icon = IcoPaste, CanExecute = false};
            CMDHelp = new Command(new Action(() => System.Diagnostics.Debug.WriteLine("help"))) { Caption = "Help", Icon = IcoHelp };
			CMDOptions = new Command(new Action(() => loadWindow("#ui.Options.crow", this))) { Caption = "Editor Options", Icon = new SvgPicture("#Icons.tools.svg") };

			cmdCloseSolution = new Command(new Action(closeSolution))
			{ Caption = "Close Solution", Icon = new SvgPicture("#Icons.paste-on-document.svg"), CanExecute = false};

			CMDViewErrors = new Command(new Action(() => loadWindow ("#ui.winErrors.crow",this)))
			{ Caption = "Errors pane"};
			CMDViewLog = new Command(new Action(() => loadWindow ("#ui.winLog.crow",this)))
			{ Caption = "Log View"};
			CMDViewSolution = new Command(new Action(() => loadWindow ("#ui.winSolution.crow",this)))
			{ Caption = "Solution Tree", CanExecute = false};
			CMDViewEditor = new Command(new Action(() => loadWindow ("#ui.winEditor.crow",this)))
			{ Caption = "Editor Pane"};
			CMDViewProperties = new Command(new Action(() => loadWindow ("#ui.winProperties.crow",this)))
			{ Caption = "Properties"};
			CMDViewDesign = new Command(new Action(() => loadWindow ("#ui.winDesign.crow",this)))
			{ Caption = "Quick Design", CanExecute = true};
			CMDViewToolbox = new Command(new Action(() => loadWindow ("#ui.winToolbox.crow",this)))
			{ Caption = "Toolbox", CanExecute = false};
			CMDViewSchema = new Command(new Action(() => loadWindow ("#ui.winSchema.crow",this)))
			{ Caption = "IML Shematic View", CanExecute = true};
			CMDViewStyling = new Command(new Action(() => loadWindow ("#ui.winStyleView.crow",this)))
			{ Caption = "Styling Explorer", CanExecute = true};
			CMDViewGTExp = new Command(new Action(() => loadWindow ("#ui.winGTExplorer.crow",this)))
			{ Caption = "Graphic Tree Explorer", CanExecute = true};
			CMDViewSyntaxTree = new Command (new Action (() => loadWindow ("#ui.winSyntaxTree.crow", currentSolution)))
			{ Caption = "Syntax Tree", CanExecute = true };

			CMDBuild = new Command(new Action(() => CurrentSolution?.Build ("Build")))
			{ Caption = "Compile Solution", CanExecute = false};
			CMDClean = new Command(new Action(() => CurrentSolution?.Build ("Clean")))
			{ Caption = "Clean Solution", CanExecute = false};
			CMDRestore = new Command(new Action(() => CurrentSolution?.Build ("Restore")))
			{ Caption = "Restore packages", CanExecute = false};
			CMDViewProjProps = new Command (new Action (() => loadWindow ("#ui.ProjectProperties.crow")))
			{ Caption = "Project Properties", CanExecute = false };
		}

		void openFileDialog () {			
			AddWidget (instFileDlg.CreateInstance()).DataSource = this;
		}
		void openOptionsDialog(){}
		void newFile() {			
			currentSolution.OpenedItems.Add(new ProjectFileNode());
		}
		void saveFileDialog() {}
		void undo() {}
		void redo() {}
		void cut () { }
		void copy () { }
		void paste () { }
		void closeSolution (){
			if (currentSolution != null)
				currentSolution.CloseSolution ();
			CurrentSolution = null;
		}

		public void saveWinConfigs() {
			Configuration.Global.Set ("WinConfigs", mainDock.ExportConfig ());
			Configuration.Global.Save ();
		}
		public void reloadWinConfigs() {
			string conf = Configuration.Global.Get<string>("WinConfigs");
			if (string.IsNullOrEmpty (conf))
				return;
			mainDock.ImportConfig (conf, this);
		}
		#endregion

		Instantiator instFileDlg;
		DockStack mainDock;

		public ProjectCollection projectCollection { get; private set; }
		public ObservableList<BuildEventArgs> BuildEvents { get; private set; } = new ObservableList<BuildEventArgs> ();

		public MSBuildWorkspace Workspace { get; private set; }
		public ProgressLog ProgressLogger { get; private set; }

		SolutionView currentSolution;
		ProjectView currentProject;

		public CrowIDE () : base (1024, 800) { }

		protected override void OnInitialized () {
			base.OnInitialized ();

			initIde ();

			reloadWinConfigs ();

			if (ReopenLastSolution && !string.IsNullOrEmpty (LastOpenSolution)) {
				CurrentSolution = new SolutionView (this, LastOpenSolution);

				Monitor.Enter (LayoutMutex);

				while (!Monitor.TryEnter (UpdateMutex)) {
					Thread.Sleep (1);
					Monitor.Wait (LayoutMutex, 10);
				}

				CurrentSolution.ReopenItemsSavedInUserConfig ();

				Monitor.Exit (UpdateMutex);
				Monitor.Exit (LayoutMutex);
			}
		}

		public override bool OnKeyDown (Key key)
		{
			switch (key) {
			case Key.F2:
				loadWindow ("#ui.winDebugLog.crow", this);
				break;
			default:
				return base.OnKeyDown (key);
			}
			return true;
		}

		void initIde() {
			var host = MefHostServices.Create (MSBuildMefHostServices.DefaultAssemblies);
			Workspace = MSBuildWorkspace.Create (host);
			Workspace.WorkspaceFailed += (sender, e) => Console.WriteLine ($"Workspace error: {e.Diagnostic}");
			ProgressLogger = new ProgressLog ();
			projectCollection = new ProjectCollection (null, new ILogger [] { new IdeLogger (this) }, ToolsetDefinitionLocations.Default) {
				//DefaultToolsVersion = DEFAULT_TOOLS_VERSION,

			};

			projectCollection.SetGlobalProperty ("RestoreConfigFile",
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile),".nuget/NuGet/NuGet.Config"));

			initCommands ();

			Widget go = Load (@"#ui.CrowIDE.crow");
			go.DataSource = this;

			mainDock = go.FindByName ("mainDock") as DockStack;

			instFileDlg = Instantiator.CreateFromImlFragment
				(this, "<FileDialog Caption='Open File' CurrentDirectory='{²CurrentDirectory}' SearchPattern='*.sln' OkClicked='onFileOpen'/>");
				
		}
		public void onFileOpen (object sender, EventArgs e)
		{
			FileDialog fd = sender as FileDialog;

			string filePath = fd.SelectedFileFullPath;

			//try {
				string ext = Path.GetExtension (filePath);
				if (string.Equals (ext, ".sln", StringComparison.InvariantCultureIgnoreCase)) {
					CurrentSolution = new SolutionView (this, filePath);
					LastOpenSolution = filePath;
					//				}else if (string.Equals (ext, ".csproj", StringComparison.InvariantCultureIgnoreCase)) {
					//					currentProject = new Project (filePath);
				}
			/*} catch (Exception ex) {
				LoadIMLFragment ("<MessageBox Message='" + ex.Message + "\n" + "' MsgType='Error'/>");
			}*/
		}


		public string CurrentDirectory {
			get => Crow.Configuration.Global.Get<string>("CurrentDirectory");
			set => Crow.Configuration.Global.Set ("CurrentDirectory", value);
		}
		public SolutionView CurrentSolution {
			get { return currentSolution; }
			set {
				if (currentSolution == value)
					return;
				
				currentSolution = value;

				CMDBuild.CanExecute = CMDClean.CanExecute = CMDRestore.CanExecute = (currentSolution != null);
				cmdCloseSolution.CanExecute = (currentSolution != null);
				CMDViewSolution.CanExecute = (currentSolution != null);
				
				lock (UpdateMutex) {
					NotifyValueChanged ("CurrentSolution", currentSolution);
				}
			}
		}
		public ProjectView CurrentProject {
			get { return currentProject; }
			set {
				if (currentProject == value)
					return;
				currentProject = value;

				CMDViewProjProps.CanExecute = (currentProject != null);
				
				lock (UpdateMutex) {
					NotifyValueChanged ("CurrentProject", currentProject);
				}
			}
		}

		public string LastOpenSolution {
			get { return Crow.Configuration.Global.Get<string>("LastOpenSolution");}
			set {
				if (LastOpenSolution == value)
					return;
				Crow.Configuration.Global.Set ("LastOpenSolution", value);
				NotifyValueChanged ("LastOpenSolution", value);
			}
		}
		public bool ReopenLastSolution {
			get { return Crow.Configuration.Global.Get<bool>("ReopenLastSolution");}
			set {
				if (ReopenLastSolution == value)
					return;
				Crow.Configuration.Global.Set ("ReopenLastSolution", value);
				NotifyValueChanged ("ReopenLastSolution", value);
			}
		}
		public LoggerVerbosity MainLoggerVerbosity {
			get => projectCollection == null ? LoggerVerbosity.Normal : projectCollection.Loggers.First ().Verbosity;
			set {
				if (MainLoggerVerbosity == value)
					return;
				if (projectCollection != null)
					projectCollection.Loggers.First ().Verbosity = value;
				NotifyValueChanged ("MainLoggerVerbosity", MainLoggerVerbosity);
			}
		}
		public bool PrintLineNumbers {
			get { return Configuration.Global.Get<bool> ("PrintLineNumbers"); }
			set {
				if (PrintLineNumbers == value)
					return;
				Configuration.Global.Set ("PrintLineNumbers", value);
				NotifyValueChanged ("PrintLineNumbers", PrintLineNumbers);

				foreach (ProjectFileNode pfn in currentSolution?.OpenedItems.OfType<ProjectFileNode>()) {
					foreach (RoslynEditor re in pfn.RegisteredEditors.Keys.OfType<RoslynEditor> ()) { //TODO:create a base class for source editors
						re.measureLeftMargin ();
						re.RegisterForGraphicUpdate ();
					}
				}
			}
		}

		Window loadWindow(string path, object dataSource = null){
			try {
				Widget g = FindByName (path);
				if (g != null)
					return g as Window;
				g = Load (path);
				g.Name = path;
				g.DataSource = dataSource;
				return g as Window;
			} catch (Exception ex) {
				Console.WriteLine (ex.ToString ());
			}
			return null;
		}
		void closeWindow (string path){
			Widget g = FindByName (path);
			if (g != null)
				DeleteWidget (g);
		}

		protected void onCommandSave(object sender, MouseButtonEventArgs e){
			System.Diagnostics.Debug.WriteLine("save");
		}
	}
}