using EndLink.Combat;
using UnityEditor;
using UnityEngine;

namespace EndLink.Editor
{
    /// <summary>
    /// CharacterStats 的精简 Inspector。
    /// 当前只显示通用属性；后续出现角色专属字段时，再按 ResolvedType 绘制对应配置区。
    /// </summary>
    [CustomEditor(typeof(CharacterStats))]
    public sealed class CharacterStatsEditor : UnityEditor.Editor
    {
        private static readonly GUIContent StatsTypeLabel = new(
            "角色类型",
            "自动识别玩家、队友或敌人，也可以手动指定类型。");

        private static readonly string[] StatsTypeOptions =
        {
            "自动识别",
            "玩家",
            "队友",
            "敌人"
        };

        private SerializedProperty _statsTypeProperty;
        private SerializedProperty _baseAttackPowerProperty;

        private void OnEnable()
        {
            _statsTypeProperty = serializedObject.FindProperty("statsType");
            _baseAttackPowerProperty = serializedObject.FindProperty("baseAttackPower");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            int selectedType = EditorGUILayout.Popup(
                StatsTypeLabel,
                _statsTypeProperty.enumValueIndex,
                StatsTypeOptions);

            if (EditorGUI.EndChangeCheck())
            {
                _statsTypeProperty.enumValueIndex = selectedType;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            DrawResolvedType();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("通用属性", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                _baseAttackPowerProperty,
                new GUIContent("基础攻击力", "角色未经成长、装备、Buff 或 Debuff 修正的攻击力。"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawResolvedType()
        {
            CharacterStats stats = (CharacterStats)target;
            CharacterStatsType resolvedType = stats.ResolvedType;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("当前类型", GetTypeDisplayName(resolvedType));
            }

            if (stats.ConfiguredType == CharacterStatsType.Auto
                && resolvedType == CharacterStatsType.Auto)
            {
                EditorGUILayout.HelpBox(
                    "未找到玩家状态机、队友状态机或 EnemyActor。请确认 CharacterStats 位于角色根物体层级，或手动选择角色类型。",
                    MessageType.Warning);
            }
        }

        private static string GetTypeDisplayName(CharacterStatsType statsType)
        {
            return statsType switch
            {
                CharacterStatsType.Player => "玩家",
                CharacterStatsType.Ally => "队友",
                CharacterStatsType.Enemy => "敌人",
                _ => "未识别"
            };
        }
    }
}
