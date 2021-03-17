// Copyright (c) 2013-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Threading;
using System.Xml.Serialization;

namespace Crow.Coding
{
	public abstract class Editor : ScrollingObject
	{
		public CommandGroup FileCommands;

		public CommandGroup EditCommands;
		public Command CMDUndo, CMDRedo, CMDCut, CMDCopy, CMDPaste;

		void initCommands () {
			CMDUndo = new Command (new Action (undo)) { Caption = "Undo", Icon = CrowIDE.IcoUndo, CanExecute = false };
			CMDRedo = new Command (new Action (redo)) { Caption = "Redo", Icon = CrowIDE.IcoRedo, CanExecute = false };
			CMDCut = new Command (new Action (cut)) { Caption = "Cut", Icon = CrowIDE.IcoCut, CanExecute = false };
			CMDCopy = new Command (new Action (copy)) { Caption = "Copy", Icon = CrowIDE.IcoCopy, CanExecute = false };
			CMDPaste = new Command (new Action (paste)) { Caption = "Paste", Icon = CrowIDE.IcoPaste, CanExecute = false };
		}

		#region CTOR
		protected Editor () : base () {
			initCommands ();
			Thread t = new Thread (backgroundThreadFunc);
			t.IsBackground = true;
			t.Start ();
		}
		#endregion

		protected ReaderWriterLockSlim editorMutex = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
		protected ProjectFileNode projFile = null;
		Exception error = null;

		public virtual ProjectFileNode ProjectNode
		{
			get { return projFile; }
			set
			{
				if (projFile == value)
					return;

				if (projFile != null) {
					projFile.UnregisterEditor (this);
					ContextCommands = null;
				}

				projFile = value;

				if (projFile != null) {
					projFile.RegisterEditor (this);
					ContextCommands = projFile.Commands;
				}

				NotifyValueChanged ("ProjectNode", projFile);
			}
		}
		[XmlIgnore]public Exception Error {
			get { return error; }
			set {
				if (error == value)
					return;
				error = value;
				NotifyValueChanged ("Error", error);
				NotifyValueChanged ("HasError", HasError);
			}
		}
		[XmlIgnore]public bool HasError {
			get { return error != null; }
		}

		protected abstract void updateEditorFromProjFile ();
		protected abstract void updateProjFileFromEditor ();
		protected abstract bool EditorIsDirty { get; set; }
		protected virtual bool IsReady { get => true; }
		protected virtual void updateCheckPostProcess () {}

		protected void backgroundThreadFunc () {
			while (true) {
				if (IsReady) {
					if (Monitor.TryEnter (IFace.UpdateMutex)) {
						if (!projFile.RegisteredEditors [this]) {
							updateEditorFromProjFile ();
							projFile.RegisteredEditors[this] = true;
						} else if (EditorIsDirty) {
							updateProjFileFromEditor ();
							EditorIsDirty = false;
						}
						updateCheckPostProcess ();
						Monitor.Exit (IFace.UpdateMutex);
					}
				}
				Thread.Sleep (100);
			}	
		}

        protected override void onFocused (object sender, EventArgs e) {
            base.onFocused (sender, e);
			(IFace as CrowIDE).CurrentEditor = this;
        }

		protected abstract void undo ();
		protected abstract void redo ();
		protected abstract void cut ();
		protected abstract void copy ();
		protected abstract void paste ();
	}
}

