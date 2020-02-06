﻿// Copyright (c) 2016-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.IO;
using System.Reflection;
using Crow.IML;

namespace Crow.Coding
{
	class CrowIDE : Interface
	{
		public static string sdkFolder = "/usr/share/dotnet/sdk";
		public static string msbuildRoot = Path.Combine (sdkFolder, "3.1.101/");
		//static string sdkFolder = "/usr/lib/mono/msbuild/Current/bin/";
		//static string msbuildRoot = sdkFolder;
		public static string toolsVersion = "Current";

		static CrowIDE ()
		{
			Interface.UPDATE_INTERVAL = 20;

			Environment.SetEnvironmentVariable ("MSBUILD_EXE_PATH", Path.Combine (msbuildRoot, "MSBuild.dll"));
			Environment.SetEnvironmentVariable ("MSBUILD_NUGET_PATH", "/home/jp/.nuget/packages");

			Environment.SetEnvironmentVariable ("FrameworkPathOverride", "/usr/lib/mono/4.5/");

			AppDomain currentDomain = AppDomain.CurrentDomain;
			currentDomain.AssemblyResolve += msbuildAssembliesResolve;

			//foreach (string s in Assembly.GetEntryAssembly ().GetManifestResourceNames ())
				//Console.WriteLine (s);
		}

		static Assembly msbuildAssembliesResolve (object sender, ResolveEventArgs args)
		{
			string assemblyPath = Path.Combine (msbuildRoot, new AssemblyName (args.Name).Name + ".dll");
			if (!File.Exists (assemblyPath)) return null;
			Assembly assembly = Assembly.LoadFrom (assemblyPath);
			return assembly;
		}

		public Command CMDNew, CMDOpen, CMDSave, CMDSaveAs, cmdCloseSolution, CMDQuit,
		CMDUndo, CMDRedo, CMDCut, CMDCopy, CMDPaste, CMDHelp, CMDAbout, CMDOptions,
		CMDViewGTExp, CMDViewProps, CMDViewProj, CMDViewProjProps, CMDViewErrors, CMDViewLog, CMDViewSolution, CMDViewEditor, CMDViewProperties,
		CMDViewToolbox, CMDViewSchema, CMDViewStyling,CMDViewDesign,
		CMDBuild, CMDClean, CMDRestore;

		void initCommands () {
			CMDNew = new Command(new Action(() => newFile())) { Caption = "New", Icon = new SvgPicture("#Icons.blank-file.svg"), CanExecute = true};
			CMDOpen = new Command(new Action(() => openFileDialog())) { Caption = "Open...", Icon = new SvgPicture("#Icons.open.svg") };
			CMDSave = new Command(new Action(() => saveFileDialog())) { Caption = "Save", Icon = new SvgPicture("#Icons.save.svg"), CanExecute = false};
			CMDSaveAs = new Command(new Action(() => saveFileDialog())) { Caption = "Save As...", Icon = new SvgPicture("#Icons.save.svg"), CanExecute = false};
			CMDQuit = new Command(new Action(() => app.running = false)) { Caption = "Quit", Icon = new SvgPicture("#Icons.sign-out.svg") };
			CMDUndo = new Command(new Action(() => undo())) { Caption = "Undo", Icon = new SvgPicture("#Icons.undo.svg"), CanExecute = false};
			CMDRedo = new Command(new Action(() => redo())) { Caption = "Redo", Icon = new SvgPicture("#Icons.redo.svg"), CanExecute = false};
            CMDCut = new Command(new Action(() => cut())) { Caption = "Cut", Icon = new SvgPicture("#Icons.scissors.svg"), CanExecute = false};
            CMDCopy = new Command(new Action(() => copy())) { Caption = "Copy", Icon = new SvgPicture("#Icons.copy-file.svg"), CanExecute = false};
            CMDPaste = new Command(new Action(() => paste())) { Caption = "Paste", Icon = new SvgPicture("#Icons.paste-on-document.svg"), CanExecute = false};
            CMDHelp = new Command(new Action(() => System.Diagnostics.Debug.WriteLine("help"))) { Caption = "Help", Icon = new SvgPicture("#Icons.question.svg") };
			CMDOptions = new Command(new Action(() => loadWindow("#CrowIDE.ui.Options.crow", this))) { Caption = "Editor Options", Icon = new SvgPicture("#Icons.tools.svg") };

			cmdCloseSolution = new Command(new Action(() => closeSolution()))
			{ Caption = "Close Solution", Icon = new SvgPicture("#Icons.paste-on-document.svg"), CanExecute = false};

			CMDViewErrors = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winErrors.crow",this)))
			{ Caption = "Errors pane"};
			CMDViewLog = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winLog.crow",this)))
			{ Caption = "Log View"};
			CMDViewSolution = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winSolution.crow",this)))
			{ Caption = "Solution Tree", CanExecute = false};
			CMDViewEditor = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winEditor.crow",this)))
			{ Caption = "Editor Pane"};
			CMDViewProperties = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winProperties.crow",this)))
			{ Caption = "Properties"};
			CMDViewDesign = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winDesign.crow",this)))
			{ Caption = "Quick Design", CanExecute = true};
			CMDViewToolbox = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winToolbox.crow",this)))
			{ Caption = "Toolbox", CanExecute = false};
			CMDViewSchema = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winSchema.crow",this)))
			{ Caption = "IML Shematic View", CanExecute = true};
			CMDViewStyling = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winStyleView.crow",this)))
			{ Caption = "Styling Explorer", CanExecute = true};
				
			CMDViewGTExp = new Command(new Action(() => loadWindow ("#CrowIDE.ui.DockWindows.winGTExplorer.crow",this)))
			{ Caption = "Graphic Tree Explorer", CanExecute = true};
			CMDBuild = new Command(new Action(() => CurrentSolution?.Build ("Build")))
			{ Caption = "Compile Solution", CanExecute = false};
			CMDClean = new Command(new Action(() => CurrentSolution?.Build ("Clean")))
			{ Caption = "Clean Solution", CanExecute = false};
			CMDRestore = new Command(new Action(() => CurrentSolution?.Build ("Restore")))
			{ Caption = "Restore packages", CanExecute = false};
			CMDViewProjProps = new Command(new Action(loadProjProps))
			{ Caption = "Project Properties", CanExecute = false};
		}

