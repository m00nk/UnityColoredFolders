using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace HueFolders
{
    public class SettingsProvider : UnityEditor.SettingsProvider
    {
        public const  string       K_PREFS_FILE              = nameof(HueFolders) + "_Prefs.json";
        public const  string       K_PREFS_PATH              = "ProjectSettings\\" + K_PREFS_FILE;
        public const  int          K_GRADIENT_WIDTH          = 16;
        
        public static readonly EditorOption sInTreeViewOnly         = new EditorOption(nameof(HueFolders) + "_InTreeViewOnly");
        private const  bool         K_IN_TREE_VIEW_ONLY_DEFAULT = true;
        
        public static readonly EditorOption sFoldersTint           = new EditorOption(nameof(HueFolders) + "_FoldersTint");
        private static readonly Color        kFoldersTintDefault   = Color.white;
        
        public static readonly EditorOption sSubFoldersTint         = new EditorOption(nameof(HueFolders) + "_SubFoldersTint");
        private static readonly Color        kSubFoldersTintDefault = new Color(1, 1, 1, 0.7f);
        
        public static readonly EditorOption sGradientScale          = new EditorOption(nameof(HueFolders) + "_GradientScale");
        private static readonly Vector2      kGradientScaleDefault  = new Vector2(0.536f, 1f);
        
        public static readonly EditorOption sLabelOverride         = new EditorOption(nameof(HueFolders) + "_LabelOverride");
        private const  bool         K_LABEL_OVERRIDE_DEFAULT = true;
        
        public static Dictionary<string, FolderData> sFoldersDataDic;
        public static Color                          sFoldersDefaultTint;
        private static readonly Color                         kFoldersDefaultTintDefault = new Color(.6f, .6f, .7f, .7f);
        
        public static List<FolderData>               sFoldersData;
        public static Texture2D                      sGradient;
        
        private ReorderableList _foldersList;

        // =======================================================================
        [Serializable]
        private class JsonWrapper
        {
            [FormerlySerializedAs("DefaultTint")] public Color                              defaultTint;
            [FormerlySerializedAs("FoldersData")] public DictionaryData<string, FolderData> foldersData;
            
            // =======================================================================
            [Serializable]
            public class DictionaryData<TKey, TValue>
            {
                [FormerlySerializedAs("Keys")] public List<TKey>   keys;
                [FormerlySerializedAs("Values")] public List<TValue> values;
                
                public IEnumerable<KeyValuePair<TKey, TValue>> enumerate()
                {
                    if (keys == null || values == null)
                        yield break; 
                            
                    for (var n = 0; n < keys.Count; n++)
                        yield return new KeyValuePair<TKey, TValue>(keys[n], values[n]);
                }

                public DictionaryData() 
                    : this(new List<TKey>(), new List<TValue>())
                {
                }
                
                public DictionaryData(List<TKey> keys, List<TValue> values)
                {
                    this.keys   = keys;
                    this.values = values;
                }
                
                public DictionaryData(IEnumerable<KeyValuePair<TKey, TValue>> data)
                {
                    var pairs = data as KeyValuePair<TKey, TValue>[] ?? data.ToArray();
                    keys   = pairs.Select(n => n.Key).ToList();
                    values = pairs.Select(n => n.Value).ToList();
                }
            }
        }
        
        [Serializable]
        public class FolderData
        {
            [FormerlySerializedAs("_guid")] public string strData;
            [FormerlySerializedAs("_color")] public Color  color;
            [FormerlySerializedAs("_recursive")] public bool   recursive;
        }
        
        public class EditorOption
        {
            public string key;
            public object val;

            // =======================================================================
            public EditorOption(string key)
            {
                this.key = key;
            }

            public void setup<T>(T def)
            {
                if (hasPrefs() == false)
                    write(def);
                
                val = read<T>(def);
            }
            
            public bool hasPrefs() => EditorPrefs.HasKey(key);
            
            public T get<T>()
            {
                return (T)val;
            }
            
            public T read<T>(T fallOff = default)
            {
                try
                {
                    var type = typeof(T);
                    
                    if (type == typeof(bool))
                        return (T)(object)EditorPrefs.GetBool(key);
                    if (type == typeof(int))
                        return (T)(object)EditorPrefs.GetInt(key);
                    if (type == typeof(float))
                        return (T)(object)EditorPrefs.GetFloat(key);
                    if (type == typeof(string))
                        return (T)(object)EditorPrefs.GetString(key);
                    
                    return JsonUtility.FromJson<T>(EditorPrefs.GetString(key));
                }
                catch
                {
                    return fallOff;
                }
            }
            
            public void write<T>(T value)
            {
                var type = typeof(T);
                this.val = value;
                
                if (type == typeof(bool))
                    EditorPrefs.SetBool(key, (bool)this.val);
                else
                if (type == typeof(int))
                    EditorPrefs.SetInt(key, (int)this.val);
                else
                if (type == typeof(string))
                    EditorPrefs.SetString(key, (string)this.val);
                else
                if (type == typeof(float))
                    EditorPrefs.SetFloat(key, (float)this.val);
                else
                    EditorPrefs.SetString(key, JsonUtility.ToJson(value));
            }
            
            public void OnGui<T>(Func<T, T> draw)
            {
                EditorGUI.BeginChangeCheck();
                
                var value = draw(get<T>());
                
                if (EditorGUI.EndChangeCheck())
                    write(value);
            }
        }
        
        // =======================================================================
        public SettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }
        
        [SettingsProvider]
        public static UnityEditor.SettingsProvider createSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/Colored Folders", SettingsScope.User);
            return provider;
        }
        
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            // initialize data from json, read editor prefs
            setProjectDataDefault();

            if (File.Exists(K_PREFS_PATH))
            {
                using var file = File.OpenText(K_PREFS_PATH);
                try
                {
                    var data = JsonUtility.FromJson<JsonWrapper>(file.ReadToEnd());
                    
                    sFoldersData = data.foldersData
                                        .enumerate()
                                        .Select(n => n.Value)
                                        .ToList();
                    
                    sFoldersDefaultTint = data.defaultTint;
                }
                catch
                {
                    setProjectDataDefault();
                }
            }
            
            sFoldersDataDic = sFoldersData.ToDictionary(n => n.strData, n => n);
            
            sInTreeViewOnly.setup(K_IN_TREE_VIEW_ONLY_DEFAULT);
            sSubFoldersTint.setup(kSubFoldersTintDefault);
            sGradientScale.setup(kGradientScaleDefault);
            sFoldersTint.setup(kFoldersTintDefault);
            sLabelOverride.setup(K_LABEL_OVERRIDE_DEFAULT);

            _updateGradient();
            EditorApplication.projectWindowItemOnGUI += HueFoldersBrowser.folderColorization;

            // -----------------------------------------------------------------------
            void setProjectDataDefault()
            {
                sFoldersData        = new List<FolderData>();
                sFoldersDefaultTint = kFoldersDefaultTintDefault;
            }
        }

        public override void OnGUI(string searchContext)
        {
            // draw ui, update variables
            

            // editor prefs variables
            EditorGUI.BeginChangeCheck();

            var inTreeViewOnly = EditorGUILayout.Toggle("In Tree View Only", sInTreeViewOnly.get<bool>());
            // project prefs variables
            EditorGUI.BeginChangeCheck();
            
            sFoldersDefaultTint = EditorGUILayout.ColorField("Default Tint", sFoldersDefaultTint);
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorApplication.RepaintProjectWindow();
                _saveProjectPrefs();
            }
            var foldersTint    = EditorGUILayout.ColorField("Folders Tint", sFoldersTint.get<Color>());
            var subFoldersTint = EditorGUILayout.ColorField("Sub Folders Tint", sSubFoldersTint.get<Color>());
            var gradientScale  = sGradientScale.get<Vector2>(); 
            sLabelOverride.OnGui<bool>( val => EditorGUILayout.Toggle("Label Override", val));
            EditorGUILayout.MinMaxSlider("Gradient Scale", ref gradientScale.x, ref gradientScale.y, 0f, 1f);
            
            if (EditorGUI.EndChangeCheck())
            {
                sInTreeViewOnly.write(inTreeViewOnly);
                sSubFoldersTint.write(subFoldersTint);
                sGradientScale.write(gradientScale);
                sFoldersTint.write(foldersTint);
                
                EditorApplication.RepaintProjectWindow();
                _updateGradient();
            }
            
            _getFoldersList().DoLayoutList();
            
        }
        
        public static void _updateGradient()
        {
            sGradient          = new Texture2D(K_GRADIENT_WIDTH, 1);
            sGradient.wrapMode = TextureWrapMode.Clamp;
            var range = sGradientScale.get<Vector2>();
            
            if (range == new Vector2(0, 1))
            {
                for (var x = 0; x < K_GRADIENT_WIDTH; x++)
                    sGradient.SetPixel(x, 0, new Color(1, 1, 1, 1));
            }
            else
            {
                for (var x = 0; x < K_GRADIENT_WIDTH; x++)
                    sGradient.SetPixel(x, 0, new Color(1, 1, 1, getAlpha(x)));

                // -----------------------------------------------------------------------
                float getAlpha(int xPixel)
                {
                    var xScale = xPixel / (K_GRADIENT_WIDTH - 1f);
                    
                    if (xScale >= range.x && xScale <= range.y)
                        return 1f;
                    
                    var distance = xScale < range.x ? range.x - xScale : xScale - range.y; 
                    return Mathf.Clamp01(1f - distance * 3f);
                }
            }

            sGradient.Apply();
        }

        private ReorderableList _getFoldersList()
        {
            if (_foldersList != null)
                return _foldersList;
            
            _foldersList = new ReorderableList(sFoldersData, typeof(FolderData), true, true, true, true);
            _foldersList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = sFoldersData[index];
                
                var refRect   = new Rect(rect.position, new Vector2(rect.size.x * .5f - EditorGUIUtility.standardVerticalSpacing, rect.size.y));
                var colorRect = new Rect(rect.position + new Vector2(rect.size.x * .5f, 0f), new Vector2(rect.size.x * .5f - 18f - EditorGUIUtility.standardVerticalSpacing, rect.size.y));
                var recRect   = new Rect(rect.position + new Vector2(rect.size.x - 18f, 0f), new Vector2(18f, rect.size.y));
                
                EditorGUI.BeginChangeCheck();
                var folder = EditorGUI.ObjectField(refRect,
                                                   GUIContent.none,
                                                   AssetDatabase.LoadAssetAtPath<DefaultAsset>(AssetDatabase.GUIDToAssetPath(element.strData)),
                                                   typeof(DefaultAsset),
                                                   false);
                
                element.color     = EditorGUI.ColorField(colorRect, GUIContent.none, element.color);
                element.recursive = EditorGUI.Toggle(recRect, GUIContent.none, element.recursive);
                
                if (EditorGUI.EndChangeCheck())
                {
                    var fodlerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(folder));
                    
                    if (element.strData != fodlerGuid)
                    { 
                        // ignore non directory files
                        if (folder != null && File.GetAttributes(AssetDatabase.GetAssetPath(folder)).HasFlag(FileAttributes.Directory) == false)
                            folder = null;

                        // ignore if already contains
                        if (folder != null && sFoldersData.Any(n => n.strData == fodlerGuid))
                            folder = null;
                    }
                    
                    element.strData = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(folder));
                    _saveProjectPrefs();
                     
                    EditorApplication.RepaintProjectWindow();
                }
                
            };
            _foldersList.elementHeight = EditorGUIUtility.singleLineHeight;
            _foldersList.onRemoveCallback = list =>
            {
                sFoldersData.RemoveAt(list.index);
                _saveProjectPrefs();
            };
            _foldersList.onAddCallback = list =>
            {
                var color = Color.HSVToRGB(Random.value, 0.7f, 0.8f);
                color.a = 0.7f;
                
                sFoldersData.Add(new FolderData() { color = color, recursive = true});
            };
            _foldersList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, new GUIContent("Folders", ""));
            };
            
            return _foldersList;
        }
        
        private void _saveProjectPrefs()
        {
            sFoldersDataDic = sFoldersData
                               .Where(n => n != null && string.IsNullOrEmpty(n.strData) == false && n.strData != Guid.Empty.ToString())
                               .ToDictionary(n => n.strData, n => n);
            
            var json = new JsonWrapper()
            {
                defaultTint = sFoldersDefaultTint,
                foldersData = new JsonWrapper.DictionaryData<string, FolderData>(sFoldersDataDic
                                                                            .Values
                                                                            .Select(n => new KeyValuePair<string, FolderData>(n.strData, n)))
            };
            
            File.WriteAllText(K_PREFS_PATH, JsonUtility.ToJson(json));
        }
    }
}
