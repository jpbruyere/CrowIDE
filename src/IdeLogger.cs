// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Reflection;
using Crow;
using Microsoft.Build.Framework;

namespace Crow.Coding
{
	public class IdeLogger : ILogger
	{
		public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;
		public string Parameters { get; set; }

		CrowIDE ide;
		IEventSource eventSource;

		public IdeLogger (CrowIDE ide)
		{
			this.ide = ide;
		}

		public void Initialize (IEventSource eventSource)
		{
			this.eventSource = eventSource;
			eventSource.AnyEventRaised += EventSource_AnyEventRaised;
		}

		void EventSource_AnyEventRaised (object sender, BuildEventArgs e) {
			if (e is BuildStartedEventArgs)
				ide.BuildEvents.Clear ();

			if (e is BuildMessageEventArgs msg) {
				if (msg.Importance == MessageImportance.Normal) {
					if (Verbosity < LoggerVerbosity.Normal)
						return;
				} else if (msg.Importance == MessageImportance.Low) {
					if (Verbosity < LoggerVerbosity.Diagnostic)
						return;
				}
			}else if (!(e is BuildErrorEventArgs)) {
				if (Verbosity < LoggerVerbosity.Diagnostic) {
					if (e is TaskStartedEventArgs)
						return;
					if (Verbosity < LoggerVerbosity.Detailed) {
						if (e is TargetStartedEventArgs || e is TaskFinishedEventArgs)
							return;
					}
					if (Verbosity < LoggerVerbosity.Normal) {
						if (e is ProjectStartedEventArgs  || e is TargetFinishedEventArgs)
							return;
					}
					if (Verbosity < LoggerVerbosity.Minimal) {
						if (e is ProjectFinishedEventArgs)
							return;
					}
				}
			}

			ide.BuildEvents.Add (e);
		}


		public void Shutdown ()
		{
			eventSource.AnyEventRaised -= EventSource_AnyEventRaised;
		}
	}
}
