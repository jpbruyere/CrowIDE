// Copyright (c) 2013-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
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

		Dictionary<string,List<CategoryContainer>> categoryContainersCache = new Dictionary<string,List<CategoryContainer>> (10);

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
					List<PropertyContainer> props = new List<PropertyContainer> (50);
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
					List<CategoryContainer> categories = new List<CategoryContainer> (20);

					foreach (IGrouping<string,PropertyContainer> ig in props.OrderBy (p => p.Name).GroupBy(pc=>pc.DesignCategory))
						categories.Add(new CategoryContainer(ig.Key, ig.ToArray()));

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
			get => projFile;
			set {
				if (projFile == value)
					return;
				projFile = value;
				NotifyValueChanged ("ProjectNode", projFile);
			}
		}
	}
}
