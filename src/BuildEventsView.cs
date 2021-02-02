// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Collections;
using Crow.Cairo;
using Microsoft.Build.Framework;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Crow
{
	public class BuildEventsView : ScrollingObject
	{
		ObservableList<BuildEventArgs> events;
		List<uint> eventsDic = new List<uint>();

		bool scrollOnOutput;
		uint visibleLines = 1;
		uint lineCount = 0;
		FontExtents fe;
		string searchString;
		int hoverEventIndex = -1, currentEventIndex = -1;

		[DefaultValue (true)]
		public virtual bool ScrollOnOutput {
			get { return scrollOnOutput; }
			set {
				if (scrollOnOutput == value)
					return;
				scrollOnOutput = value;
				NotifyValueChanged ("ScrollOnOutput", scrollOnOutput);

			}
		}
		public string SearchString {
			get => searchString;
			set {
				if (searchString == value)
					return;
				searchString = value;
				NotifyValueChanged ("SearchString", searchString);

				performSearch (searchString);
			}
		}
		void performSearch (string str, bool next = false) {
			if (string.IsNullOrEmpty (str))
				return;
			BuildEventArgs[] evts = events.ToArray ();
			if (evts.Length == 0) {
				CurrentEventIndex = -1;
				return;
            }			
			int idx = CurrentEventIndex < 0 ? 0 : next ? CurrentEventIndex + 1 : CurrentEventIndex;
			while (idx < evts.Length) {
				if (evts[idx].Message.Contains (str)) {
					CurrentEventIndex = idx;
					return;
                }
				idx++;
            }
			if (CurrentEventIndex <= 0)//all the list has been searched
				return;
			idx = 0;
			while (idx < CurrentEventIndex) {
				if (evts[idx].Message.Contains (str)) {
					CurrentEventIndex = idx;
					return;
				}
				idx++;
			}

		}

		public int HoverEventIndex {
			get { return hoverEventIndex; }
			set {
				if (hoverEventIndex == value)
					return;
				hoverEventIndex = value;
				NotifyValueChanged ("HoverEventIndex", hoverEventIndex);
				RegisterForRedraw ();
				lock (eventsDic) {
					if (hoverLineIndex < 0 || hoverLineIndex >= eventsDic.Count)
						return;
					NotifyValueChanged ("HoverEventLineCount", eventsDic[hoverLineIndex]);
				}
				//NotifyValueChanged ("HoverError", buffer [hoverLine].exception);
			}
		}
		public int CurrentEventIndex {
			get { return currentEventIndex; }
			set {
				if (currentEventIndex == value)
					return;

				lock(eventsDic) {
					if (value > eventsDic.Count) {
						if (currentEventIndex == eventsDic.Count - 1)
							return;
						currentEventIndex = eventsDic.Count - 1;
                    }else
						currentEventIndex = value;
				}
				
				NotifyValueChanged ("CurrentEventIndex", currentEventIndex);

				if (currentEventIndex < 0)
					ScrollY = 0;
				else {
					lock (eventsDic) {
						if (eventsDic[currentEventIndex] < ScrollY || eventsDic[currentEventIndex] >= ScrollY + visibleLines) {
							uint evtLines = currentEventIndex < eventsDic.Count - 1 ?
								eventsDic[currentEventIndex + 1] - eventsDic[currentEventIndex] :
								lineCount - eventsDic[currentEventIndex];
							if (evtLines >= visibleLines)
								ScrollY = (int)(eventsDic[currentEventIndex]);
							else
								ScrollY = (int)(eventsDic[currentEventIndex] - visibleLines / 2 + evtLines / 2);														
						}
					}
				}

				RegisterForRedraw ();
			}			
		}

        private void onSearch (object sender, KeyEventArgs e) {
			if (e.Key == Glfw.Key.Enter)
				performSearch (SearchString, true);
        }

        public virtual ObservableList<BuildEventArgs> Events {
			get => events;
			set {
				if (events == value)
					return;
				if (events != null) {
					events.ListClear -= Messages_ListClear;
					events.ListAdd -= Lines_ListAdd;
					events.ListRemove -= Lines_ListRemove;
					reset ();
				}
				events = value;
				if (events != null) {
					events.ListClear += Messages_ListClear;
					events.ListAdd += Lines_ListAdd;
					events.ListRemove += Lines_ListRemove;
					lineCount = 0;
					lock (eventsDic) {
						foreach (BuildEventArgs e in events) {
							eventsDic.Add (lineCount);
							if (string.IsNullOrEmpty (e.Message))
								lineCount++;
							else
								lineCount += (uint)Regex.Split (e.Message, "\r\n|\r|\n").Length;
						}
					}
				}
				NotifyValueChanged ("Events", events);
				RegisterForGraphicUpdate ();
			}
		}

		void reset ()
		{
			lineCount = 0;
			lock(eventsDic)
				eventsDic.Clear ();
			ScrollY = ScrollX = 0;
			MaxScrollY = MaxScrollX = 0;
		}

		void Messages_ListClear (object sender, ListChangedEventArg e)
		{
			reset ();
			RegisterForGraphicUpdate ();
		}


		void Lines_ListAdd (object sender, ListChangedEventArg e)
		{
			BuildEventArgs bea = e.Element as BuildEventArgs;
			lock (eventsDic)
				eventsDic.Add (lineCount);
			string msg = bea.Message;
			lineCount += string.IsNullOrEmpty(msg) ? 1 : (uint)Regex.Split (msg, "\r\n|\r|\n").Length;
			MaxScrollY = (int)(lineCount - visibleLines);
			if (scrollOnOutput)
				ScrollY = MaxScrollY;
		}

		void Lines_ListRemove (object sender, ListChangedEventArg e)
		{
			BuildEventArgs bea = e.Element as BuildEventArgs;
			lock (eventsDic)
				eventsDic.RemoveAt (e.Index);
			string msg = (e.Element as BuildEventArgs).Message;
			lineCount -= string.IsNullOrEmpty (msg) ? 1 : (uint)Regex.Split (msg, "\r\n|\r|\n").Length;
			MaxScrollY = (int)(lineCount - visibleLines);
		}


		public override void OnLayoutChanges (LayoutingType layoutType)
		{
			base.OnLayoutChanges (layoutType);

			if (layoutType == LayoutingType.Height) {				
				using (Context gr = new Context (IFace.surf)) {
					//Cairo.FontFace cf = gr.GetContextFontFace ();
					gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
					gr.SetFontSize (Font.Size);
					fe = gr.FontExtents;
				}				
				visibleLines = (uint)(Math.Max(1, Math.Floor ((double)ClientRectangle.Height / fe.Height)));
				MaxScrollY = (int)(lineCount - visibleLines);
			}
		}
				
		protected override void onDraw (Context gr)
		{
			base.onDraw (gr);

			if (events == null)
				return;

			gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
			gr.SetFontSize (Font.Size);

			Rectangle r = ClientRectangle;

			double y = ClientRectangle.Y;
			double x = ClientRectangle.X;

			int spaces = 0;

			uint [] evts;
			lock (eventsDic)
				evts = eventsDic.ToArray ();

			int idx = Array.BinarySearch (evts, (uint)ScrollY);
			if (idx < 0) 
				idx = ~idx - 1;
			if (idx < 0)
				return;

			int diff = ScrollY - (int)evts [idx];

			int i = 0;
			while (i < visibleLines) {

				if (idx >= events.Count)
					break;
				//if ((lines [i + Scroll] as string).StartsWith ("error", StringComparison.OrdinalIgnoreCase)) {
				//	errorFill.SetAsSource (gr);
				//	gr.Rectangle (x, y, (double)r.Width, fe.Height);
				//	gr.Fill ();
				//	Foreground.SetAsSource (gr);
				//}
				BuildEventArgs evt = events[idx] as BuildEventArgs;
				string[] lines = Regex.Split (evt.Message, "\r\n|\r|\n");//|\r|\n|\\\\n");

				if (idx == HoverEventIndex || idx == CurrentEventIndex) {
					RectangleD highlight = new RectangleD (x, y, r.Width, (lines.Length - diff) * fe.Height);
					highlight.Height = Math.Min (r.Bottom - y, highlight.Height);
					gr.Rectangle (highlight);
					if (idx == CurrentEventIndex)
						gr.SetSource (0, 0.1, 0.2);
					else
						gr.SetSource (0, 0, 0.1);
					
					gr.Fill ();
				}

				if (evt is BuildMessageEventArgs) {
					BuildMessageEventArgs msg = evt as BuildMessageEventArgs;
					switch (msg.Importance) {
					case MessageImportance.High:
						gr.SetSource (Colors.White);
						break;
					case MessageImportance.Normal:
						gr.SetSource (Colors.Grey);
						break;
					case MessageImportance.Low:
						gr.SetSource (Colors.Jet);
						break;
					}
				} else if (evt is BuildStartedEventArgs || evt is BuildFinishedEventArgs)
					gr.SetSource (Colors.White);
				else if (evt is ProjectStartedEventArgs || evt is ProjectFinishedEventArgs)
					gr.SetSource (Colors.RoyalBlue);
				else if (evt is BuildErrorEventArgs)
					gr.SetSource (Colors.Red);
				else if (evt is BuildWarningEventArgs)
					gr.SetSource (Colors.Orange);
				else if (evt is TaskStartedEventArgs || evt is TaskFinishedEventArgs)
					gr.SetSource (Colors.Cyan);
				else if (evt is TargetStartedEventArgs || evt is TargetFinishedEventArgs)
					gr.SetSource (Colors.GreenYellow);
				else if (evt is BuildEventArgs)
					gr.SetSource (Colors.Yellow);
				else if (evt is BuildStatusEventArgs)
					gr.SetSource (Colors.Green);										


				for (int j = diff; j < lines.Length; j++) {
					gr.MoveTo (x, y + fe.Ascent);
					gr.ShowText (new string (' ', spaces) + lines[j]);
					y += fe.Height;
					i++;
					if (y > r.Bottom)
						break;
				}
				diff = 0;
				idx++;

				gr.Fill ();
			}
		}

		Point mouseLocalPos;
		int hoverLineIndex = -1;
		
		public override void onMouseMove (object sender, MouseMoveEventArgs e) {
            base.onMouseMove (sender, e);

			if (lineCount == 0) {
				hoverLineIndex = -1;
				HoverEventIndex = -1;
				return;
            }

			mouseLocalPos = e.Position - ScreenCoordinates (Slot).TopLeft - ClientRectangle.TopLeft;
			int lastHoverLineIndex = hoverLineIndex;
			hoverLineIndex = (int)Math.Min (lineCount, Math.Max (0, Math.Floor (mouseLocalPos.Y / (fe.Ascent + fe.Descent))) + ScrollY);
			NotifyValueChanged ("HoverLineIndex", hoverLineIndex);
			if (lastHoverLineIndex == hoverLineIndex)
				return;
			lock (eventsDic) {

				if (lastHoverLineIndex > 0) {
					if (HoverEventIndex >= eventsDic.Count) {
						hoverLineIndex = -1;
						HoverEventIndex = -1;
						return;
                    }
					if (lastHoverLineIndex < hoverLineIndex) {
						for (int i = HoverEventIndex; i < eventsDic.Count; i++) {
							if (eventsDic[i] > hoverLineIndex) {
								HoverEventIndex = i - 1;
								return;
							}
						}
					} else {
						for (int i = HoverEventIndex; i > 0; i--) {
							if (eventsDic[i] <= hoverLineIndex) {
								HoverEventIndex = i;
								return;
							}
						}
					}
				} else {
					for (int i = 0; i < eventsDic.Count; i++) {
						if (eventsDic[i] > hoverLineIndex) {
							HoverEventIndex = i - 1;
							return;
						}
					}
				}
			}		
			HoverEventIndex = 0;			
		}

        public override void onMouseDown (object sender, MouseButtonEventArgs e) {
            base.onMouseDown (sender, e);
			CurrentEventIndex = HoverEventIndex;
        }
    }
}

