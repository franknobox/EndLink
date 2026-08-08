using System.Collections.Generic;
using System.Text;
using EndLink.Combat;
using UnityEditor;
using UnityEngine;

namespace EndLink.Editor
{
    /// <summary>
    /// 战斗事件监视窗口。
    /// 订阅 CombatEventsBus，在 Editor 中查看事件流，避免把白模调试信息全部打到 Console。
    /// </summary>
    public sealed class CombatMonitorWindow : EditorWindow
    {
        public const int DefaultCapacity = 100;

        public static readonly Vector2 DefaultMinSize = new(920f, 380f);

        private readonly List<CombatEventRecord> _records = new(DefaultCapacity);
        private Vector2 _scrollPosition;
        private bool _isPaused;
        private bool _autoScroll = true;
        private bool _showActionStarted = true;
        private bool _showHitResolved = true;
        private bool _showHitLanded = true;
        private bool _showDamaged = true;
        private bool _showDead = true;
        private bool _showTagEvents = true;
        private bool _showReactionEvents = true;
        private int _capacity = DefaultCapacity;

        [MenuItem("EndLink/Debug/Combat Monitor")]
        public static void Open()
        {
            CombatMonitorWindow window = GetWindow<CombatMonitorWindow>("Combat Monitor");
            window.minSize = DefaultMinSize;
            window.Show();
        }

        private void OnEnable()
        {
            CombatEventsBus.Raised += OnCombatEventRaised;
        }

        private void OnDisable()
        {
            CombatEventsBus.Raised -= OnCombatEventRaised;
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawFilters();
            EditorGUILayout.Space(4f);
            DrawHeader();
            DrawEventList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _isPaused = GUILayout.Toggle(_isPaused, "暂停", EditorStyles.toolbarButton, GUILayout.Width(56f));
                _autoScroll = GUILayout.Toggle(_autoScroll, "自动滚动", EditorStyles.toolbarButton, GUILayout.Width(72f));

                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                {
                    _records.Clear();
                }

                using (new EditorGUI.DisabledScope(_records.Count == 0))
                {
                    if (GUILayout.Button("复制", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildReport();
                        ShowNotification(new GUIContent("战斗事件已复制"));
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("容量", GUILayout.Width(32f));
                _capacity = EditorGUILayout.IntSlider(_capacity, 20, 500, GUILayout.Width(180f));
                TrimToCapacity();
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("筛选", EditorStyles.boldLabel, GUILayout.Width(40f));
                _showActionStarted = GUILayout.Toggle(_showActionStarted, "动作", EditorStyles.miniButtonLeft);
                _showHitResolved = GUILayout.Toggle(_showHitResolved, "结算", EditorStyles.miniButtonMid);
                _showHitLanded = GUILayout.Toggle(_showHitLanded, "成立命中", EditorStyles.miniButtonMid);
                _showDamaged = GUILayout.Toggle(_showDamaged, "伤害", EditorStyles.miniButtonMid);
                _showDead = GUILayout.Toggle(_showDead, "死亡", EditorStyles.miniButtonMid);
                _showTagEvents = GUILayout.Toggle(_showTagEvents, "标签", EditorStyles.miniButtonMid);
                _showReactionEvents = GUILayout.Toggle(_showReactionEvents, "协议反应", EditorStyles.miniButtonRight);
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawHeaderColumn("时间", 52f);
                DrawHeaderColumn("类型", 112f);
                DrawHeaderColumn("来源", 130f);
                DrawHeaderColumn("目标", 130f);
                DrawHeaderColumn("动作 / 规则", 150f);
                DrawHeaderColumn("标签", 105f);
                DrawHeaderColumn("层数", 44f);
                DrawHeaderColumn("伤害", 58f);
                DrawHeaderColumn("伤害类型", 105f);
                DrawHeaderColumn("结算结果", 86f);
            }
        }

        private void DrawEventList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_records.Count == 0)
            {
                EditorGUILayout.HelpBox("尚未捕获战斗事件。进入 Play Mode 并触发战斗行为后会显示。", MessageType.Info);
            }

            foreach (CombatEventRecord record in _records)
            {
                if (!ShouldShow(record.EventType))
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawColumn(record.TimeText, 52f);
                    DrawColumn(record.EventType.ToString(), 112f);
                    DrawColumn(record.SourceName, 130f);
                    DrawColumn(record.TargetName, 130f);
                    DrawColumn(record.ActionId, 150f);
                    DrawColumn(record.TagId, 105f);
                    DrawColumn(record.StackText, 44f);
                    DrawColumn(record.DamageText, 58f);
                    DrawColumn(record.DamageTypeText, 105f);
                    DrawColumn(record.HitOutcomeText, 86f);
                }
            }

