using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Crow.Coding
{
    public class NetcoredbgDebugger : Debugger { 

        enum MIAttributeType
        {
            Value,
            Tuple,
            List
        }
        ref struct MIAttribute
        {
            public MIAttributeType Type;
            public ReadOnlySpan<char> Name;
            public ReadOnlySpan<char> Value;
        }
        
        Process procdbg;
        Queue<string> pendingRequest = new Queue<string> ();
        CSProjectItem executingFile;
        int executingLine = -1;


        public override ProjectView Project {
            get => base.Project;
            set {
                if (base.Project == value)
                    return;
                base.Project = value;
                CreateNewRequest ($"-file-exec-and-symbols {project.OutputAssembly}");
                CreateNewRequest ($"-environment-cd {Path.GetDirectoryName (project.OutputAssembly)}");
            }
        }

        #region CTOR
        public NetcoredbgDebugger (ProjectView project) {
            
            procdbg = new Process ();
            procdbg.StartInfo.FileName = project.solution.IDE.NetcoredbgPath;
            procdbg.StartInfo.Arguments = "--interpreter=mi";
            procdbg.StartInfo.CreateNoWindow = true;
            procdbg.StartInfo.RedirectStandardInput = true;
            procdbg.StartInfo.RedirectStandardOutput = true;
            procdbg.StartInfo.RedirectStandardError = true;

            procdbg.EnableRaisingEvents = true;
            procdbg.OutputDataReceived += Procdbg_OutputDataReceived;
            procdbg.ErrorDataReceived += Procdbg_ErrorDataReceived;
            procdbg.Exited += Procdbg_Exited;

            bool result = procdbg.Start ();

            procdbg.BeginOutputReadLine ();

            Project = project;

            foreach (BreakPoint bp in breakPoints.Where (b => b.IsEnabled))
                InsertBreakPoint (bp);

            breakPoints.ListAdd += BreakPoints_ListAdd;
            breakPoints.ListRemove += BreakPoints_ListRemove;

        }
        #endregion

        protected override void ResetCurrentExecutingLocation () {
            if (executingFile == null)
                return;
            executingFile.ExecutingLine = -1;
            executingFile = null;
        }
        /// <summary>
        /// send request on netcoredbg process stdin
        /// </summary>
        void sendRequest (string request) {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine ($"<- {request}");
            Console.ResetColor ();
            procdbg.StandardInput.WriteLine (request);
        }

        /// <summary>
        /// enqueue new request, send it if no other request is pending
        /// </summary>
        void CreateNewRequest (string request) {
            lock (pendingRequest) {
                pendingRequest.Enqueue (request);
                if (pendingRequest.Count == 1)
                    sendRequest (request);
            }
        }

        #region Debugger abstract class implementation
        public override void Start () {
            CurrentState = Status.Starting;
            CreateNewRequest ($"-exec-run");
        }
        public override void Pause () {
            CreateNewRequest ($"-exec-interrupt");
        }
        public override void Continue () {
            CreateNewRequest ($"-exec-continue");
        }
        public override void Stop () {
            CreateNewRequest ($"-exec-abort");
        }

        public override void StepIn () {
            CreateNewRequest ($"-exec-step");
        }
        public override void StepOver () {
            CreateNewRequest ($"-exec-next");
        }
        public override void StepOut () {
            CreateNewRequest ($"-exec-finish");
        }

        public override void InsertBreakPoint (BreakPoint bp) {
            CreateNewRequest ($"-break-insert {bp.File.FullPath}:{bp.Line + 1}");
        }
        public override void DeleteBreakPoint (int breakPointIndex) {
            CreateNewRequest ($"-break-delete {breakPointIndex}");
        }
        #endregion

        private void BreakPoints_ListRemove (object sender, ListChangedEventArg e) {
            DeleteBreakPoint (e.Index);            
        }
        private void BreakPoints_ListAdd (object sender, ListChangedEventArg e) {
            InsertBreakPoint ((BreakPoint)e.Element);
        }
        private void Procdbg_Exited (object sender, EventArgs e) {
            Console.WriteLine ("GDB process exited");
            CurrentState = Status.Init;

            breakPoints.ListAdd -= BreakPoints_ListAdd;
            breakPoints.ListRemove -= BreakPoints_ListRemove;
            procdbg.Dispose ();
            procdbg = null;


            CreateNewRequest ($"-gdb-exit");
        }
        private void Procdbg_ErrorDataReceived (object sender, DataReceivedEventArgs e) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine ($"-> Error: {e.Data}");
            Console.ResetColor ();
        }
        bool TryGetNextMIAttributes (ref ReadOnlySpan<char> remaining_datas, out MIAttribute attribute) {
            attribute = new MIAttribute ();
            int idx = remaining_datas.IndexOf ('=');
            if (idx < 0)
                return false;
            attribute.Name = remaining_datas.Slice (0, idx);
            remaining_datas = remaining_datas.Slice (idx + 1);
            if (remaining_datas[0] == '{') {
                attribute.Type = MIAttributeType.Tuple;
                int parenths = 1;
                ReadOnlySpan<char> value = remaining_datas.Slice (1);

                do {
                    idx = value.IndexOfAny ('{', '}');
                    if (idx < 0)
                        throw new Exception ("mi parsing error");
                    if (value[idx] == '{')
                        parenths++;
                    else
                        parenths--;
                    value = value.Slice (idx + 1);
                } while (parenths > 0);
                if (value.Length == 0)
                    idx = -1;
                else if (value[0] != ',')
                    throw new Exception ("mi parsing error, expecting ','");
                else
                    idx = remaining_datas.Length - value.Length + 1;
            } else if (remaining_datas[0] == '"') {
                attribute.Type = MIAttributeType.Value;

                ReadOnlySpan<char> value = remaining_datas.Slice (1);
                int quotes = 0;
                while (true) {
                    idx = value.IndexOf ('"');
                    if (idx < 0)
                        throw new Exception ("mi parsing error: expecting quote");
                    if (value[idx-1] == '\\') {
                        quotes++;
                    }else if (quotes % 2 > 0)
                        throw new Exception ("mi parsing error: wrong number of escaped quotes in c-string");
                    else {
                        value = value.Slice (idx + 1);
                        break;
                    }                    
                    value = value.Slice (idx + 1);
                }
                if (value.Length == 0)
                    idx = -1;
                else if (value[0] != ',')
                    throw new Exception ("mi parsing error, expecting ','");
                else
                    idx = remaining_datas.Length - value.Length;
            }

            if (idx < 0) {
                attribute.Value = remaining_datas;
                remaining_datas = default;
            }else {
                attribute.Value = remaining_datas.Slice (1, idx-2);
                remaining_datas = remaining_datas.Slice (idx + 1);
            }                           
            return true;
        }
        bool TryGetNextMIAttributeValue (ReadOnlySpan<char> attributeName, ref ReadOnlySpan<char> remaining_datas, out ReadOnlySpan<char> value) {
            while(TryGetNextMIAttributes (ref remaining_datas, out MIAttribute attribute)) {
                if (attribute.Name.SequenceEqual (attributeName)) {
                    value = attribute.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private void Procdbg_OutputDataReceived (object sender, DataReceivedEventArgs e) {
            if (string.IsNullOrEmpty (e.Data))
                return;

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine ($"-> {e.Data}");
            Console.ResetColor ();

            char firstChar = e.Data[0];
            ReadOnlySpan<char> data = e.Data.AsSpan (1);

            if (firstChar == '(') {
                return;
            }
            

            int tokEnd = data.IndexOf (',');

            ReadOnlySpan<char> data_id = tokEnd < 0 ? data : data.Slice (0, tokEnd);
            if (tokEnd >= 0)
                data = data.Slice (tokEnd + 1);

            if (firstChar == '^') {
                string request = null;
                lock (pendingRequest) {
                    if (pendingRequest.Count > 0) {
                        request = pendingRequest.Dequeue ();
                        if (pendingRequest.Count > 0)
                            sendRequest (pendingRequest.Peek ());
                    }
                }
                
                if (data_id.SequenceEqual ("running")) {
                    CurrentState = Status.Running;
                } else if (data_id.SequenceEqual ("done")) {
                    Console.WriteLine ($"=> request done: {request}");

                } else if (data_id.SequenceEqual ("exit")) {
                    Console.WriteLine ($"=> request done: {request}");
                } else
                    print_unknown_datas ($"requested: {request} data:{e.Data}");
                Console.ResetColor ();

            } else if (firstChar == '*') {
                if (data_id.SequenceEqual ("stopped")) {
                    CurrentState = Status.Stopped;
                    TryGetNextMIAttributeValue ("reason", ref data, out ReadOnlySpan<char> reason);
                    if (reason.SequenceEqual ("exited")) {
                        CurrentState = Status.Exited;
                        TryGetNextMIAttributeValue ("exit-code", ref data, out ReadOnlySpan<char> exit_code);                        
                    } else if (reason.SequenceEqual ("entry-point-hit") && !BreakOnStartup) {
                        Continue ();
                    } else {
                        Console.WriteLine ($"Stopped reason:{reason.ToString ()}");
                        TryGetNextMIAttributeValue ("frame", ref data, out ReadOnlySpan<char> frame);
                        TryGetNextMIAttributeValue ("fullname", ref frame, out ReadOnlySpan<char> fullname);
                        TryGetNextMIAttributeValue ("line", ref frame, out ReadOnlySpan<char> lineno);

                        executingLine = int.Parse (lineno) - 1;
                        string strPath = fullname.ToString ().Replace ("\\\\", "\\");

                        if (project.TryGetProjectFileFromPath (strPath, out ProjectFileNode pf)) {
                            if (!pf.IsOpened)
                                pf.Open ();
                            pf.IsSelected = true;

                            executingFile = pf as CSProjectItem;
                            executingFile.ExecutingLine = executingLine;
                            executingFile.CurrentLine = executingLine;
                        } else {
                            ResetCurrentExecutingLocation ();
                            print_unknown_datas ($"current executing file ({strPath}) not found.");
                        }
                    }


                } else
                    print_unknown_datas (e.Data);
            } else if (firstChar == '=') {

                if (data_id.SequenceEqual("message")) {
                    TryGetNextMIAttributeValue ("text", ref data, out ReadOnlySpan<char> text);
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine (text.ToString());
                    Console.ResetColor ();

                } else {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine (e.Data);
                    Console.ResetColor ();
                }
            } else { 
                print_unknown_datas (e.Data);
            }
            
        }

        void print_unknown_datas (string data) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine (data);
            Console.ResetColor ();
        }
    }
}
