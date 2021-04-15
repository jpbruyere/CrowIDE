using System.Reflection;
// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.Linq;
using Crow;
using Microsoft.Build.Framework;

namespace Crow.Coding
{
	public class IdeLogger : ILogger
	{
		IEventSource eventSource;
		LoggerVerbosity verbosity;
		CrowIDE ide;

		public LoggerVerbosity Verbosity {
			get => verbosity;
			set {
				if (verbosity == value)
					return;
				if (eventSource != null)
					unregisterHandles ();
				verbosity = value;
				if (eventSource != null)
					registerHandles ();
			}
		} 
		public string Parameters { get; set; }

		public IdeLogger (CrowIDE ide, LoggerVerbosity verbosity = LoggerVerbosity.Diagnostic)
		{
			this.ide = ide;
			this.verbosity = verbosity;
		}
		public void Initialize (IEventSource eventSource) {
			this.eventSource = eventSource;
			registerHandles ();
		}


		void registerHandles () {
			eventSource.BuildStarted += EventSource_Progress_BuildStarted;
			eventSource.BuildFinished += EventSource_Progress_BuildFinished;
			eventSource.ProjectStarted += EventSource_Progress_ProjectStarted;
			eventSource.ProjectFinished += EventSource_Progress_ProjectFinished;
			eventSource.TaskStarted += EventSource_Progress_TaskStarted;
			eventSource.TaskFinished += EventSource_Progress_TaskFinished;
			eventSource.WarningRaised += EventSource_WarningRaised;
			eventSource.ErrorRaised += EventSource_ErrorRaised;
			eventSource.CustomEventRaised += EventSource_CustomEvent;

			switch (Verbosity) {
			case LoggerVerbosity.Minimal:
				eventSource.MessageRaised += EventSource_MessageRaised_Minimal;
				break;
			case LoggerVerbosity.Normal:
				eventSource.MessageRaised += EventSource_MessageRaised_Normal;
				eventSource.ProjectStarted += EventSource_ProjectStarted;
				eventSource.ProjectFinished += EventSource_ProjectFinished;
				break;
			case LoggerVerbosity.Detailed:
				eventSource.MessageRaised += EventSource_MessageRaised_All;
				eventSource.ProjectStarted += EventSource_ProjectStarted;
				eventSource.ProjectFinished += EventSource_ProjectFinished;
				eventSource.TargetStarted += EventSource_TargetStarted;
				eventSource.TargetFinished += EventSource_TargetFinished;
				eventSource.TaskStarted += EventSource_TaskStarted;
				eventSource.TaskFinished += EventSource_TaskFinished;
				break;
			case LoggerVerbosity.Diagnostic:
				eventSource.AnyEventRaised += EventSource_AnyEventRaised;
				break;
			}
		}

		void unregisterHandles () {
			eventSource.BuildStarted -= EventSource_Progress_BuildStarted;
			eventSource.BuildFinished -= EventSource_Progress_BuildFinished;
			eventSource.ProjectStarted -= EventSource_Progress_ProjectStarted;
			eventSource.ProjectFinished -= EventSource_Progress_ProjectFinished;
			eventSource.TaskStarted -= EventSource_Progress_TaskStarted;
			eventSource.TaskFinished -= EventSource_Progress_TaskFinished;
			eventSource.WarningRaised -= EventSource_WarningRaised;
			eventSource.ErrorRaised -= EventSource_ErrorRaised;
			eventSource.CustomEventRaised -= EventSource_CustomEvent;


			switch (Verbosity) {
			case LoggerVerbosity.Minimal:
				eventSource.MessageRaised -= EventSource_MessageRaised_Minimal;
				break;
			case LoggerVerbosity.Normal:
				eventSource.MessageRaised -= EventSource_MessageRaised_Normal;
				eventSource.ProjectStarted -= EventSource_ProjectStarted;
				eventSource.ProjectFinished -= EventSource_ProjectFinished;
				break;
			case LoggerVerbosity.Detailed:
				eventSource.MessageRaised -= EventSource_MessageRaised_All;
				eventSource.ProjectStarted -= EventSource_ProjectStarted;
				eventSource.ProjectFinished -= EventSource_ProjectFinished;
				eventSource.TargetStarted -= EventSource_TargetStarted;
				eventSource.TargetFinished -= EventSource_TargetFinished;
				eventSource.TaskStarted -= EventSource_TaskStarted;
				eventSource.TaskFinished -= EventSource_TaskFinished;
				break;
			case LoggerVerbosity.Diagnostic:
				eventSource.AnyEventRaised += EventSource_AnyEventRaised;
				break;
			}

		}
		private void EventSource_CustomEvent (object sender, CustomBuildEventArgs e) {
			if (!ide.CurrentSolution.TryGetProjectFromFullPath(e.Message, out ProjectView prj))
				return;
			Type cbeaType = e.GetType();
			FieldInfo fi = cbeaType.GetField("ResolvedReferences");
			prj.ResolvedReferences = fi.GetValue(e) as ITaskItem[];
		}
        private void EventSource_TaskFinished (object sender, TaskFinishedEventArgs e) {
			ide.BuildEvents.Add (e);
		}

