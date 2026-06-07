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
        private string _tagId = "combat.tag";
        private string _tagDisplayName = "Combat Tag";
        private int _tagLevel = 1;
        private float _tagDefaultDuration;
        private int _tagMaxStackCount = 1;
        private string _combinationRuleAssetName = "CombatTagCombination_New";
        private CombatTagDefinition _ruleFirstTag;
        private CombatTagDefinition _ruleSecondTag;
        private int _ruleFirstStack = 1;
        private int _ruleSecondStack = 1;
        private int _rulePriority;
        private CombatTagReactionEffectType _ruleEffectType = CombatTagReactionEffectType.ApplyTag;
        private CombatTagDefinition _ruleEffectTag;
        private float _ruleEffectDuration;
        private int _ruleEffectStackCount = 1;
        private int _ruleEffectDamageAmount;
        private CombatDamageType _ruleEffectDamageType = CombatDamageType.RuntimeDamage;
        private string _ruleEffectId;

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
            _tagId = EditorGUILayout.TextField("Tag Id", _tagId);
            _tagDisplayName = EditorGUILayout.TextField("Display Name", _tagDisplayName);
            _tagLevel = Mathf.Max(1, EditorGUILayout.IntField("Tag Level", _tagLevel));
            _tagDefaultDuration = Mathf.Max(0f, EditorGUILayout.FloatField("Default Duration", _tagDefaultDuration));
            _tagMaxStackCount = Mathf.Max(1, EditorGUILayout.IntField("Max Stack Count", _tagMaxStackCount));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Tag Definition", GUILayout.Height(28f)))
                {
                    CreateCombatTagDefinitionAsset();
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
            _ruleFirstTag = (CombatTagDefinition)EditorGUILayout.ObjectField("First Tag", _ruleFirstTag, typeof(CombatTagDefinition), false);
            _ruleFirstStack = Mathf.Max(1, EditorGUILayout.IntField("First Stack", _ruleFirstStack));
            _ruleSecondTag = (CombatTagDefinition)EditorGUILayout.ObjectField("Second Tag", _ruleSecondTag, typeof(CombatTagDefinition), false);
            _ruleSecondStack = Mathf.Max(1, EditorGUILayout.IntField("Second Stack", _ruleSecondStack));
            _rulePriority = EditorGUILayout.IntField("Priority", _rulePriority);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("First Reaction Effect", EditorStyles.boldLabel);
            _ruleEffectType = (CombatTagReactionEffectType)EditorGUILayout.EnumPopup("Effect Type", _ruleEffectType);

            if (UsesTag(_ruleEffectType))
            {
                _ruleEffectTag = (CombatTagDefinition)EditorGUILayout.ObjectField("Effect Tag", _ruleEffectTag, typeof(CombatTagDefinition), false);
                _ruleEffectDuration = Mathf.Max(0f, EditorGUILayout.FloatField("Duration", _ruleEffectDuration));
                _ruleEffectStackCount = Mathf.Max(1, EditorGUILayout.IntField("Stack Count", _ruleEffectStackCount));
            }

            if (_ruleEffectType == CombatTagReactionEffectType.DealDamage)
            {
                _ruleEffectDamageAmount = Mathf.Max(0, EditorGUILayout.IntField("Damage Amount", _ruleEffectDamageAmount));
                _ruleEffectDamageType = (CombatDamageType)EditorGUILayout.EnumPopup("Damage Type", _ruleEffectDamageType);
            }

            if (_ruleEffectType == CombatTagReactionEffectType.CustomEvent)
            {
                _ruleEffectId = EditorGUILayout.TextField("Effect Id", _ruleEffectId);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Combination Rule", GUILayout.Height(28f)))
                {
                    CreateCombatTagCombinationRuleAsset();
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
                    EditorGUILayout.LabelField(GetAssetSummary(asset), GUILayout.MinWidth(220f));

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

        private void CreateCombatTagDefinitionAsset()
        {
            EnsureFolder(TagDefinitionsFolder);

            string assetName = SanitizeAssetName(_tagAssetName, "CombatTag_New");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{TagDefinitionsFolder}/{assetName}.asset");
            CombatTagDefinition asset = CreateInstance<CombatTagDefinition>();
            SerializedObject serializedAsset = new SerializedObject(asset);

            serializedAsset.FindProperty("tagId").stringValue = string.IsNullOrWhiteSpace(_tagId) ? assetName : _tagId.Trim();
            serializedAsset.FindProperty("displayName").stringValue = string.IsNullOrWhiteSpace(_tagDisplayName) ? assetName : _tagDisplayName.Trim();
            serializedAsset.FindProperty("tagLevel").intValue = Mathf.Max(1, _tagLevel);
            serializedAsset.FindProperty("defaultDuration").floatValue = Mathf.Max(0f, _tagDefaultDuration);
            serializedAsset.FindProperty("maxStackCount").intValue = Mathf.Max(1, _tagMaxStackCount);
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void CreateCombatTagCombinationRuleAsset()
        {
            EnsureFolder(TagCombinationRulesFolder);

            string assetName = SanitizeAssetName(_combinationRuleAssetName, "CombatTagCombination_New");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{TagCombinationRulesFolder}/{assetName}.asset");
            CombatTagCombinationRule asset = CreateInstance<CombatTagCombinationRule>();
            SerializedObject serializedAsset = new SerializedObject(asset);

            serializedAsset.FindProperty("firstTag").objectReferenceValue = _ruleFirstTag;
            serializedAsset.FindProperty("secondTag").objectReferenceValue = _ruleSecondTag != null ? _ruleSecondTag : _ruleFirstTag;
            serializedAsset.FindProperty("requiredFirstStack").intValue = Mathf.Max(1, _ruleFirstStack);
            serializedAsset.FindProperty("requiredSecondStack").intValue = Mathf.Max(1, _ruleSecondStack);
            serializedAsset.FindProperty("priority").intValue = _rulePriority;

            SerializedProperty effectsProperty = serializedAsset.FindProperty("reactionEffects");
            effectsProperty.arraySize = ShouldCreateInitialReactionEffect() ? 1 : 0;

            if (effectsProperty.arraySize > 0)
            {
                SerializedProperty effectProperty = effectsProperty.GetArrayElementAtIndex(0);
                ConfigureReactionEffectProperty(effectProperty);
            }

            serializedAsset.ApplyModifiedPropertiesWithoutUndo();

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

        private void ConfigureReactionEffectProperty(SerializedProperty effectProperty)
        {
            effectProperty.FindPropertyRelative("effectType").intValue = (int)_ruleEffectType;
            effectProperty.FindPropertyRelative("tag").objectReferenceValue = _ruleEffectTag;
            effectProperty.FindPropertyRelative("duration").floatValue = Mathf.Max(0f, _ruleEffectDuration);
            effectProperty.FindPropertyRelative("stackCount").intValue = Mathf.Max(1, _ruleEffectStackCount);
            effectProperty.FindPropertyRelative("damageAmount").intValue = Mathf.Max(0, _ruleEffectDamageAmount);
            effectProperty.FindPropertyRelative("damageType").intValue = (int)_ruleEffectDamageType;
            effectProperty.FindPropertyRelative("value").floatValue = 0f;
            effectProperty.FindPropertyRelative("radius").floatValue = 0f;
            effectProperty.FindPropertyRelative("effectId").stringValue = _ruleEffectId?.Trim();
        }

        private bool ShouldCreateInitialReactionEffect()
        {
            if (UsesTag(_ruleEffectType))
            {
                return _ruleEffectTag != null;
            }

            if (_ruleEffectType == CombatTagReactionEffectType.DealDamage)
            {
                return _ruleEffectDamageAmount > 0;
            }

            if (_ruleEffectType == CombatTagReactionEffectType.CustomEvent)
            {
                return !string.IsNullOrWhiteSpace(_ruleEffectId);
            }

            return true;
        }

        private static bool UsesTag(CombatTagReactionEffectType effectType)
        {
            return effectType == CombatTagReactionEffectType.ApplyTag
                || effectType == CombatTagReactionEffectType.RemoveTag
                || effectType == CombatTagReactionEffectType.SpreadTag;
        }

        private static string GetAssetSummary(UnityEngine.Object asset)
        {
            if (asset is CombatActionDefinition action)
            {
                return $"{action.ActionType} / {action.DamageType} / flat {action.FlatDamage:0.#} / x{action.AtkPowerMultiplier:0.##}";
            }

            if (asset is CombatTagDefinition tag)
            {
                return $"{tag.TagId} / Lv.{tag.TagLevel} / max {tag.MaxStackCount} / {tag.DefaultDuration:0.#}s";
            }

            if (asset is CombatTagCombinationRule rule)
            {
                string firstTag = rule.FirstTag != null ? rule.FirstTag.TagId : "None";
                string secondTag = rule.SecondTag != null ? rule.SecondTag.TagId : "None";
                int effectCount = rule.ReactionEffects != null ? rule.ReactionEffects.Count : 0;
                return $"{firstTag} + {secondTag} / P{rule.Priority} / effects {effectCount}";
            }

            return string.Empty;
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
