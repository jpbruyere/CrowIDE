﻿// Copyright (c) 2016-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
		public static string DEFAULT_TOOLS_VERSION = "Current";
		public static CrowIDE MainWin;

		#region Commands
		public static Picture IcoNew = new SvgPicture ("#Icons.blank-file.svg");
		public static Picture IcoOpen = new SvgPicture ("#Icons.open.svg");
		public static Picture IcoSave = new SvgPicture ("#Icons.save.svg");
		public static Picture IcoSaveAs = new SvgPicture ("#Icons.save.svg");
		public static Picture IcoQuit = new SvgPicture ("#Icons.sign-out.svg");
		public static string IcoUndo = "#Icons.undo.svg";
		public static string IcoRedo = "#Icons.redo.svg";

		public static string IcoCut = "#Icons.scissors.svg";
		public static string IcoCopy = "#Icons.copy-file.svg";
		public static string IcoPaste = "#Icons.paste-on-document.svg";
		public static string IcoCloseSolution = IcoPaste;
		public static Picture IcoHelp = new SvgPicture ("#Icons.question.svg");

		public static Picture IcoReference = new SvgPicture ("#Icons.cube.svg");
		public static Picture IcoPackageReference = new SvgPicture ("#Icons.package.svg");

		public static Picture IcoFileCS = new SvgPicture ("#Icons.cs-file.svg");
		public static Picture IcoFileXML = new SvgPicture ("#Icons.file-code.svg");


		public Command CMDNew, CMDOpen, CMDSave, CMDSaveAs, cmdCloseSolution, CMDQuit,
		CMDUndo, CMDRedo, CMDCut, CMDCopy, CMDPaste, CMDHelp, CMDAbout, CMDOptions,
		CMDViewGTExp, CMDViewProps, CMDViewProj, CMDViewProjProps, CMDViewErrors, CMDViewLog, CMDViewSolution, CMDViewEditor, CMDViewProperties,
		CMDViewToolbox, CMDViewSchema, CMDViewStyling,CMDViewDesign, CMDViewSyntaxTree, CMDViewSyntaxThemeEditor,
		CMDBuild, CMDClean, CMDRestore, CMDStartDebug;

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
			{ Caption = "Close Solution", Icon = IcoCloseSolution, CanExecute = false};

			CMDViewErrors = new Command(new Action(() => loadWindow ("#ui.winErrors.crow",this)))
			{ Caption = "Errors pane"};
			CMDViewLog = new Command(new Action(() => loadWindow ("#ui.winLog.crow",this)))
			{ Caption = "Log View"};
			CMDViewSolution = new Command(new Action(() => loadWindow ("#ui.winSolution.crow",this)))
			{ Caption = "Solution Tree", CanExecute = true};
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
			CMDViewSyntaxThemeEditor = new Command (new Action (() => loadWindow ("#ui.winThemeEditor.crow", this)))
			{ Caption = "Syntax Theme Editor", CanExecute = true };

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
			//currentSolution.OpenedItems.Add(new ProjectFileNode());
		}
		void saveFileDialog() {}
		void undo() {}
		void redo() {}
		void cut () { }
		void copy () { }
		void paste () { }

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
		string mainLoggerSearchString;



		public ProjectCollection projectCollection { get; private set; }
		public ObservableList<BuildEventArgs> BuildEvents { get; private set; } = new ObservableList<BuildEventArgs> ();

		//public MSBuildWorkspace Workspace { get; private set; }
		public ProgressLog ProgressLogger { get; private set; }

		SolutionView currentSolution;
		ProjectView currentProject;
		Editor currentEditor;

		public CrowIDE () : base (1024, 800) { }

		protected override void OnInitialized () {
			base.OnInitialized ();
			initIde ();
			reloadWinConfigs ();
			if (ReopenLastSolution && !string.IsNullOrEmpty (LastOpenSolution)) 
				Task.Run (() => loadSolution (LastOpenSolution));
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
			//var host = MefHostServices.Create (MSBuildMefHostServices.DefaultAssemblies);			
			ProgressLogger = new ProgressLog (this);			

			projectCollection = new ProjectCollection (null,
										new ILogger [] { new IdeLogger (this, MainLoggerVerbosity) },
										ToolsetDefinitionLocations.Default);

			projectCollection.SetGlobalProperty ("RestoreConfigFile", Path.Combine (
							Path.Combine (
								Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".nuget"), "NuGet"),
								"NuGet.Config"));
			string crowIdePath = Path.GetDirectoryName (Assembly.GetEntryAssembly ().Location);
			projectCollection.SetGlobalProperty ("CrowIDEPath", crowIdePath);
			//https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019
			projectCollection.SetGlobalProperty ("CustomBeforeMicrosoftCommonTargets", Path.Combine(crowIdePath, @"src\CustomCrowIDE.targets"));
			//projectCollection.SetGlobalProperty ("CustomAfterMicrosoftCommonProps", Path.Combine (crowIdePath, @"src\CustomCrowIDE.props"));
			
			initCommands ();

			Widget go = Load (@"#ui.CrowIDE.crow");
			go.DataSource = this;

			mainDock = go.FindByName ("mainDock") as DockStack;

			instFileDlg = Instantiator.CreateFromImlFragment
				(this, "<FileDialog Caption='Open File' CurrentDirectory='{²CurrentDirectory}' SearchPattern='*.sln' OkClicked='onFileOpen'/>");

			reloadSyntaxTheme ();
		}

		public void onFileOpen (object sender, EventArgs e)
		{
			FileDialog fd = sender as FileDialog;

			string filePath = fd.SelectedFileFullPath;

			//try {
				string ext = Path.GetExtension (filePath);
				if (string.Equals (ext, ".sln", StringComparison.InvariantCultureIgnoreCase)) {
					Task.Run (() => loadSolution (filePath));
					//				}else if (string.Equals (ext, ".csproj", StringComparison.InvariantCultureIgnoreCase)) {
					//					currentProject = new Project (filePath);
				}
			/*} catch (Exception ex) {
				LoadIMLFragment ("<MessageBox Message='" + ex.Message + "\n" + "' MsgType='Error'/>");
			}*/
		}

		void loadSolution (string filePath) {
			ProgressInit (5000, "loading solution ..");
			CurrentSolution = new SolutionView (this, filePath);
			LastOpenSolution = filePath;
			CurrentSolution.ReopenItemsSavedInUserConfig ();
			ProgressVisible = false;
		}
		void closeSolution () {
			lock (UpdateMutex) {
				if (currentSolution != null)
					currentSolution.CloseSolution ();
				CurrentProject = null;
				CurrentSolution = null;
			}
		}

		public string NetcoredbgPath {
			get => Configuration.Global.Get<string> ("NetcoredbgPath");
			set {
				if (value == NetcoredbgPath)
					return;
				Configuration.Global.Set ("NetcoredbgPath", value);
				NotifyValueChanged (value);
			}
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
				lock (UpdateMutex)
					NotifyValueChanged (currentSolution);
			}
		}
		public ProjectView CurrentProject {
			get { return currentProject; }
			set {
				if (currentProject == value)
					return;
				currentProject = value;

				CMDViewProjProps.CanExecute = (currentProject != null);
				
				lock (UpdateMutex)
					NotifyValueChanged (currentProject);				
			}
		}
		/// <summary>
		/// Currently focused editor.
		/// </summary>
		public Editor CurrentEditor {
			get => currentEditor;
			set {
				if (currentEditor == value)
					return;
				currentEditor = value;
				NotifyValueChanged (currentEditor);				
			}
		}
		public string LastOpenSolution {
			get { return Crow.Configuration.Global.Get<string>("LastOpenSolution");}
			set {
				if (LastOpenSolution == value)
					return;
				Crow.Configuration.Global.Set ("LastOpenSolution", value);
				NotifyValueChanged (value);
			}
		}
		#region Options
		public string SDKFolder {
			get => Configuration.Global.Get<string> ("SDKFolder");
			set {
				if (SDKFolder == value)
					return;
				Configuration.Global.Set ("SDKFolder", value);
				NotifyValueChanged (SDKFolder);
			}
		}
		public string MSBuildRoot {
			get => Configuration.Global.Get<string> ("MSBuildRoot");
			set {
				if (MSBuildRoot == value)
					return;
				Configuration.Global.Set ("MSBuildRoot", value);
				NotifyValueChanged (MSBuildRoot);
			}
		}
		public void onClickSelectSDKFolder (object sender, EventArgs e) {
			this.LoadIMLFragment (@"
				<FileDialog Caption='Select SDK Folder' CurrentDirectory='{SDKFolder}'
							ShowFiles='false' ShowHidden='true' OkClicked='onSelectSDKFolder'/>
			").DataSource = this;
		}
		public void onSelectSDKFolder (object sender, EventArgs e) {
			FileDialog fd = sender as FileDialog;
			SDKFolder = fd.SelectedDirectory;
		}
		public void onClickSelectMSBuildRoot (object sender, EventArgs e) {
			this.LoadIMLFragment (@"
				<FileDialog Caption='Select MSBuild Root' CurrentDirectory='{MSBuildRoot}'
							ShowFiles='false' ShowHidden='true' OkClicked='onSelectMSBuildRoot'/>
			").DataSource = this;
		}
		public void onSelectMSBuildRoot (object sender, EventArgs e) {
			FileDialog fd = sender as FileDialog;
			MSBuildRoot = fd.SelectedDirectory;
		}
		public void onClickSelectNetcoredbgPath (object sender, EventArgs e) {
			this.LoadIMLFragment (@"
				<FileDialog Caption='Select netcoredbg executable path' CurrentDirectory='{NetcoredbgPath}'
							ShowFiles='true' ShowHidden='true' OkClicked='onSelectNetcoredbgPath'/>
			").DataSource = this;
		}
		public void onSelectNetcoredbgPath (object sender, EventArgs e) {
			FileDialog fd = sender as FileDialog;
			NetcoredbgPath = fd.SelectedFileFullPath;
		}
		public bool ReopenLastSolution {
			get => Crow.Configuration.Global.Get<bool>("ReopenLastSolution");
			set {
				if (ReopenLastSolution == value)
					return;
				Crow.Configuration.Global.Set ("ReopenLastSolution", value);
				NotifyValueChanged (value);
			}
		}
		public LoggerVerbosity MainLoggerVerbosity {
			get => Crow.Configuration.Global.Get<LoggerVerbosity>("MainLoggerVerbosity");
			set {
				if (MainLoggerVerbosity == value)
					return;
				if (projectCollection != null)
					projectCollection.Loggers.First ().Verbosity = value;
				Crow.Configuration.Global.Set ("MainLoggerVerbosity", value);
				NotifyValueChanged ("MainLoggerVerbosity", value);
			}
		}
		public string MainLoggerSearchString {
			get => mainLoggerSearchString;
			set {
				if (mainLoggerSearchString == value)
					return;
				mainLoggerSearchString = value;
				NotifyValueChanged (mainLoggerSearchString);
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
		void refreshAllEditors () {
			if (CurrentSolution == null)
				return;
			foreach (ProjectFileNode pfn in CurrentSolution.OpenedItems.OfType<ProjectFileNode> ()) {
				foreach (Editor editor in pfn.RegisteredEditors.Keys.OfType<Editor> ()) 
					editor.RegisterForRedraw ();				
			}
		}
		public Dictionary<string, TextFormatting> SyntaxTheme;

		void reloadSyntaxTheme () {			
			if (!File.Exists (syntaxThemeFile))
				return;
			SyntaxTheme = new Dictionary<string, TextFormatting> ();
			using (StreamReader sr = new StreamReader(syntaxThemeFile)) {
				while (!sr.EndOfStream) {
					string l = sr.ReadLine ();
					string[] tmp = l.Split ('=');
					SyntaxTheme.Add (tmp[0].Trim (), TextFormatting.Parse (tmp[1].Trim ()));
				}
            }
			NotifyValueChanged ("SyntaxTheme", SyntaxTheme);
			refreshAllEditors ();
		}
		void saveSyntaxTheme () {
			using (StreamWriter sw = new StreamWriter (syntaxThemeFile)) {
				foreach (string key in SyntaxTheme.Keys) {
					sw.WriteLine ($"{key} = {SyntaxTheme[key]}");
				}
			}
		}

		private void onClickReloadSyntaxTheme (object sender, MouseButtonEventArgs e) {
			reloadSyntaxTheme ();
		}
		private void onClickSaveSyntaxTheme (object sender, MouseButtonEventArgs e) {
			saveSyntaxTheme ();
		}
		private void onClickSaveSyntaxThemeAs (object sender, MouseButtonEventArgs e) {
			throw new NotImplementedException ();
		}

		string syntaxThemeDirectory => Path.Combine (Path.GetDirectoryName (Assembly.GetEntryAssembly ().Location), "SyntaxThemes");
		string syntaxThemeFile => Path.Combine (syntaxThemeDirectory, $"{SyntaxThemeName}.syntax");

		public string[] AvailableSyntaxThemes {
			get {
				string[] tmp = Directory.GetFiles (syntaxThemeDirectory);
                for (int i = 0; i < tmp.Length; i++)
					tmp[i] = Path.GetFileNameWithoutExtension (tmp[i]);
				return tmp;
            }
        }
		public string SyntaxThemeName {
			get { return Configuration.Global.Get<string> ("SyntaxThemeName"); }
			set {
				if (SyntaxThemeName == value)
					return;
				Configuration.Global.Set ("SyntaxThemeName", value);
				NotifyValueChanged ("SyntaxThemeName", (object)SyntaxThemeName);
				reloadSyntaxTheme ();
			}
		}

		public bool AutoFoldRegions {
			get { return Crow.Configuration.Global.Get<bool> ("AutoFoldRegions"); }
			set {
				if (AutoFoldRegions == value)
					return;
				Crow.Configuration.Global.Set ("AutoFoldRegions", value);
				NotifyValueChanged (value);
			}
		}
		public bool AutoFoldComments {
			get { return Crow.Configuration.Global.Get<bool> ("AutoFoldComments"); }
			set {
				if (AutoFoldComments == value)
					return;
				Crow.Configuration.Global.Set ("AutoFoldComments", value);
				NotifyValueChanged (value);
			}
		}
		#endregion

		#region Status bar
		bool progressVisible = false;
		double progressMax = 10;
		double progressValue = 0;
		string progressMessage = "";

		public string ProgressMessage {
			get => progressMessage;
			set {
				if (progressMessage == value)
					return;
				progressMessage = value;
				NotifyValueChanged (progressMessage);
			}
		}
		public bool ProgressVisible {
			get => progressVisible;
			set {
				if (progressVisible == value)
					return;
				progressVisible = value;
				NotifyValueChanged (progressVisible);
			}
		}
		public double ProgressMax {
			get => progressMax;
			set {
				if (progressMax == value)
					return;
				progressMax = value;
				NotifyValueChanged (progressMax);
			}
		}
		public double ProgressValue {
			get => progressValue;
			set {
				if (progressValue == value)
					return;
				progressValue = value;
				NotifyValueChanged (progressValue);
			}
		}
		public void ProgressInit (int steps, string message = "") {
			ProgressVisible = true;
			ProgressMessage = message;
			ProgressMax = steps;
			ProgressValue = 1;
		}
		public void ProgressNotify (int steps, string message = "") {
			ProgressValue += steps;
			if (!string.IsNullOrEmpty (message))
				ProgressMessage = message;
		}
		#endregion

		Window loadWindow (string path, object dataSource = null){
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