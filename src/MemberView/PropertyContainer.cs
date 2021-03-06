﻿// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;

namespace Crow.Coding
{
	[DebuggerDisplay ("{Type}:{Name}")]
	public class PropertyContainer : IValueChange
	{
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged(string MemberName, object _value)
		{
			ValueChanged.Raise(this, new ValueChangeEventArgs(MemberName, _value));
		}
		#endregion

		PropertyInfo pi;
		MembersView mview;
		Command cmdReset, cmdGoToStyle;

		public List<Crow.Command> Commands;

		#region CTOR
		public PropertyContainer(MembersView mv, PropertyInfo prop){
			mview = mv;
			pi = prop;

			cmdReset = new Crow.Command (new Action (() => Reset ())) { Caption = "Reset to default" };
			cmdGoToStyle = new Crow.Command (new Action (() => GotoStyle ())) { Caption = "Goto style" };

			Commands = new List<Crow.Command> (new Crow.Command[] { cmdReset, cmdGoToStyle });
		}
		#endregion

		public string DesignCategory {
			get {
				DesignCategory dca = (DesignCategory)pi.GetCustomAttribute (typeof(DesignCategory));
				return dca == null ? "Divers" : dca.Name;					
			}
		}
		public string Name { get { return pi.Name; }}
		public object Value {
			get {
				return mview.ProjectNode?.SelectedItem == null ? null : pi.GetValue(mview.ProjectNode.SelectedItem);
			}
			set {
				try {
					if (value == Value)
						return;
					Widget g = Instance;
					string valstr = null, oldval = null;

					if (value != null)
						valstr = value.ToString();

					if (HasStyling)
						oldval = g.design_style_values[Name];
					else if (HasDefaultValue)
						oldval = DefaultValue?.ToString();
					else if (IsSetByIML)
						oldval = g.design_iml_values [Name];
					
					if (valstr == oldval){
						if (IsSetByIML){
							g.design_iml_values.Remove(Name);
							Debug.WriteLine("iml attrib removed {0}.{1}", g.Name, Name);
						}else
							return;
					}else{
						if (IsSetByIML){
							g.design_iml_values [Name] = valstr;
							Debug.WriteLine("iml update {0}.{1} = {2}", g.Name, Name, valstr);
						}else{
							g.design_iml_values.Add(Name,valstr);
							Debug.WriteLine("iml add {0}.{1} = {2}", g.Name, Name, valstr);
						}
					}

					if (!pi.PropertyType.IsAssignableFrom(value.GetType()) && pi.PropertyType != typeof(string)){
						if (pi.PropertyType.IsEnum) {
							if (value is string) {
								pi.SetValue (g, Enum.Parse (pi.PropertyType, (string)value));
							}else
								pi.SetValue (g, value);
						} else {
							MethodInfo me = pi.PropertyType.GetMethod
								("Parse", BindingFlags.Static | BindingFlags.Public,
									System.Type.DefaultBinder, new Type [] {typeof (string)},null);
							pi.SetValue (g, me.Invoke (null, new object[] { value.ToString() }), null);
						}
					}else
						pi.SetValue(g, value);

					Debug.WriteLine("\t\tPropContainer set design_dirty to instance");

					mview.ProjectNode.Instance.design_HasChanged = true;
					NotifyValueChanged ("Value", value);
					NotifyValueChanged ("LabForeground", LabForeground);
				} catch (Exception ex) {
					Debug.WriteLine ("Error setting property:"+ ex.ToString());
				}
				//
			}
		}
		/// <summary>
		/// for style attribute which is a string, return Style as type
		/// </summary>
		public string Type { get { return pi.PropertyType.IsEnum ?
				"System.Enum"
					: pi.Name == "Style" ? "Style" : pi.PropertyType.FullName; }}
		
		public object[] Choices {
			get {
				return pi.PropertyType.IsEnum ?
					Enum.GetValues (pi.PropertyType).Cast<object>().ToArray() :
					mview.ProjectNode.Project.solution.AvailaibleStyles;
			}
		}
		/// <summary>
		/// Current graphicobject instance
		/// </summary>
		public Widget Instance {
			get { return mview?.Instance as Widget; }
		}
		public object DefaultValue {
			get { return ((DefaultValueAttribute)(pi.GetCustomAttribute (typeof (DefaultValueAttribute)))).Value; }
		}
		public bool HasDefaultValue {
			get { return pi.GetCustomAttribute (typeof(DefaultValueAttribute))!=null; }
		}
		/// <summary>
		/// return true if current value comes from IML attributes
		/// </summary>
		public bool IsSetByIML {
			get { return Instance.design_iml_values.ContainsKey (Name); }
		}
		/// <summary>
		/// return true if member default value comes from style
		/// </summary>
		public bool HasStyling {
			get { return (bool)Instance?.design_style_locations.ContainsKey(Name); }
		}
		/// <summary>
		/// Return true if current value comes from styling
		/// </summary>
		public bool IsSetByStyling {
			get { return IsSetByIML ? false : HasStyling; }
		}


		public Fill LabForeground {
			get { return IsSetByIML ? Colors.DarkBlue : HasStyling ? Colors.Black : Colors.Grey;}
		}

		/// <summary>
		/// reset to default value
		/// </summary>
		public void Reset () {
			Widget inst = mview.ProjectNode.SelectedItem as Widget;
			if (!inst.design_iml_values.ContainsKey (Name))
				return;
			inst.design_iml_values.Remove (Name);

			NotifyValueChanged ("LabForeground", LabForeground);
			mview.ProjectNode.UpdateSource(this, mview.ProjectNode.Instance.GetIML());
			//mview.ProjectNode.Instance.design_HasChanged = true;
			//should reinstantiate to get default
		}
		public void GotoStyle(){
			Widget g = Instance;
			if (!g.design_style_locations.ContainsKey (Name))
				return;
			FileLocation fl = g.design_style_locations [Name];
			ProjectFileNode pf;

			if (!mview.ProjectNode.Project.TryGetProjectFileFromPath ("#" + fl.FilePath, out pf))
				return;

			if (!pf.IsOpened)
				pf.Open ();

			pf.CurrentLine = fl.Line;
			pf.CurrentColumn = fl.Column;

			pf.IsSelected = true;

		}

		public override string ToString ()
		{
			return string.Format ("{0} = {1}", Name, Value);
		}
	}
}

