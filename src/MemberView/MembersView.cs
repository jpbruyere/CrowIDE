﻿// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.ComponentModel;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace Crow.Coding
{
	public class MembersView : ListBox
	{		
		object instance;
		ProjectItemNode projFile;

		public MembersView () : base() {}

		//cache property containers per type
		//Dictionary<string,PropertyContainer[]> propContainersCache = new Dictionary<string, PropertyContainer[]>();
		Dictionary<string,List<CategoryContainer>> categoryContainersCache = new Dictionary<string,List<CategoryContainer>> ();

		[DefaultValue(null)]
		public virtual object Instance {
			get { return instance; }
			set {
				if (instance == value)
					return;
				object lastInst = instance;
				//Console.WriteLine ($"mview instance: {value}");
				instance = value;
				NotifyValueChanged ("Instance", instance);

				if (Instance is Widget) {
					NotifyValueChanged ("SelectedItemName", Instance.GetType().Name + (Instance as Widget).design_id
						+ ":" + (Instance as Widget).design_imlPath );
				}else
					NotifyValueChanged ("SelectedItemName", "");

				if (instance == null) { 
					Data = null;
					return;
				}	

				Type it = instance.GetType ();
				if (!categoryContainersCache.ContainsKey (it.FullName)) {
					MemberInfo[] members = it.GetMembers (BindingFlags.Public | BindingFlags.Instance);
					List<PropertyContainer> props = new List<PropertyContainer> ();
					foreach (MemberInfo m in members) {
						if (m.MemberType == MemberTypes.Property) {
							PropertyInfo pi = m as PropertyInfo;
							if (!pi.CanWrite)
								continue;
							if (pi.GetCustomAttribute (typeof(XmlIgnoreAttribute)) != null)
								continue;
							props.Add (new PropertyContainer (this, pi));
						}
					}
					//propContainersCache.Add (it.FullName, props.OrderBy (p => p.Name).ToArray ());
					List<CategoryContainer> categories = new List<CategoryContainer> ();

					foreach (IGrouping<string,PropertyContainer> ig in props.OrderBy (p => p.Name).GroupBy(pc=>pc.DesignCategory)) {
						categories.Add(new CategoryContainer(ig.Key, ig.ToArray()));
					}
					categoryContainersCache.Add (it.FullName, categories);
				}


				Data = categoryContainersCache[it.FullName];

				if (lastInst != instance) {
					foreach (CategoryContainer cc in categoryContainersCache [it.FullName]) {
						foreach (PropertyContainer pc in cc.Properties) {
							pc.NotifyValueChanged ("Value", pc.Value);
							pc.NotifyValueChanged ("LabForeground", pc.LabForeground);
						}
					}
				}
			}
		}
		public ProjectItemNode ProjectNode {
			get { return projFile; }
			set {
				if (projFile == value)
					return;

				//				if (projFile != null)
				//					projFile.UnregisterEditor (this);
				projFile = value;



//				if (projFile != null)
//					projFile.RegisterEditor (this);

				NotifyValueChanged ("ProjectNode", projFile);
			}
		}

//		public void updateSource () {
//			if (projFile == null)
//				return;
//			projFile.UpdateSource (this, (Instance as GraphicObject).GetIML ());
//		}

//		public override void Paint (ref Context ctx)
//		{
//			base.Paint (ref ctx);
//
//			if (SelectedIndex < 0)
//				return;
//
//			Rectangle r =  Parent.ContextCoordinates(Items [SelectedIndex].Slot);
//			ctx.SetSourceRGB (0, 0, 1);
//			ctx.Rectangle (r);
//			ctx.LineWidth = 2;
//			ctx.Stroke ();
//		}

	}
}
