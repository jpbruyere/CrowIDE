// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;

namespace Crow.Coding
{
	public class ProjectItemNode : ProjectNode {
		#region CTOR
		public ProjectItemNode () { }
		public ProjectItemNode (ProjectView project, ProjectItem item) : base (project) {
			Item = item;
		}
		#endregion

		public ProjectItem Item;

		public override Picture Icon {
			get {
				switch (Extension) {
				case ".cs":
					return CrowIDE.IcoFileCS;
				case ".crow":
					return CrowIDE.IcoFileXML;
				case ".xml":
					return CrowIDE.IcoFileXML;
				case ".style":
					return CrowIDE.IcoStyle;
				case ".jpg":
				case ".jpeg":
				case ".png":
				case ".gif":
				case ".bmp":
					return CrowIDE.IcoImage;

				default:
					return base.Icon;
				}
			}
		}

		public string Extension => Path.GetExtension (RelativePath);
		public string RelativePath => Item.EvaluatedInclude?.NormalizePath ();
		public string FullPath => Path.GetFullPath(Path.Combine (Project.RootDir, RelativePath));
		public override ItemType Type => Enum.TryParse (Item.ItemType, true, out ItemType tmp) ? tmp : ItemType.Unknown;

		//used for saving open items
		internal string SaveID => $"{Project.DisplayName}|{RelativePath}";

		public override string DisplayName {
			get { 
				return Type == ItemType.Reference ?
					RelativePath :
					RelativePath.Split ('/').LastOrDefault();
			}
		}
		public string HintPath {
			get { return "HintPath?"; }
		}

		public override bool IsSelected {
			get => base.IsSelected;
			set {
				base.IsSelected = value;
				if (isSelected) {
					TreeNode pn = Parent;
					while (pn != null) {
						pn.IsExpanded = true;
						pn = pn.Parent;
					}
					Project.Solution.SelectedItem = this;
				}
			}
		}
	}
}