		void openFileDialog () {			
			AddWidget (instFileDlg.CreateInstance()).DataSource = this;
		}
		void openOptionsDialog(){}
		void newFile() {			
			currentSolution.OpenedItems.Add(new ProjectFile());
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

		static CrowIDE app;
		[STAThread]
		static void Main ()
		{
			using (app = new CrowIDE ()) {
				MainIFace = app;
				//app.Keyboard.KeyDown += App_KeyboardKeyDown;
				app.Run ();

				app.saveWinConfigs ();
			}
		}
		protected override void Startup ()
		{
			initIde ();
			reloadWinConfigs ();
		}

		static void App_KeyboardKeyDown (object sender, KeyEventArgs e)
		{
			Console.WriteLine((byte)e.Key);
			//#if DEBUG_LOG
			/*switch (e.Key) {
			case Key.F2:				
				DebugLog.save (app);
				break;
			}*/
			//#endif
		}

		public CrowIDE ()
			: base(1024, 800)
		{
		}

		Instantiator instFileDlg;
		SolutionView currentSolution;
		ProjectView currentProject;
		DockStack mainDock;

		public static Interface MainIFace;
		public static CrowIDE MainWin;

		void initIde() {

			initCommands ();

			Widget go = Load (@"#CrowIDE.ui.CrowIDE.crow");
			go.DataSource = this;

			mainDock = go.FindByName ("mainDock") as DockStack;

			if (ReopenLastSolution && !string.IsNullOrEmpty (LastOpenSolution)) {
				CurrentSolution = new SolutionView (LastOpenSolution);
				lock(MainIFace.UpdateMutex)
					CurrentSolution.ReopenItemsSavedInUserConfig ();
			}

			instFileDlg = Instantiator.CreateFromImlFragment
				(MainIFace, "<FileDialog Caption='Open File' CurrentDirectory='{²CurrentDirectory}' SearchPattern='*.sln' OkClicked='onFileOpen'/>");

			/*DockWindow dw = loadWindow ("#CrowIDE.ui.DockWindows.winEditor.crow", this) as DockWindow;
			dw.DockingPosition = Alignment.Center;
			dw.Dock (mainDock);
			dw = loadWindow ("#CrowIDE.ui.DockWindows.winSolution.crow", this) as DockWindow;
			dw.DockingPosition = Alignment.Right;
			dw.Dock (mainDock);
			dw = loadWindow ("#CrowIDE.ui.DockWindows.winToolbox.crow", this) as DockWindow;
			dw.DockingPosition = Alignment.Left;
			dw.Dock (mainDock);*/

			//Console.WriteLine ();
		}

		void loadProjProps () {
			loadWindow ("#CrowIDE.ui.ProjectProperties.crow");
		}

		public string CurrentDirectory {
			get { return Crow.Configuration.Global.Get<string>("CurrentDirectory");}
			set {
				Crow.Configuration.Global.Set ("CurrentDirectory", value);
			}
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
				
				lock (MainIFace) {
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
				
				lock (MainIFace) {
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

		public void onFileOpen (object sender, EventArgs e)
		{
			FileDialog fd = sender as FileDialog;

			string filePath = fd.SelectedFileFullPath;

			try {
				string ext = Path.GetExtension (filePath);
				if (string.Equals (ext, ".sln", StringComparison.InvariantCultureIgnoreCase)) {					
					CurrentSolution = new SolutionView (filePath);
					LastOpenSolution = filePath;
//				}else if (string.Equals (ext, ".csproj", StringComparison.InvariantCultureIgnoreCase)) {
//					currentProject = new Project (filePath);
				}
			} catch (Exception ex) {
				LoadIMLFragment ("<MessageBox Message='"+ ex.Message + "\n" + "' MsgType='Error'/>");	
			}				
		}

		Window loadWindow(string path, object dataSource = null){
			try {
				Widget g = MainIFace.FindByName (path);
				if (g != null)
					return g as Window;
				g = MainIFace.Load (path);
				g.Name = path;
				g.DataSource = dataSource;
				return g as Window;
			} catch (Exception ex) {
				Console.WriteLine (ex.ToString ());
			}
			return null;
		}
		void closeWindow (string path){
			Widget g = MainIFace.FindByName (path);
			if (g != null)
				MainIFace.DeleteWidget (g);
		}

		protected void onCommandSave(object sender, MouseButtonEventArgs e){
			System.Diagnostics.Debug.WriteLine("save");
		}

		void actionOpenFile(){
			System.Diagnostics.Debug.WriteLine ("OpenFile action");
		}
	}
}