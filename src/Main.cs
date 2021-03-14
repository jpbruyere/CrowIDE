// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Crow.Coding
{
	public static class Startup
	{

#if NETCOREAPP
		static IntPtr resolveUnmanaged (Assembly assembly, String libraryName) {

			switch (libraryName) {
			case "glfw3":
				return NativeLibrary.Load ("glfw", assembly, null);
			case "rsvg-2.40":
				return NativeLibrary.Load ("rsvg-2", assembly, null);
			}
			Console.WriteLine ($"[UNRESOLVE] {assembly} {libraryName}");
			return IntPtr.Zero;
		}

		static Startup () {
			System.Runtime.Loader.AssemblyLoadContext.Default.ResolvingUnmanagedDll += resolveUnmanaged;
		}
#endif             //public static string SDKFolder = "/usr/lib/mono/msbuild/Current/bin/";
		//public static string MSBuildRoot = SDKFolder;

		public static string SDKFolder;
		public static string MSBuildRoot;

		[STAThread]
		static void Main ()
		{
			Color c = new Color (0xff + 1);

			DbgLogger.IncludeEvents = DbgEvtType.ActiveWidget;
			DbgLogger.DiscardEvents = 0;

			configureDefaultSDKPathes ();

			Environment.SetEnvironmentVariable ("MSBUILD_EXE_PATH", Path.Combine (MSBuildRoot, "MSBuild.dll"));
			Environment.SetEnvironmentVariable ("MSBuildSDKsPath", Path.Combine (MSBuildRoot, "Sdks"));

			if (Environment.OSVersion.Platform == PlatformID.Unix)
				Environment.SetEnvironmentVariable ("FrameworkPathOverride", "/usr/lib/mono/4.5/");

			//Environment.SetEnvironmentVariable ("MSBuildExtensionsPath", MSBuildRoot);
			/*Environment.SetEnvironmentVariable ("MSBuildToolsPath", @"C:\Program Files\dotnet\sdk\5.0.100");
			Environment.SetEnvironmentVariable ("MSBuildBinPath", @"C:\Program Files\dotnet\sdk\5.0.100");*/

			AppDomain currentDomain = AppDomain.CurrentDomain;
			currentDomain.AssemblyResolve += msbuildAssembliesResolve;

/*#if NETCOREAPP
			NativeLibrary.SetDllImportResolver (Assembly.GetAssembly(typeof(Glfw.Glfw3)),
				(libraryName, assembly, searchPath) => NativeLibrary.Load (libraryName == "glfw3" ?
					Environment.OSVersion.Platform == PlatformID.Unix ? "glfw" : "glfw3" : libraryName, assembly, searchPath));
#endif*/
			start ();

		}
		static void start()
		{

/*#if NET472
			var nativeSharedMethod = typeof (Microsoft.Build.Construction.SolutionFile).Assembly.GetType ("Crow.Build.Shared.NativeMethodsShared");
			var isMonoField = nativeSharedMethod.GetField ("_isMono", BindingFlags.Static | BindingFlags.NonPublic);
			isMonoField.SetValue (null, true);
#endif*/

			using (CrowIDE app = new CrowIDE ()) {
				app.Run ();
				app.saveWinConfigs ();
			}

		}
		static Assembly msbuildAssembliesResolve (object sender, ResolveEventArgs args)
		{
			string assemblyPath = Path.Combine (MSBuildRoot, new AssemblyName (args.Name).Name + ".dll");
			if (!File.Exists (assemblyPath)) return null;
			Assembly assembly = Assembly.LoadFrom (assemblyPath);
			return assembly;
		}

		static void configureDefaultSDKPathes ()
		{
			SDKFolder = Configuration.Global.Get<string> ("SDKFolder");
			if (string.IsNullOrEmpty (SDKFolder)) {
				switch (Environment.OSVersion.Platform) {
				case PlatformID.Win32S:
				case PlatformID.Win32Windows:
				case PlatformID.Win32NT:
				case PlatformID.WinCE:
					SDKFolder = @"C:\Program Files\dotnet\sdk\";
					break;
				case PlatformID.Unix:
					SDKFolder = @"/usr/share/dotnet/sdk";
					break;
				default:
					throw new NotSupportedException ();
				}
				Configuration.Global.Set ("SDKFolder", SDKFolder);
			}

			MSBuildRoot = Configuration.Global.Get<string> ("MSBuildRoot");
			if (!string.IsNullOrEmpty (MSBuildRoot) && Directory.Exists(MSBuildRoot))
				return;

			List<SDKVersion> versions = new List<SDKVersion> ();
			foreach (string dir in Directory.EnumerateDirectories (SDKFolder)) {
				string dirName = Path.GetFileName (dir);
				if (SDKVersion.TryParse (dirName, out SDKVersion vers))
					versions.Add (vers);
			}
			versions.Sort ((a, b) => a.ToInt.CompareTo (b.ToInt));
			MSBuildRoot = versions.Count > 0 ? Path.Combine (SDKFolder, versions.Last ().ToString ()) : SDKFolder;
			Configuration.Global.Set ("MSBuildRoot", MSBuildRoot);
		}
	}
	public class SDKVersion
	{
		public int major, minor, revision;
		public static bool TryParse (string versionString, out SDKVersion version) {
			version = null;
			if (string.IsNullOrEmpty (versionString))
				return false;
			string [] verNums = versionString.Split ('.');
			if (verNums.Length != 3)
				return false;
			if (!int.TryParse (verNums [0], out int maj))
				return false;
			if (!int.TryParse (verNums [1], out int min))
				return false;
			if (!int.TryParse (verNums [2], out int rev))
				return false;
			version = new SDKVersion { major = maj, minor = min, revision = rev };
			return true;
		}
		public long ToInt => major << 62 + minor << 60 + revision;
		public override string ToString () => $"{major}.{minor}.{revision}";
	}
}
