using System.Collections.Generic;
using EndLink.Ally;
using EndLink.Combat;
using UnityEditor;
using UnityEngine;

namespace EndLink.Editor
{
    /// <summary>
    /// 队友状态与行为监视窗口。
    /// 用于集中观察队友状态机、助战目标、攻击距离、冷却和关键 AI 行为日志。
    /// </summary>
    public sealed class AllyMonitorWindow : EditorWindow
    {
        private const int DefaultCapacity = 200;

        private static readonly Vector2 DefaultMinSize = new(900f, 460f);

        private readonly List<AllyDebugRecord> _records = new(DefaultCapacity);
        private Vector2 _statusScroll;
        private Vector2 _logScroll;
        private bool _isPaused;
        private bool _autoScroll = true;
        private int _capacity = DefaultCapacity;
        private GameObject _selectedAlly;
        private AllyDebugCategory _categoryFilter = AllyDebugCategory.All;

        [MenuItem("EndLink/Debug/Ally Monitor")]
        public static void Open()
        {
            AllyMonitorWindow window = GetWindow<AllyMonitorWindow>("Ally Monitor");
            window.minSize = DefaultMinSize;
            window.Show();
        }

        private void OnEnable()
        {
            AllyDebugLog.Raised += OnAllyDebugRaised;
        }

        private void OnDisable()
        {
            AllyDebugLog.Raised -= OnAllyDebugRaised;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawFilters();
            EditorGUILayout.Space(4f);
            DrawStatusSection();
            EditorGUILayout.Space(6f);
            DrawLogSection();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                AllyDebugLog.CaptureEnabled = GUILayout.Toggle(
                    AllyDebugLog.CaptureEnabled,
                    "Capture",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(70f));

                AllyDebugLog.MirrorToConsole = GUILayout.Toggle(
                    AllyDebugLog.MirrorToConsole,
                    "Console",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(68f));

                _isPaused = GUILayout.Toggle(_isPaused, "Pause", EditorStyles.toolbarButton, GUILayout.Width(58f));
                _autoScroll = GUILayout.Toggle(_autoScroll, "Auto Scroll", EditorStyles.toolbarButton, GUILayout.Width(88f));

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                {
                    _records.Clear();
                }

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    Repaint();
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Capacity", GUILayout.Width(52f));
                _capacity = EditorGUILayout.IntSlider(_capacity, 50, 1000, GUILayout.Width(190f));
                TrimToCapacity();
            }
        }

        private void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Ally", EditorStyles.boldLabel, GUILayout.Width(42f));
                _selectedAlly = (GameObject)EditorGUILayout.ObjectField(
                    _selectedAlly,
                    typeof(GameObject),
                    true,
                    GUILayout.Width(220f));

                EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel, GUILayout.Width(78f));
                _categoryFilter = (AllyDebugCategory)EditorGUILayout.EnumFlagsField(_categoryFilter);
            }
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.LabelField("Ally Status", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawColumn("Name", 140f, EditorStyles.boldLabel);
                DrawColumn("State", 70f, EditorStyles.boldLabel);
                DrawColumn("Follow", 120f, EditorStyles.boldLabel);
                DrawColumn("Assist Target", 130f, EditorStyles.boldLabel);
                DrawColumn("Surface", 64f, EditorStyles.boldLabel);
                DrawColumn("Enter", 58f, EditorStyles.boldLabel);
                DrawColumn("Stop", 58f, EditorStyles.boldLabel);
                DrawColumn("Reengage", 70f, EditorStyles.boldLabel);
                DrawColumn("CD", 46f, EditorStyles.boldLabel);
                DrawColumn("Action", 150f, EditorStyles.boldLabel);
                DrawColumn("Select", 58f, EditorStyles.boldLabel);
            }

            _statusScroll = EditorGUILayout.BeginScrollView(_statusScroll, GUILayout.Height(145f));

            AllyStateMachine[] allies = Object.FindObjectsByType<AllyStateMachine>(FindObjectsSortMode.None);

            if (allies.Length == 0)
            {
                EditorGUILayout.HelpBox("No AllyStateMachine found in the open scene.", MessageType.Info);
            }

            foreach (AllyStateMachine ally in allies)
            {
                if (ally == null)
                {
                    continue;
                }

                DrawAllyStatusRow(ally);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAllyStatusRow(AllyStateMachine ally)
        {
            AllyCombatDriver combatDriver = ally.CombatDriver;
            Transform assistTarget = ally.CurrentAssistTarget;
            Transform followTarget = ally.FollowTarget;
            CombatActionDefinition action = combatDriver != null ? combatDriver.AssistAction : null;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawColumn(ally.name, 140f);
                DrawColumn(ally.CurrentStateId.ToString(), 70f);
                DrawColumn(GetTransformName(followTarget), 120f);
                DrawColumn(GetTransformName(assistTarget), 130f);
                DrawColumn(GetSurfaceDistanceText(ally.transform, assistTarget), 64f);
                DrawColumn(ally.AssistAttackEnterDistance.ToString("F2"), 58f);
                DrawColumn(ally.AssistApproachStopDistance.ToString("F2"), 58f);
                DrawColumn(ally.AssistReengageRange.ToString("F2"), 70f);
                DrawColumn(combatDriver != null ? combatDriver.AssistCooldownRemaining.ToString("F2") : "-", 46f);
                DrawColumn(action != null ? action.ActionId : "None", 150f);

                if (GUILayout.Button("Select", GUILayout.Width(58f)))
                {
                    Selection.activeObject = ally.gameObject;
                    EditorGUIUtility.PingObject(ally.gameObject);
                }
            }
        }

        private void DrawLogSection()
        {
            EditorGUILayout.LabelField("Ally Behavior Log", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawColumn("Time", 56f, EditorStyles.boldLabel);
                DrawColumn("Frame", 54f, EditorStyles.boldLabel);
                DrawColumn("Category", 78f, EditorStyles.boldLabel);
                DrawColumn("Ally", 140f, EditorStyles.boldLabel);
                DrawColumn("Message", 520f, EditorStyles.boldLabel);
            }

            _logScroll = EditorGUILayout.BeginScrollView(_logScroll);

            if (_records.Count == 0)
            {
                EditorGUILayout.HelpBox("No ally debug logs captured. Enter Play Mode and trigger ally behavior.", MessageType.Info);
            }

            foreach (AllyDebugRecord record in _records)
            {
                if (!ShouldShow(record))
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSelectableColumn(record.TimeText, 56f);
                    DrawSelectableColumn(record.FrameText, 54f);
                    DrawSelectableColumn(record.CategoryText, 78f);
                    DrawSelectableColumn(record.AllyName, 140f);
                    DrawSelectableColumn(record.Message);
                }
            }

            if (_autoScroll && Event.current.type == EventType.Repaint)
            {
                _logScroll.y = float.MaxValue;
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnAllyDebugRaised(AllyDebugEntry entry)
        {
            if (_isPaused)
            {
                return;
            }

            _records.Add(new AllyDebugRecord(entry));
            TrimToCapacity();
            Repaint();
        }

        private void TrimToCapacity()
        {
            int overflowCount = _records.Count - Mathf.Max(1, _capacity);

            if (overflowCount > 0)
            {
                _records.RemoveRange(0, overflowCount);
            }
        }

        private bool ShouldShow(AllyDebugRecord record)
        {
            if (_selectedAlly != null && record.Ally != _selectedAlly)
            {
                return false;
            }

            return (_categoryFilter & record.Category) != 0;
        }

        private string GetSurfaceDistanceText(Transform from, Transform target)
        {
            if (from == null || target == null)
            {
                return "-";
            }

            float distance = CombatTargetUtility.GetSurfaceDistance(
                target,
                from.position);

            return distance.ToString("F2");
        }

        private static string GetTransformName(Transform target)
        {
            return target != null ? target.name : "None";
        }

        private static void DrawColumn(string text, float width)
        {
            DrawColumn(text, width, EditorStyles.label);
        }

        private static void DrawColumn(string text, float width, GUIStyle style)
        {
            EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
        }

        private static void DrawSelectableColumn(string text, float width)
        {
            EditorGUILayout.SelectableLabel(
                text,
                EditorStyles.label,
                GUILayout.Width(width),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private static void DrawSelectableColumn(string text)
        {
            EditorGUILayout.SelectableLabel(
                text,
                EditorStyles.label,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private readonly struct AllyDebugRecord
        {
            public AllyDebugRecord(AllyDebugEntry entry)
            {
                TimeText = entry.TimeStamp.ToString("0.00");
                FrameText = entry.Frame.ToString();
                Category = entry.Category;
                CategoryText = entry.Category.ToString();
                Ally = entry.Ally;
                AllyName = entry.Ally != null ? entry.Ally.name : "None";
                Message = entry.Message;
            }

            public string TimeText { get; }

            public string FrameText { get; }

            public AllyDebugCategory Category { get; }

            public string CategoryText { get; }

            public GameObject Ally { get; }

            public string AllyName { get; }

            public string Message { get; }
        }
    }
}
