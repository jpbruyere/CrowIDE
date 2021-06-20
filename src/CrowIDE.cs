// Copyright (c) 2016-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
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
		public static Picture IcoTools = new SvgPicture("#Icons.tools.svg");
		public static Picture IcoStyle = new SvgPicture ("#Icons.palette.svg");
		public static Picture IcoImage = new SvgPicture ("#Icons.picture-file.svg");


		static Command CMDCloseSolution = 
			new Command("Close Solution", (sender) => ((sender as Widget).IFace as CrowIDE).closeSolution (), IcoCloseSolution, false);

		public Command CMDNew, CMDOpen, CMDSave, CMDSaveAs, CMDQuit,
		CMDUndo, CMDRedo, CMDCut, CMDCopy, CMDPaste, CMDHelp, CMDAbout, CMDOptions,
		CMDViewGTExp, CMDViewProps, CMDViewProj, CMDViewProjProps, CMDViewErrors, CMDViewLog, CMDViewSolution, CMDViewEditor, CMDViewProperties,
		CMDViewToolbox, CMDViewSchema, CMDViewStyling,CMDViewDesign, CMDViewSyntaxTree, CMDViewSyntaxThemeEditor, CMDViewDebugger,
		CMDBuild, CMDClean, CMDRestore, CMDStartDebug;

		public CommandGroup AllIdeCommands => new CommandGroup (
			FileCommands,
			ViewCommands
		);
		public CommandGroup FileCommands = new CommandGroup ("File",
 			new Command("New", (sender) => ((sender as Widget).IFace as CrowIDE).newFile (), IcoNew),
			new Command("Open...", (sender) => ((sender as Widget).IFace as CrowIDE).openFileDialog (), IcoOpen),
			CMDCloseSolution ,
			new Command("Quit", (sender) => ((sender as Widget).IFace as CrowIDE).Quit (), IcoQuit),
			new Command("Editor Options", (sender) => {
				Widget w = sender as Widget;
				(w.IFace as CrowIDE).loadWindow("#ui.Options.crow", w.IFace);
			}, IcoTools)
		);
		public CommandGroup EditCommands = new CommandGroup("Debug");

		static void loadWindowWithIdeAsDataSource(object sender, string path) {
			Widget w = sender as Widget;
			CrowIDE ide = w.IFace as CrowIDE;
			ide.loadWindow (path, ide);
		}
			

		public CommandGroup ViewCommands = new CommandGroup ("View",
 			new Command("Log View", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winLog.crow")),
 			new Command("Solution Tree", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winSolution.crow")),
 			new Command("Editor Pane", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winEditor.crow")),
 			new Command("Properties", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winProperties.crow")),
 			new Command("ItemProperties", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winItemProperties.crow")),
 			new Command("Toolbox", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winToolbox.crow")),
 			new Command("IML Shematic View", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winSchema.crow")),
 			new Command("Styling Explorer", (sender) => loadWindowWithIdeAsDataSource (sender, "")),
 			new Command("Graphic Tree Explorer", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winGTExplorer.crow")),
 			new Command("Syntax Tree", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winSyntaxTree.crow")),
 			new Command("Syntax Theme Editor", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winThemeEditor.crow")),
 			new Command("Debugger", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winDebugger.crow")),
 			new Command("Errors pane", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winErrors.crow")),
			new CommandGroup ("Debug Windows",
	 			new Command("Watches", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winWatches.crow")),
	 			new Command("Call Stack", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winStackFrames.crow")),
	 			new Command("Threads", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winThreads.crow")),
	 			new Command("BreakPoints", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winBreakPoints.crow")),
	 			new Command("Debugger Logs", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winDebuggerLog.crow")),
	 			new Command("Debugger", (sender) => loadWindowWithIdeAsDataSource (sender, "#ui.winDebuggerLog.crow"))
			)
		);
		public CommandGroup DebugCommands = new CommandGroup("Debug");

		Command createLoadWinIdeCommand (string caption, string window, string icon = null, bool canExecute=true)
			=> new Command(caption, (sender) => ((sender as Widget).IFace as CrowIDE).loadWindow (window,((sender as Widget).IFace as CrowIDE)), icon, canExecute);
		

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


		
		public ObservableList<BuildEventArgs> BuildEvents { get; private set; } = new ObservableList<BuildEventArgs> ();

		//public MSBuildWorkspace Workspace { get; private set; }
		public ProgressLog ProgressLogger { get; private set; }
		public IdeLogger MainIdeLogger { get; private set; }
		public Dictionary<string, string> GlobalProperties { get; private set; }

		SolutionView currentSolution;
		ProjectView currentProject;
		Editor currentEditor;

		public CrowIDE () : base (Configuration.Global.Get<int>("MainWinWidth"), Configuration.Global.Get<int>("MainWinHeight")) {

		}
		public override void ProcessResize(Rectangle bounds)
		{
			base.ProcessResize(bounds);
			Configuration.Global.Set ("MainWinWidth", clientRectangle.Width);
			Configuration.Global.Set ("MainWinHeight", clientRectangle.Height);
		}

		protected override void OnInitialized () {
			base.OnInitialized ();

			SetWindowIcon ("#CrowIDE.images.crow.png");
			
			MainIdeLogger  = new IdeLogger (this, MainLoggerVerbosity);
			ProgressLogger = new ProgressLog (this);

			GlobalProperties = new Dictionary<string, string>();
			GlobalProperties.Add ("RestoreConfigFile", Path.Combine (
							Path.Combine (
								Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), ".nuget"), "NuGet"),
								"NuGet.Config"));
			string crowIdePath = Path.GetDirectoryName (Assembly.GetEntryAssembly ().Location);
			GlobalProperties.Add ("CrowIDEPath", crowIdePath);
			//https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019
			GlobalProperties.Add ("CustomBeforeMicrosoftCommonTargets", Path.Combine(crowIdePath, @"src\CustomCrowIDE.targets"));
			//projectCollection.SetGlobalProperty ("CustomAfterMicrosoftCommonProps", Path.Combine (crowIdePath, @"src\CustomCrowIDE.props"));
			
			initCommands ();

			Widget go = Load (@"#ui.CrowIDE.crow");
			go.DataSource = this;

			mainDock = go.FindByName ("mainDock") as DockStack;

			instFileDlg = Instantiator.CreateFromImlFragment
				(this, "<FileDialog Caption='Open File' CurrentDirectory='{²CurrentDirectory}' SearchPattern='*.sln' OkClicked='onFileOpen'/>");

			reloadSyntaxTheme (this);

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
			case Key.F7:
				CMDBuild.Execute ();
				break;
			case Key.F5:
				CurrentSolution?.CMDDebugStart.Execute ();
				break;
			case Key.F10:
				CurrentSolution?.CMDDebugStepOver.Execute ();
				break;
			case Key.F11:
				if (Shift)
					CurrentSolution?.CMDDebugStepOut.Execute ();
				else
					CurrentSolution?.CMDDebugStepIn.Execute ();
				break;
			default:
				return base.OnKeyDown (key);
			}
			return true;
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
			ProgressTerminate();
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
				CMDCloseSolution.CanExecute = (currentSolution != null);
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
			get => Crow.Configuration.Global.Get<string>("LastOpenSolution");
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
		public Command CMDOptions_SelectSDKFolder = new Command ("...",
			(sender) => {
				CrowIDE ide = (sender as Widget).IFace as CrowIDE;
				FileDialog dlg = ide.LoadIMLFragment<FileDialog> (@"
				<FileDialog Caption='Select SDK Folder' CurrentDirectory='{SDKFolder}'
							ShowFiles='false' ShowHidden='true' />");
				dlg.OkClicked += (sender, e) => ide.SDKFolder = (sender as FileDialog).SelectedFileFullPath;
				dlg.DataSource = ide;
			}
		);
		public Command CMDOptions_SelectMSBuildRoot = new Command ("...",
			(sender) => {
				CrowIDE ide = (sender as Widget).IFace as CrowIDE;
				FileDialog dlg = ide.LoadIMLFragment<FileDialog> (@"
					<FileDialog Caption='Select MSBuild Root' CurrentDirectory='{MSBuildRoot}'
								ShowFiles='false' ShowHidden='true'/>");
				dlg.OkClicked += (sender, e) => ide.MSBuildRoot = (sender as FileDialog).SelectedFileFullPath;
				dlg.DataSource = ide;
			}
		);
		public Command CMDOptions_SelectNetcoredbgPath = new Command ("...",
			(sender) => {
				CrowIDE ide = (sender as Widget).IFace as CrowIDE;
				FileDialog dlg = ide.LoadIMLFragment<FileDialog> (@"
					<FileDialog Caption='Select netcoredbg executable path' CurrentDirectory='{NetcoredbgPath}'
								ShowFiles='true' ShowHidden='true'/>
				");
				dlg.OkClicked += (sender, e) => ide.NetcoredbgPath = (sender as FileDialog).SelectedFileFullPath;
				dlg.DataSource = ide;
			}
		);
		public Command CMDSyntaxTheme_Reload = new Command ("Reload",
			(sender) => reloadSyntaxTheme ((sender as Widget).IFace as CrowIDE));
		public Command CMDSyntaxTheme_Save = new Command ("Save",
			(sender) => saveSyntaxTheme ((sender as Widget).IFace as CrowIDE));
		public Command CMDSyntaxTheme_SaveAs = new Command ("Save As...",
			(sender) => saveSyntaxThemeAs ((sender as Widget).IFace as CrowIDE));

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
				MainIdeLogger.Verbosity = value;
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
		static void reloadSyntaxTheme (CrowIDE ide) {
			if (!File.Exists (ide.syntaxThemeFile))
				return;
			ide.SyntaxTheme = new Dictionary<string, TextFormatting> ();
			using (StreamReader sr = new StreamReader(ide.syntaxThemeFile)) {
				while (!sr.EndOfStream) {
					string l = sr.ReadLine ();
					string[] tmp = l.Split ('=');
					ide.SyntaxTheme.Add (tmp[0].Trim (), TextFormatting.Parse (tmp[1].Trim ()));
				}
            }
			ide.NotifyValueChanged ("SyntaxTheme", ide.SyntaxTheme);
			ide.refreshAllEditors ();
		}
		static void saveSyntaxTheme (CrowIDE ide) {
			using (StreamWriter sw = new StreamWriter (ide.syntaxThemeFile)) {
				foreach (string key in ide.SyntaxTheme.Keys) {
					sw.WriteLine ($"{key} = {ide.SyntaxTheme[key]}");
				}
			}
		}
		static void saveSyntaxThemeAs (CrowIDE ide)
		{
			FileDialog fd = ide.LoadIMLFragment<FileDialog> (@"<FileDialog Width='60%' Height='50%' Caption='Save as ...' CurrentDirectory='" +
				ide.SyntaxThemeDirectory + "' SelectedFile='" +
				Path.GetFileName(ide.syntaxThemeFile) + "' OkClicked='saveSyntaxThemeAsDialog_OkClicked'/>");			
			fd.DataSource = ide;
			fd.OkClicked += (sender, e) => {
				FileDialog fd = sender as FileDialog;

				if (string.IsNullOrEmpty (fd.SelectedFileFullPath))
					return;

				if (File.Exists(fd.SelectedFileFullPath)) {
					MessageBox mb = MessageBox.ShowModal (ide, MessageBox.Type.YesNo, "File exists, overwrite?");
					mb.Yes += (sender2, e2) => {
						ide.SyntaxThemeName = Path.GetFileNameWithoutExtension(fd.SelectedFile);
						ide.SyntaxThemeDirectory = fd.SelectedDirectory;
						saveSyntaxTheme (ide);
					};
					return;
				}

				ide.SyntaxThemeName = Path.GetFileNameWithoutExtension(fd.SelectedFile);
				saveSyntaxTheme (ide);
			};
		}

		string SyntaxThemeDirectory {
			get => Configuration.Global.Get<string> ("SyntaxThemeDirectory") ??
					Path.Combine (Path.GetDirectoryName (Assembly.GetEntryAssembly ().Location), "SyntaxThemes");
			set {
				if (SyntaxThemeDirectory == value)
					return;
				Configuration.Global.Set ("SyntaxThemeDirectory", value);				
				NotifyValueChanged ("SyntaxThemeDirectory", (object)SyntaxThemeDirectory);
				NotifyValueChanged ("AvailableSyntaxThemes", (object)AvailableSyntaxThemes);
				reloadSyntaxTheme(this);
			}
		}
		string syntaxThemeFile => Path.Combine (SyntaxThemeDirectory, $"{SyntaxThemeName}.syntax");

		public string[] AvailableSyntaxThemes {
			get {
				string[] tmp = Directory.GetFiles (SyntaxThemeDirectory);
                for (int i = 0; i < tmp.Length; i++)
					tmp[i] = Path.GetFileNameWithoutExtension (tmp[i]);
				return tmp;
            }
        }
		public string SyntaxThemeName {
			get => Configuration.Global.Get<string> ("SyntaxThemeName");
			set {
				if (SyntaxThemeName == value)
					return;
				Configuration.Global.Set ("SyntaxThemeName", value);
				NotifyValueChanged ("SyntaxThemeName", (object)SyntaxThemeName);
				reloadSyntaxTheme (this);
			}
		}

		public bool AutoFoldRegions {
			get => Crow.Configuration.Global.Get<bool> ("AutoFoldRegions");
			set {
				if (AutoFoldRegions == value)
					return;
				Crow.Configuration.Global.Set ("AutoFoldRegions", value);
				NotifyValueChanged (value);
			}
		}
		public bool AutoFoldComments {
			get => Crow.Configuration.Global.Get<bool> ("AutoFoldComments");
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
		public void ProgressTerminate () {
			ProgressVisible = false;
		}
		#endregion

		public Window loadWindow (string path, object dataSource = null){
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
		public void closeWindow (string path){
			Widget g = FindByName (path);
			if (g != null)
				DeleteWidget (g);
		}

		protected void onCommandSave(object sender, MouseButtonEventArgs e){
			System.Diagnostics.Debug.WriteLine("save");
		}
	}
}