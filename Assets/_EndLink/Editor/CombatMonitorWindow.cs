using System.Collections.Generic;
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

        public static readonly Vector2 DefaultMinSize = new(760f, 360f);

        private readonly List<CombatEventRecord> _records = new(DefaultCapacity);
        private Vector2 _scrollPosition;
        private bool _isPaused;
        private bool _autoScroll = true;
        private bool _showActionStarted = true;
        private bool _showHitLanded = true;
        private bool _showDamaged = true;
        private bool _showDead = true;
        private bool _showTagEvents = true;
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
                _isPaused = GUILayout.Toggle(_isPaused, "Pause", EditorStyles.toolbarButton, GUILayout.Width(64f));
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto Scroll", EditorStyles.toolbarButton, GUILayout.Width(88f));

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                {
                    _records.Clear();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Capacity", GUILayout.Width(52f));
                _capacity = EditorGUILayout.IntSlider(_capacity, 20, 500, GUILayout.Width(180f));
                TrimToCapacity();
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel, GUILayout.Width(52f));
                _showActionStarted = GUILayout.Toggle(_showActionStarted, "Action", EditorStyles.miniButtonLeft);
                _showHitLanded = GUILayout.Toggle(_showHitLanded, "Hit", EditorStyles.miniButtonMid);
                _showDamaged = GUILayout.Toggle(_showDamaged, "Damage", EditorStyles.miniButtonMid);
                _showDead = GUILayout.Toggle(_showDead, "Dead", EditorStyles.miniButtonMid);
                _showTagEvents = GUILayout.Toggle(_showTagEvents, "Tags", EditorStyles.miniButtonRight);
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawColumn("Time", 58f, EditorStyles.boldLabel);
                DrawColumn("Type", 112f, EditorStyles.boldLabel);
                DrawColumn("Source", 150f, EditorStyles.boldLabel);
                DrawColumn("Target", 150f, EditorStyles.boldLabel);
                DrawColumn("Action", 150f, EditorStyles.boldLabel);
                DrawColumn("Tag", 120f, EditorStyles.boldLabel);
                DrawColumn("Damage", 64f, EditorStyles.boldLabel);
                DrawColumn("Dmg Type", 112f, EditorStyles.boldLabel);
            }
        }

        private void DrawEventList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_records.Count == 0)
            {
                EditorGUILayout.HelpBox("No combat events captured. Enter Play Mode and trigger combat actions.", MessageType.Info);
            }

            foreach (CombatEventRecord record in _records)
            {
                if (!ShouldShow(record.EventType))
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawColumn(record.TimeText, 58f);
                    DrawColumn(record.EventType.ToString(), 112f);
                    DrawColumn(record.SourceName, 150f);
                    DrawColumn(record.TargetName, 150f);
                    DrawColumn(record.ActionId, 150f);
                    DrawColumn(record.TagId, 120f);
                    DrawColumn(record.DamageText, 64f);
                    DrawColumn(record.DamageTypeText, 112f);
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
            DrawColumn(text, width, EditorStyles.label);
        }

        private static void DrawColumn(string text, float width, GUIStyle style)
        {
            EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
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
                CombatEventType.HitLanded => _showHitLanded,
                CombatEventType.Damaged => _showDamaged,
                CombatEventType.Dead => _showDead,
                CombatEventType.TagAdded => _showTagEvents,
                CombatEventType.TagRemoved => _showTagEvents,
                CombatEventType.TagExpired => _showTagEvents,
                CombatEventType.ReactionTriggered => _showTagEvents,
                _ => true
            };
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
                DamageText = eventData.DamageAmount > 0f ? eventData.DamageAmount.ToString("0.#") : "-";
                DamageTypeText = eventData.DamageAmount > 0f ? eventData.DamageType.ToString() : "-";
            }

            public string TimeText { get; }

            public CombatEventType EventType { get; }

            public string SourceName { get; }

            public string TargetName { get; }

            public string ActionId { get; }

            public string TagId { get; }

            public string DamageText { get; }

            public string DamageTypeText { get; }

            private static string GetObjectName(Object targetObject)
            {
                return targetObject != null ? targetObject.name : "None";
            }
        }
    }
}
