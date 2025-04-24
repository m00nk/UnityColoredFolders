using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ColoredFolders {
	public static class HueFoldersBrowser {
		private static GUIStyle _sLabelNormal;
		private static GUIStyle _sLabelSelected;

		private static int _cnt = 0;

		// =======================================================================
		public static void folderColorization(string guid, Rect rect) {
			if (SettingsProvider.sInTreeViewOnly.get<bool>() && isTreeView() == false) {
				return;
			}

			if (rect.width < rect.height) {
				return;
			}

			_cnt++;
			Debug.Log($"Cnt: {_cnt}");

			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (isNotValidFolder(path))
				return;

			// get base color of folder, configure rect
			var data = getFolderData(out var isSubfolder);

			Color baseColor = SettingsProvider.sFoldersDefaultTint;
			Color folderColor = baseColor * SettingsProvider.sFoldersTint.get<Color>();

			if (data != null) {
				baseColor = data.color;
				if (baseColor.a <= 0f)
					return;

				folderColor = baseColor * SettingsProvider.sFoldersTint.get<Color>();
				if (isSubfolder) {
					var tint = SettingsProvider.sSubFoldersTint.get<Color>();
					folderColor *= tint;
				}
			}

			if (isTreeView() == false)
				rect.xMin += 3;

			// draw background, overdraw icon and text
			GUI.color = folderColor;
			GUI.DrawTexture(rect, gradient(), ScaleMode.ScaleAndCrop);

			GUI.color = Color.white;
			GUI.DrawTexture(iconRect(), folderIcon());

			if (SettingsProvider.sLabelOverride.get<bool>())
				GUI.Label(textRect(), Path.GetFileName(path), labelSkin());

			GUI.color = Color.white;

			// =======================================================================
			bool isNotValidFolder(string folderPath) {
				return AssetDatabase.IsValidFolder(folderPath) == false || folderPath.StartsWith("Packages") || folderPath.Equals("Assets");
			}

			SettingsProvider.FolderData getFolderData(out bool isSubFolder) {
				isSubFolder = false;

				foreach (var item in SettingsProvider.folderNamesDic) {
					var fileName = Path.GetFileName(path);
					if (fileName != null && fileName == item.Key) {
						return item.Value;
					}
				}

				isSubFolder = true;

				string foundSubPath = "";
				SettingsProvider.FolderData folderData = null;

				foreach (var item in SettingsProvider.folderNamesDic) {
					string f = '/' + item.Key + '/';
					if (path.Contains(f)) {
						string subPath = path.Substring(0, path.LastIndexOf(f, StringComparison.Ordinal));

						if (foundSubPath.Length < subPath.Length) {
							foundSubPath = subPath;
							folderData = item.Value;
						}
					}
				}

				return string.IsNullOrEmpty(foundSubPath) ? null : folderData;
			}

			Rect iconRect() {
				var result = new Rect(rect);
				result.width = result.height;

				return result;
			}

			Rect textRect() {
				var result = new Rect(rect);
				result.xMin += iconRect().width;
				if (isTreeView()) {
					result.yMax -= 1;
				}

				return result;
			}

			Texture folderIcon() {
				if (EditorGUIUtility.isProSkin)
					return EditorGUIUtility.IconContent(isFolderEmpty() ? "FolderEmpty Icon" : "Folder Icon").image;
				else {
					if (isSelected())
						return EditorGUIUtility.IconContent(isFolderEmpty() ? "FolderEmpty On Icon" : "Folder On Icon").image;
					else
						return EditorGUIUtility.IconContent(isFolderEmpty() ? "FolderEmpty Icon" : "Folder Icon").image;
				}
			}

			bool isFolderEmpty() {
				var items = Directory.EnumerateFileSystemEntries(path);
				using (var en = items.GetEnumerator())
					return en.MoveNext() == false;
			}

			bool isSelected() { return Selection.assetGUIDs.Contains(guid); }

			bool isTreeView() { return (rect.x - 16) % 14 == 0; }

			GUIStyle labelSkin() {
				if (_sLabelSelected == null) {
					_sLabelSelected = new GUIStyle("Label");
					_sLabelSelected.normal.textColor = Color.white;
					_sLabelSelected.hover.textColor = _sLabelSelected.normal.textColor;
				}

				if (_sLabelNormal == null) {
					_sLabelNormal = new GUIStyle("Label");
					_sLabelNormal.normal.textColor = EditorGUIUtility.isProSkin ? new Color32(175, 175, 175, 255) : new Color32(2, 2, 2, 255);
					_sLabelNormal.hover.textColor = _sLabelNormal.normal.textColor;
				}

				return isSelected() ? _sLabelSelected : _sLabelNormal;
			}

			Texture2D gradient() {
				if (SettingsProvider.sGradient == null)
					SettingsProvider._updateGradient();

				//if (_isSelected())
				//    return SettingsProvider.s_Fill;

				return SettingsProvider.sGradient;
			}
		}
	}
}
