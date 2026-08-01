using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            "动作",
            "标签定义",
            "组合规则",
            "资源列表"
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
        private float _ruleEffectValue;
        private float _ruleEffectRadius;
        private string _ruleEffectId;

        [MenuItem("EndLink/Combat Data Tool")]
        public static void Open()
        {
            CombatDataToolWindow window = GetWindow<CombatDataToolWindow>("Combat Data Tool");
            window.minSize = new Vector2(760f, 420f);
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
            EditorGUILayout.LabelField("快捷创建、浏览并检查战斗数据；完整字段继续在 Inspector 中编辑。");
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
            DrawFolderField("目录", ActionsFolder);
            GUI.SetNextControlName("EndLink.CombatDataTool.ActionAssetName");
            _actionAssetName = EditorGUILayout.TextField("资源名称", _actionAssetName);
            _actionType = (CombatActionType)EditorGUILayout.EnumPopup("动作类型", _actionType);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("创建动作", GUILayout.Height(28f)))
                {
                    CreateCombatActionAsset();
                }

                if (GUILayout.Button("定位目录", GUILayout.Height(28f)))
                {
                    SelectFolder(ActionsFolder);
                }
            }
        }

        private void DrawTagDefinitionCreator()
        {
            DrawFolderField("目录", TagDefinitionsFolder);
            GUI.SetNextControlName("EndLink.CombatDataTool.TagDefinitionAssetName");
            _tagAssetName = EditorGUILayout.TextField("资源名称", _tagAssetName);
            _tagId = EditorGUILayout.TextField("Tag Id", _tagId);
            _tagDisplayName = EditorGUILayout.TextField("显示名称", _tagDisplayName);
            _tagLevel = Mathf.Max(1, EditorGUILayout.IntField("标签等级", _tagLevel));
            _tagDefaultDuration = Mathf.Max(0f, EditorGUILayout.FloatField("默认持续时间", _tagDefaultDuration));
            _tagMaxStackCount = Mathf.Max(1, EditorGUILayout.IntField("最大层数", _tagMaxStackCount));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("创建标签", GUILayout.Height(28f)))
                {
                    CreateCombatTagDefinitionAsset();
                }

                if (GUILayout.Button("定位目录", GUILayout.Height(28f)))
                {
                    SelectFolder(TagDefinitionsFolder);
                }
            }
        }

        private void DrawCombinationRuleCreator()
        {
            DrawFolderField("目录", TagCombinationRulesFolder);
            GUI.SetNextControlName("EndLink.CombatDataTool.CombinationRuleAssetName");
            _combinationRuleAssetName = EditorGUILayout.TextField("资源名称", _combinationRuleAssetName);
            _ruleFirstTag = (CombatTagDefinition)EditorGUILayout.ObjectField("标签 A", _ruleFirstTag, typeof(CombatTagDefinition), false);
            _ruleFirstStack = Mathf.Max(1, EditorGUILayout.IntField("标签 A 所需层数", _ruleFirstStack));
            _ruleSecondTag = (CombatTagDefinition)EditorGUILayout.ObjectField("标签 B", _ruleSecondTag, typeof(CombatTagDefinition), false);
            _ruleSecondStack = Mathf.Max(1, EditorGUILayout.IntField("标签 B 所需层数", _ruleSecondStack));
            _rulePriority = EditorGUILayout.IntField("优先级", _rulePriority);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("第一条反应效果", EditorStyles.boldLabel);
            _ruleEffectType = (CombatTagReactionEffectType)EditorGUILayout.EnumPopup("效果类型", _ruleEffectType);

            if (UsesTag(_ruleEffectType))
            {
                _ruleEffectTag = (CombatTagDefinition)EditorGUILayout.ObjectField("效果标签", _ruleEffectTag, typeof(CombatTagDefinition), false);
                _ruleEffectDuration = Mathf.Max(0f, EditorGUILayout.FloatField("持续时间", _ruleEffectDuration));
                _ruleEffectStackCount = Mathf.Max(1, EditorGUILayout.IntField("标签层数", _ruleEffectStackCount));
            }

            if (_ruleEffectType == CombatTagReactionEffectType.DealDamage)
            {
                _ruleEffectDamageAmount = Mathf.Max(0, EditorGUILayout.IntField("伤害值", _ruleEffectDamageAmount));
                _ruleEffectDamageType = (CombatDamageType)EditorGUILayout.EnumPopup("伤害类型", _ruleEffectDamageType);
            }

            if (UsesEffectId(_ruleEffectType))
            {
                _ruleEffectId = EditorGUILayout.TextField("效果 ID", _ruleEffectId);
            }

            if (UsesValue(_ruleEffectType))
            {
                _ruleEffectValue = EditorGUILayout.FloatField("效果数值", _ruleEffectValue);
            }

            if (UsesRadius(_ruleEffectType))
            {
                _ruleEffectRadius = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField("效果半径", _ruleEffectRadius));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("创建组合规则", GUILayout.Height(28f)))
                {
                    CreateCombatTagCombinationRuleAsset();
                }

                if (GUILayout.Button("定位目录", GUILayout.Height(28f)))
                {
                    SelectFolder(TagCombinationRulesFolder);
                }
            }
        }

        private void DrawAssetList()
        {
            HashSet<string> duplicateActionIds = GetDuplicateIds(
                ActionsFolder,
                (CombatActionDefinition action) => action.ActionId);
            HashSet<string> duplicateTagIds = GetDuplicateIds(
                TagDefinitionsFolder,
                (CombatTagDefinition tag) => tag.TagId);

            DrawAssetSection<CombatActionDefinition>("动作", ActionsFolder, duplicateActionIds);
            EditorGUILayout.Space(10f);
            DrawAssetSection<CombatTagDefinition>("标签定义", TagDefinitionsFolder, duplicateTagIds);
            EditorGUILayout.Space(10f);
            DrawAssetSection<CombatTagCombinationRule>("组合规则", TagCombinationRulesFolder, null);
        }

        private static void DrawAssetSection<T>(
            string title,
            string folder,
            ISet<string> duplicateIds)
            where T : UnityEngine.Object
        {
            EnsureFolder(folder);

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            EditorGUILayout.LabelField($"{title} ({guids.Length})", EditorStyles.boldLabel);

            if (guids.Length == 0)
            {
                EditorGUILayout.HelpBox("该目录中没有对应资源。", MessageType.Info);
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
                    string validationMessage = GetAssetValidationMessage(asset, duplicateIds);
                    GUIContent statusContent = string.IsNullOrWhiteSpace(validationMessage)
                        ? EditorGUIUtility.IconContent("TestPassed")
                        : EditorGUIUtility.IconContent("console.warnicon.sml");
                    statusContent.tooltip = string.IsNullOrWhiteSpace(validationMessage)
                        ? "基础数据合法"
                        : validationMessage;

                    GUILayout.Label(statusContent, GUILayout.Width(20f), GUILayout.Height(18f));
                    EditorGUILayout.ObjectField(asset, typeof(T), false, GUILayout.Width(210f));
                    EditorGUILayout.SelectableLabel(
                        GetAssetSummary(asset),
                        EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight),
                        GUILayout.MinWidth(350f));

                    if (GUILayout.Button("定位", GUILayout.Width(56f)))
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
            effectProperty.FindPropertyRelative("value").floatValue = _ruleEffectValue;
            effectProperty.FindPropertyRelative("radius").floatValue = Mathf.Max(0f, _ruleEffectRadius);
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

        private static bool UsesEffectId(CombatTagReactionEffectType effectType)
        {
            return effectType == CombatTagReactionEffectType.ApplyControl
                || effectType == CombatTagReactionEffectType.InterruptAction
                || effectType == CombatTagReactionEffectType.ModifyResource
                || effectType == CombatTagReactionEffectType.CustomEvent;
        }

        private static bool UsesValue(CombatTagReactionEffectType effectType)
        {
            return effectType == CombatTagReactionEffectType.ApplyControl
                || effectType == CombatTagReactionEffectType.ModifyResource
                || effectType == CombatTagReactionEffectType.CustomEvent;
        }

        private static bool UsesRadius(CombatTagReactionEffectType effectType)
        {
            return effectType == CombatTagReactionEffectType.SpreadTag
                || effectType == CombatTagReactionEffectType.ApplyControl;
        }

        private static string GetAssetSummary(UnityEngine.Object asset)
        {
            if (asset is CombatActionDefinition action)
            {
                string synergySummary = action.ActionType == CombatActionType.LinkAttack
                    ? $" / 协同 {action.SynergyGainOnLink:0.#}"
                    : string.Empty;
                string rootMotionSummary = action.UseRootMotion
                    ? $" / RM x{action.RootMotionScale:0.##}"
                    : string.Empty;
                string feedbackSummary = action.HitFeedback != null ? " / 反馈" : string.Empty;
                string hitboxSummary = action.HitboxPrefab != null ? " / Hitbox" : " / 无 Hitbox";
                return $"{action.ActionType} / {action.DamageType} / {action.FlatDamage:0.#}+ATKx{action.AtkPowerMultiplier:0.##} / Hit {action.HitStrength:0.#} / Balance {action.BalanceDamage:0.#} / {action.TimingSource}{rootMotionSummary}{feedbackSummary}{hitboxSummary}{synergySummary}";
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
                return $"{firstTag} x{rule.RequiredFirstStack} + {secondTag} x{rule.RequiredSecondStack} / P{rule.Priority} / effects {effectCount}";
            }

            return string.Empty;
        }

        private static string GetAssetValidationMessage(
            UnityEngine.Object asset,
            ISet<string> duplicateIds)
        {
            if (asset is CombatActionDefinition action)
            {
                if (string.IsNullOrWhiteSpace(action.ActionId))
                {
                    return "Action Id 为空。";
                }

                if (duplicateIds != null && duplicateIds.Contains(action.ActionId))
                {
                    return $"Action Id 重复：{action.ActionId}";
                }

                if (action.ActionType != CombatActionType.Ultimate && action.HitboxPrefab == null)
                {
                    return "当前执行器要求该动作配置 Hitbox Prefab。";
                }

                return string.Empty;
            }

            if (asset is CombatTagDefinition tag)
            {
                if (!tag.IsValid)
                {
                    return "Tag Id 为空。";
                }

                return duplicateIds != null && duplicateIds.Contains(tag.TagId)
                    ? $"Tag Id 重复：{tag.TagId}"
                    : string.Empty;
            }

            if (asset is CombatTagCombinationRule rule && !rule.IsValid)
            {
                return "组合规则缺少输入标签，或没有至少一条有效反应效果。";
            }

            return string.Empty;
        }

        private static HashSet<string> GetDuplicateIds<T>(
            string folder,
            Func<T, string> idSelector)
            where T : UnityEngine.Object
        {
            EnsureFolder(folder);
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .Select(idSelector)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .GroupBy(id => id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
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
