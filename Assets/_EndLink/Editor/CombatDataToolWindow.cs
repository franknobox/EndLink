using System;
using System.IO;
using EndLink.Combat;
using UnityEditor;
using UnityEngine;

namespace EndLink.Editor
{
    public sealed class CombatDataToolWindow : EditorWindow
    {
        public const string ActionsFolder = "Assets/_EndLink/Data/CombatData/Actions";
        public const string TagDefinitionsFolder = "Assets/_EndLink/Data/CombatData/Tags/Definitions";
        public const string TagCombinationRulesFolder = "Assets/_EndLink/Data/CombatData/Tags/CombinationRules";

        private static readonly string[] TabNames =
        {
            "Actions",
            "Tag Definitions",
            "Combination Rules",
            "Asset List"
        };

        private int _selectedTab;
        private Vector2 _scrollPosition;
        private string _actionAssetName = "CombatAction_New";
        private CombatActionType _actionType = CombatActionType.BasicAttack;
        private string _tagAssetName = "CombatTag_New";
        private string _combinationRuleAssetName = "CombatTagCombination_New";

        [MenuItem("EndLink/Combat Data Tool")]
        public static void Open()
        {
            CombatDataToolWindow window = GetWindow<CombatDataToolWindow>("Combat Data Tool");
            window.minSize = new Vector2(520f, 360f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawTabToolbar();
            EditorGUILayout.Space(8f);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            try
            {
                switch (_selectedTab)
                {
                    case 0:
                        DrawActionCreator();
                        break;
                    case 1:
                        DrawTagDefinitionCreator();
                        break;
                    case 2:
                        DrawCombinationRuleCreator();
                        break;
                    case 3:
                        DrawAssetList();
                        break;
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("EndLink Combat Data Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Create and browse combat data assets. Edit asset fields in the Inspector.");
            EditorGUILayout.Space(6f);
        }

        private void DrawTabToolbar()
        {
            int nextTab = GUILayout.Toolbar(_selectedTab, TabNames);

            if (nextTab == _selectedTab)
            {
                return;
            }

            _selectedTab = nextTab;
            GUI.FocusControl(string.Empty);
        }

        private void DrawActionCreator()
        {
            DrawFolderField("Folder", ActionsFolder);
            GUI.SetNextControlName("EndLink.CombatDataTool.ActionAssetName");
            _actionAssetName = EditorGUILayout.TextField("Action Asset Name", _actionAssetName);
            _actionType = (CombatActionType)EditorGUILayout.EnumPopup("Action Type", _actionType);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Action", GUILayout.Height(28f)))
                {
                    CreateCombatActionAsset();
                }

                if (GUILayout.Button("Show Folder", GUILayout.Height(28f)))
                {
                    SelectFolder(ActionsFolder);
                }
            }
        }

        private void DrawTagDefinitionCreator()
        {
            DrawFolderField("Folder", TagDefinitionsFolder);
            GUI.SetNextControlName("EndLink.CombatDataTool.TagDefinitionAssetName");
            _tagAssetName = EditorGUILayout.TextField("Tag Asset Name", _tagAssetName);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Tag Definition", GUILayout.Height(28f)))
                {
                    CreateAsset<CombatTagDefinition>(TagDefinitionsFolder, _tagAssetName, "CombatTag_New");
                }

                if (GUILayout.Button("Show Folder", GUILayout.Height(28f)))
                {
                    SelectFolder(TagDefinitionsFolder);
                }
            }
        }

        private void DrawCombinationRuleCreator()
        {
            DrawFolderField("Folder", TagCombinationRulesFolder);
            GUI.SetNextControlName("EndLink.CombatDataTool.CombinationRuleAssetName");
            _combinationRuleAssetName = EditorGUILayout.TextField("Rule Asset Name", _combinationRuleAssetName);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Combination Rule", GUILayout.Height(28f)))
                {
                    CreateAsset<CombatTagCombinationRule>(
                        TagCombinationRulesFolder,
                        _combinationRuleAssetName,
                        "CombatTagCombination_New");
                }

                if (GUILayout.Button("Show Folder", GUILayout.Height(28f)))
                {
                    SelectFolder(TagCombinationRulesFolder);
                }
            }
        }

        private void DrawAssetList()
        {
            DrawAssetSection<CombatActionDefinition>("Actions", ActionsFolder);
            EditorGUILayout.Space(10f);
            DrawAssetSection<CombatTagDefinition>("Tag Definitions", TagDefinitionsFolder);
            EditorGUILayout.Space(10f);
            DrawAssetSection<CombatTagCombinationRule>("Combination Rules", TagCombinationRulesFolder);
        }

        private static void DrawAssetSection<T>(string title, string folder)
            where T : UnityEngine.Object
        {
            EnsureFolder(folder);

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            EditorGUILayout.LabelField($"{title} ({guids.Length})", EditorStyles.boldLabel);

            if (guids.Length == 0)
            {
                EditorGUILayout.HelpBox("No assets found in this folder.", MessageType.Info);
                return;
            }

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

                if (asset == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(asset, typeof(T), false);

                    if (GUILayout.Button("Select", GUILayout.Width(72f)))
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
            }
        }

        private static void DrawFolderField(string label, string folder)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                EditorGUILayout.SelectableLabel(folder, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static void CreateAsset<T>(string folder, string rawAssetName, string fallbackName)
            where T : ScriptableObject
        {
            EnsureFolder(folder);

            string assetName = SanitizeAssetName(rawAssetName, fallbackName);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
            T asset = CreateInstance<T>();

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void CreateCombatActionAsset()
        {
            EnsureFolder(ActionsFolder);

            string assetName = SanitizeAssetName(_actionAssetName, "CombatAction_New");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ActionsFolder}/{assetName}.asset");
            CombatActionDefinition asset = CreateInstance<CombatActionDefinition>();
            asset.EditorInitialize(_actionType, assetName);

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static string SanitizeAssetName(string rawAssetName, string fallbackName)
        {
            string assetName = string.IsNullOrWhiteSpace(rawAssetName) ? fallbackName : rawAssetName.Trim();
            assetName = Path.GetFileNameWithoutExtension(assetName);

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                assetName = assetName.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(assetName) ? fallbackName : assetName;
        }

        private static void SelectFolder(string folder)
        {
            EnsureFolder(folder);
            UnityEngine.Object folderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);

            if (folderAsset == null)
            {
                return;
            }

            Selection.activeObject = folderAsset;
            EditorGUIUtility.PingObject(folderAsset);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(folder))
            {
                throw new InvalidOperationException($"Failed to create asset folder: {folder}");
            }
        }
    }
}