		private void EventSource_TaskStarted (object sender, TaskStartedEventArgs e) {
			ide.BuildEvents.Add (e);
		}

		private void EventSource_TargetFinished (object sender, TargetFinishedEventArgs e) {			
			ide.BuildEvents.Add (e);
		}

		private void EventSource_TargetStarted (object sender, TargetStartedEventArgs e) {
			ide.BuildEvents.Add (e);
		}
		private void EventSource_MessageRaised (object sender, BuildMessageEventArgs e) {
			ide.BuildEvents.Add (e);
		}
        private void EventSource_AnyEventRaised (object sender, BuildEventArgs e) {
			ide.BuildEvents.Add (e);
		}

        private void EventSource_MessageRaised_Minimal (object sender, BuildMessageEventArgs e) {
			if (e.Importance == MessageImportance.High)
				ide.BuildEvents.Add (e);
		}
		private void EventSource_MessageRaised_Normal (object sender, BuildMessageEventArgs e) {
			if (e.Importance != MessageImportance.Low)
				ide.BuildEvents.Add (e);
		}
		private void EventSource_MessageRaised_All (object sender, BuildMessageEventArgs e) {			
			ide.BuildEvents.Add (e);
		}
		void EventSource_Progress_BuildStarted (object sender, BuildStartedEventArgs e)
		{
			ide.BuildEvents.Clear ();
			ide.ProgressInit (1000, "Build starting");
			ide.BuildEvents.Add (e);
		}
		void EventSource_Progress_BuildFinished (object sender, BuildFinishedEventArgs e)
		{
			ide.BuildEvents.Add (e);
			ide.ProgressTerminate();
			ide.CurrentSolution.RaiseDiagnosticsValueChanged();
		}		
		void EventSource_Progress_ProjectStarted (object sender, ProjectStartedEventArgs e)
		{
			ide.ProgressNotify (1, $"{e.ProjectFile} [{e.TargetNames}]");
		}
		void EventSource_Progress_ProjectFinished (object sender, ProjectFinishedEventArgs e)
		{
			ide.ProgressNotify (10);
		}
        private void EventSource_Progress_TaskFinished (object sender, TaskFinishedEventArgs e) {
			ide.ProgressNotify (1);
		}

		private void EventSource_Progress_TaskStarted (object sender, TaskStartedEventArgs e) {
			ide.ProgressNotify (1);
		}


		void EventSource_ProjectStarted (object sender, ProjectStartedEventArgs e)
		{
			ide.BuildEvents.Add (e);
		}
		void EventSource_ProjectFinished (object sender, ProjectFinishedEventArgs e)
		{
			ide.BuildEvents.Add (e);
		}
		void EventSource_ErrorRaised (object sender, BuildErrorEventArgs e)
		{
			ide.BuildEvents.Add (e);
		}
		private void EventSource_WarningRaised (object sender, BuildWarningEventArgs e) {
			ide.BuildEvents.Add (e);
		}

		public void Shutdown ()
		{
			if (eventSource != null)
				unregisterHandles ();
		}
	}
}
