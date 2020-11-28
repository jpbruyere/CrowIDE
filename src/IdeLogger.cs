// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Crow;
using Microsoft.Build.Framework;

namespace Crow.Coding
{
	public class IdeLogger : ILogger
	{
		IEventSource eventSource;
		LoggerVerbosity verbosity = LoggerVerbosity.Quiet;
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

		public IdeLogger (CrowIDE ide)
		{
			this.ide = ide;
		}

		void registerHandles () {
			eventSource.BuildStarted += EventSource_BuildStarted; ;

			switch (Verbosity) {
			case LoggerVerbosity.Quiet:
				eventSource.BuildFinished += EventSource_BuildFinished;
				break;
			case LoggerVerbosity.Minimal:
                eventSource.MessageRaised += EventSource_MessageRaised_Minimal;
				break;
			case LoggerVerbosity.Normal:
				eventSource.MessageRaised += EventSource_MessageRaised_Normal;
				eventSource.ProjectStarted += EventSource_ProjectStarted;
				eventSource.ProjectFinished += EventSource_ProjectFinished;
				break;
			case LoggerVerbosity.Detailed:
				break;
			case LoggerVerbosity.Diagnostic:
                eventSource.AnyEventRaised += EventSource_AnyEventRaised;
                eventSource.MessageRaised += EventSource_MessageRaised;
				break;
			}

			eventSource.BuildFinished += EventSource_BuildFinished;

			eventSource.ErrorRaised += EventSource_ErrorRaised;
		}
		void unregisterHandles () {
			eventSource.BuildStarted -= EventSource_BuildStarted; ;

			switch (Verbosity) {
			case LoggerVerbosity.Quiet:
				eventSource.BuildFinished -= EventSource_BuildFinished;
				break;
			case LoggerVerbosity.Minimal:
				eventSource.MessageRaised -= EventSource_MessageRaised_Minimal;
				break;
			case LoggerVerbosity.Normal:
				eventSource.MessageRaised -= EventSource_MessageRaised_Normal;
				eventSource.ProjectStarted -= EventSource_ProjectStarted;
				eventSource.ProjectFinished -= EventSource_ProjectFinished;
				break;
			case LoggerVerbosity.Detailed:
				break;
			case LoggerVerbosity.Diagnostic:
				eventSource.AnyEventRaised -= EventSource_AnyEventRaised;
				eventSource.MessageRaised -= EventSource_MessageRaised;
				break;
			}

			eventSource.BuildFinished -= EventSource_BuildFinished;

			eventSource.ErrorRaised -= EventSource_ErrorRaised;
		}

		public void Initialize (IEventSource eventSource) {
			this.eventSource = eventSource;
			registerHandles ();
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
		void EventSource_BuildStarted (object sender, BuildStartedEventArgs e)
		{
			ide.BuildEvents.Clear ();
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



		public void Shutdown ()
		{
			if (eventSource != null)
				unregisterHandles ();
		}
	}
}
