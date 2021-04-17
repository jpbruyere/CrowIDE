// Copyright (c) 2013-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.CompilerServices;

namespace Crow.Coding.Debugging
{
	public abstract class Debugger : IValueChange
	{
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public void NotifyValueChanged(string MemberName, object _value)
		{
			//Debug.WriteLine ("Value changed: {0}->{1} = {2}", this, MemberName, _value);
			ValueChanged.Raise(this, new ValueChangeEventArgs(MemberName, _value));
		}
		public void NotifyValueChanged(object _value, [CallerMemberName] string caller = null)
		{
			NotifyValueChanged(caller, _value);
		}
		#endregion

		public enum Status
		{
			/// <summary>netcoredbg process created, project loaded, breakpoints requested</summary>
			Init,
			/// <summary>exec-run sent</summary>
			Starting,
			/// <summary>running state received</summary>
			Running,
			/// <summary>stopped event received</summary>
			Stopped,
			/// <summary>debugged program exited</summary>
			Exited,
		}

		protected ProjectView project;
		protected SolutionView solution => project.Solution;

		protected ObservableList<BreakPoint> BreakPoints => project.Solution.BreakPoints;

		Status currentState = Status.Init;
		bool breakOnStartup = false;

		public Status CurrentState
		{
			get => currentState;
			set
			{
				if (currentState == value)
					return;
				currentState = value;
				switch (currentState)
				{
					case Status.Init:
						solution.CMDDebugStart.CanExecute = false;
						solution.CMDDebugPause.CanExecute = false;
						solution.CMDDebugStop.CanExecute = false;
						solution.CMDDebugStepIn.CanExecute = false;
						solution.CMDDebugStepOut.CanExecute = false;
						solution.CMDDebugStepOver.CanExecute = false;
						break;
					case Status.Starting:
						solution.CMDDebugStart.CanExecute = false;
						solution.CMDDebugPause.CanExecute = false;
						solution.CMDDebugStop.CanExecute = false;
						break;
					case Status.Running:
						solution.CMDDebugStart.CanExecute = false;
						solution.CMDDebugPause.CanExecute = true;
						solution.CMDDebugStop.CanExecute = true;
						solution.CMDDebugStepIn.CanExecute = false;
						solution.CMDDebugStepOut.CanExecute = false;
						solution.CMDDebugStepOver.CanExecute = false;
						ResetCurrentExecutingLocation();
						break;
					case Status.Stopped:
						solution.CMDDebugStart.CanExecute = true;
						solution.CMDDebugPause.CanExecute = false;
						solution.CMDDebugStop.CanExecute = true;
						solution.CMDDebugStepIn.CanExecute = true;
						solution.CMDDebugStepOut.CanExecute = true;
						solution.CMDDebugStepOver.CanExecute = true;
						break;
					case Status.Exited:
						solution.CMDDebugStart.CanExecute = false;
						solution.CMDDebugPause.CanExecute = false;
						solution.CMDDebugStop.CanExecute = false;
						solution.CMDDebugStepIn.CanExecute = false;
						solution.CMDDebugStepOut.CanExecute = false;
						solution.CMDDebugStepOver.CanExecute = false;
						ResetCurrentExecutingLocation();
						break;
				}
				NotifyValueChanged(CurrentState);
			}
		}
		public bool BreakOnStartup
		{
			get => breakOnStartup;
			set
			{
				if (BreakOnStartup == value)
					return;
				breakOnStartup = value;
				NotifyValueChanged(breakOnStartup);
			}
		}
		public virtual ProjectView Project
		{
			get => project;
			set
			{
				if (project == value)
					return;
				project = value;
				NotifyValueChanged(Project);
			}
		}

		public abstract void Start();
		public abstract void Pause();
		public abstract void Continue();
		public abstract void Stop();

		public abstract void StepIn();
		public abstract void StepOver();
		public abstract void StepOut();

		public abstract void InsertBreakPoint(BreakPoint bp);
		public abstract void DeleteBreakPoint(BreakPoint bp);

		protected abstract void ResetCurrentExecutingLocation();


	}
}
