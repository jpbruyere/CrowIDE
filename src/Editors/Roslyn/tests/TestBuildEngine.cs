using System;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace Crow.Coding
{
    public class MyBuildEngine : IBuildEngine
    {
       /* public static void Test () {
            ResolveAssemblyReference resolve = new ResolveAssemblyReference ();
            resolve.BuildEngine = new MyBuildEngine ();
            resolve. = new TaskItem[]{
               new TaskItem(@"C:\Code\Projects\MSBuildTest\HelloWorld\HelloWorld.cs")
            };

            if (resolve.Execute ()) {
                Console.WriteLine ("Task executed ok. Resulting assembly: {0}",
                   resolve.OutputAssembly
                );
            }
        }*/
        public bool BuildProjectFile (string projectFileName, string[] targetNames,
                                     IDictionary globalProperties,
                                     IDictionary targetOutputs) {
            throw new NotImplementedException ();
        }

        public int ColumnNumberOfTaskNode {
            get { return 0; }
        }

        public bool ContinueOnError {
            get { return false; }
        }

        public int LineNumberOfTaskNode {
            get { return 0; }
        }

        public string ProjectFileOfTaskNode {
            get { return ""; }
        }

        public void LogCustomEvent (CustomBuildEventArgs e) {
            Console.WriteLine ("Custom: {0}", e.Message);
        }

        public void LogErrorEvent (BuildErrorEventArgs e) {
            Console.WriteLine ("Error: {0}", e.Message);
        }

        public void LogMessageEvent (BuildMessageEventArgs e) {
            Console.WriteLine ("Message: {0}", e.Message);
        }

        public void LogWarningEvent (BuildWarningEventArgs e) {
            Console.WriteLine ("Warning: {0}", e.Message);
        }
    }
}