            if (_autoScroll && Event.current.type == EventType.Repaint)
            {
                _scrollPosition.y = float.MaxValue;
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawColumn(string text, float width)
        {
            EditorGUILayout.SelectableLabel(
                text,
                EditorStyles.label,
                GUILayout.Width(width),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private static void DrawHeaderColumn(string text, float width)
        {
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel, GUILayout.Width(width));
        }

        private void OnCombatEventRaised(CombatEvent eventData)
        {
            if (_isPaused)
            {
                return;
            }

            _records.Add(new CombatEventRecord(eventData));
            TrimToCapacity();
            Repaint();
        }

        private void TrimToCapacity()
        {
            int overflowCount = _records.Count - Mathf.Max(1, _capacity);

            if (overflowCount <= 0)
            {
                return;
            }

            _records.RemoveRange(0, overflowCount);
        }

        private bool ShouldShow(CombatEventType eventType)
        {
            return eventType switch
            {
                CombatEventType.ActionStarted => _showActionStarted,
                CombatEventType.HitResolved => _showHitResolved,
                CombatEventType.HitLanded => _showHitLanded,
                CombatEventType.Damaged => _showDamaged,
                CombatEventType.Dead => _showDead,
                CombatEventType.TagAdded => _showTagEvents,
                CombatEventType.TagRemoved => _showTagEvents,
                CombatEventType.TagExpired => _showTagEvents,
                CombatEventType.ReactionTriggered => _showReactionEvents,
                _ => true
            };
        }

        private string BuildReport()
        {
            StringBuilder builder = new();
            builder.AppendLine("EndLink Combat Monitor");
            foreach (CombatEventRecord record in _records)
            {
                builder.Append('[')
                    .Append(record.TimeText)
                    .Append("] ")
                    .Append(record.EventType)
                    .Append(" | ")
                    .Append(record.SourceName)
                    .Append(" -> ")
                    .Append(record.TargetName)
                    .Append(" | Action/Rule=")
                    .Append(record.ActionId)
                    .Append(" | Tag=")
                    .Append(record.TagId)
                    .Append(" x")
                    .Append(record.StackText)
                    .Append(" | Damage=")
                    .Append(record.DamageText)
                    .Append(' ')
                    .Append(record.DamageTypeText)
                    .Append(" | HitOutcome=")
                    .AppendLine(record.HitOutcomeText);
            }

            return builder.ToString();
        }

        private readonly struct CombatEventRecord
        {
            public CombatEventRecord(CombatEvent eventData)
            {
                TimeText = eventData.TimeStamp.ToString("0.00");
                EventType = eventData.EventType;
                SourceName = GetObjectName(eventData.Source);
                TargetName = GetObjectName(eventData.Target);
                ActionId = eventData.ActionDefinition != null
                    ? eventData.ActionDefinition.ActionId
                    : eventData.ReactionRule != null
                        ? eventData.ReactionRule.name
                        : "None";
                TagId = eventData.CombatTag != null ? eventData.CombatTag.TagId : "None";
                StackText = eventData.CombatTagStackCount > 0
                    ? eventData.CombatTagStackCount.ToString()
                    : "-";
                DamageText = eventData.DamageAmount > 0f ? eventData.DamageAmount.ToString("0.#") : "-";
                DamageTypeText = eventData.DamageAmount > 0f ? eventData.DamageType.ToString() : "-";
                HitOutcomeText = eventData.HasHitResolution
                    ? eventData.HitResolution.Outcome.ToString()
                    : "-";
            }

            public string TimeText { get; }

            public CombatEventType EventType { get; }

            public string SourceName { get; }

            public string TargetName { get; }

            public string ActionId { get; }

            public string TagId { get; }

            public string StackText { get; }

            public string DamageText { get; }

            public string DamageTypeText { get; }

            public string HitOutcomeText { get; }

            private static string GetObjectName(Object targetObject)
            {
                return targetObject != null ? targetObject.name : "None";
            }
        }
    }
}
