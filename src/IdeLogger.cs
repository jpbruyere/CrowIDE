// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
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
			if (Verbosity == LoggerVerbosity.Diagnostic) {
				eventSource.AnyEventRaised += EventSource_AnyEventRaised;
				return;
			}

			eventSource.BuildStarted += EventSource_BuildStarted;
			eventSource.BuildFinished += EventSource_BuildFinished;

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
			}
			eventSource.WarningRaised += EventSource_WarningRaised;
			eventSource.ErrorRaised += EventSource_ErrorRaised;
		}

		void unregisterHandles () {
			if (Verbosity == LoggerVerbosity.Diagnostic) {
				eventSource.AnyEventRaised -= EventSource_AnyEventRaised;
				return;
			}

			eventSource.BuildStarted -= EventSource_BuildStarted;
			eventSource.BuildFinished -= EventSource_BuildFinished;

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
			}
			eventSource.WarningRaised -= EventSource_WarningRaised;
			eventSource.ErrorRaised -= EventSource_ErrorRaised;
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
		void EventSource_BuildStarted (object sender, BuildStartedEventArgs e)
		{
			ide.BuildEvents.Clear ();
			if (Verbosity > LoggerVerbosity.Detailed)
				return;
			ide.BuildEvents.Add (e);
		}
		void EventSource_BuildFinished (object sender, BuildFinishedEventArgs e)
		{
			ide.BuildEvents.Add (e);
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
