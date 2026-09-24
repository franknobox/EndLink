using System;
using System.Collections.Generic;
using System.Linq;
using EndLink.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EndLink.Editor
{
    /// <summary>
    /// Compares one Combat Action with its clip and Animator state, then previews spatial data in Scene View.
    /// The first version is diagnostic-only and never rewrites imported clips or controllers.
    /// </summary>
    internal sealed class ActionAnimationCalibrationWindow : EditorWindow
    {
        private const string MenuPath = "EndLink/Combat/Action Animation Calibration";

        private static readonly Color StartupColor = new(0.35f, 0.55f, 0.9f, 1f);
        private static readonly Color ActiveColor = new(0.95f, 0.38f, 0.18f, 1f);
        private static readonly Color RecoveryColor = new(0.42f, 0.7f, 0.46f, 1f);
        private static readonly Color ClipColor = new(0.72f, 0.72f, 0.72f, 1f);

        private CombatActionDefinition _action;
        private AnimationClip _clip;
        private AnimatorController _controller;
        private Transform _previewRoot;
        private List<AnimatorStateRecord> _states = new();
        private List<CalibrationIssue> _issues = new();
        private int _stateIndex = -1;
        private Vector2 _scrollPosition;
        private bool _drawScenePreview = true;
        private bool _showNonCombatEvents = true;

        private AnimatorStateRecord SelectedState => _stateIndex >= 0 && _stateIndex < _states.Count
            ? _states[_stateIndex]
            : null;

        [MenuItem(MenuPath)]
        internal static void Open()
        {
            ActionAnimationCalibrationWindow window = GetWindow<ActionAnimationCalibrationWindow>(
                "Action Calibration");
            window.minSize = new Vector2(620f, 620f);
            window.Show();
        }

        internal static void Open(CombatActionDefinition action)
        {
            Open();
            ActionAnimationCalibrationWindow window = GetWindow<ActionAnimationCalibrationWindow>();
            window._action = action;
            window.AutoSelectState(false);
            window.RefreshAnalysis();
            window.Focus();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawScenePreview;
            Selection.selectionChanged += HandleSelectionChanged;
            AdoptSelection(false);
            RebuildStates();
            RefreshAnalysis();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawScenePreview;
            Selection.selectionChanged -= HandleSelectionChanged;
        }

        private void OnGUI()
        {
            DrawHeader();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawContext();
            DrawSummary();
            DrawIssues();
            DrawTimeline();
            DrawEventTable();
            DrawAnimatorMapping();
            DrawScenePreviewSettings();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Action 与动画校准", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "对照 Action、Clip 和 Animator State，并在 Scene View 检查判定与位移。第一版只诊断，不自动修改资源。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(5f);
        }

        private void DrawContext()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("校准对象", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                CombatActionDefinition nextAction = (CombatActionDefinition)EditorGUILayout.ObjectField(
                    "Combat Action",
                    _action,
                    typeof(CombatActionDefinition),
                    false);
                AnimationClip nextClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "Animation Clip",
                    _clip,
                    typeof(AnimationClip),
                    false);
                AnimatorController nextController = (AnimatorController)EditorGUILayout.ObjectField(
                    "Animator Controller",
                    _controller,
                    typeof(AnimatorController),
                    false);
                if (EditorGUI.EndChangeCheck())
                {
                    bool actionChanged = nextAction != _action;
                    bool clipChanged = nextClip != _clip;
                    bool controllerChanged = nextController != _controller;
                    _action = nextAction;
                    _clip = nextClip;
                    _controller = nextController;

                    if (controllerChanged)
                    {
                        RebuildStates();
                    }

                    if (clipChanged)
                    {
                        SelectStateUsingClip();
                    }
                    else if (actionChanged || controllerChanged)
                    {
                        AutoSelectState(false);
                    }

                    RefreshAnalysis();
                    SceneView.RepaintAll();
                }

                string[] statePaths = _states.Select(state => state.Path).ToArray();
                using (new EditorGUI.DisabledScope(statePaths.Length == 0))
                {
                    int nextStateIndex = EditorGUILayout.Popup(
                        "Animator State",
                        Mathf.Clamp(_stateIndex, 0, Mathf.Max(0, statePaths.Length - 1)),
                        statePaths.Length > 0 ? statePaths : new[] { "未找到 State" });
                    if (statePaths.Length > 0 && nextStateIndex != _stateIndex)
                    {
                        _stateIndex = nextStateIndex;
                        AnimationClip stateClip = ActionAnimationCalibrationAnalyzer.FindFirstClip(
                            SelectedState.State.motion);
                        if (stateClip != null)
                        {
                            _clip = stateClip;
                        }

                        RefreshAnalysis();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("从当前选择读取"))
                    {
                        AdoptSelection(true);
                    }

                    if (GUILayout.Button("自动匹配 State"))
                    {
                        AutoSelectState(true);
                        RefreshAnalysis();
                    }

                    using (new EditorGUI.DisabledScope(_action == null && _clip == null && _controller == null))
                    {
                        if (GUILayout.Button("定位资源"))
                        {
                            UnityEngine.Object target = (UnityEngine.Object)_clip
                                ?? (UnityEngine.Object)_action
                                ?? _controller;
                            Selection.activeObject = target;
                            EditorGUIUtility.PingObject(target);
                        }
                    }
                }
            }
        }

        private void DrawSummary()
        {
            if (_action == null || _clip == null)
            {
                return;
            }

            float effectiveClipDuration = ActionAnimationCalibrationAnalyzer.GetEffectiveClipDuration(
                _clip,
                SelectedState);
            AnimationEvent[] events = ActionAnimationCalibrationAnalyzer.GetSortedEvents(_clip);
            int hitboxWindows = events.Count(animationEvent =>
                animationEvent.functionName == ActionAnimationCalibrationAnalyzer.HitboxStartEvent);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("时序摘要", EditorStyles.boldLabel);
                DrawSummaryRow("Action", $"{_action.DisplayName}  ({_action.ActionId})");
                DrawSummaryRow("时序来源", _action.TimingSource.ToString());
                DrawSummaryRow(
                    "Action 时长",
                    $"{_action.TotalDuration:0.###}s = {_action.StartupTime:0.###} / "
                    + $"{_action.ActiveTime:0.###} / {_action.RecoveryTime:0.###}");
                DrawSummaryRow(
                    "Clip 时长",
                    Mathf.Approximately(effectiveClipDuration, _clip.length)
                        ? $"{_clip.length:0.###}s"
                        : $"{_clip.length:0.###}s / State 后 {effectiveClipDuration:0.###}s");
                DrawSummaryRow("事件 / 判定窗口", $"{events.Length} / {hitboxWindows}");
                DrawSummaryRow(
                    "Root Motion",
                    _action.UseRootMotion
                        ? $"启用，倍率 {_action.RootMotionScale:0.###}，平均速度 {_clip.averageSpeed.magnitude:0.###}m/s"
                        : "关闭");
            }
        }

        private static void DrawSummaryRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(105f));
                EditorGUILayout.SelectableLabel(
                    value,
                    EditorStyles.miniLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void DrawIssues()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int errorCount = _issues.Count(issue => issue.Severity == CalibrationSeverity.Error);
                int warningCount = _issues.Count(issue => issue.Severity == CalibrationSeverity.Warning);
                EditorGUILayout.LabelField(
                    $"校准结果  错误 {errorCount} / 警告 {warningCount}",
                    EditorStyles.boldLabel);

                if (_issues.Count == 0)
                {
                    EditorGUILayout.HelpBox("当前校准项没有发现问题。", MessageType.Info);
                    return;
                }

                foreach (CalibrationIssue issue in _issues)
                {
                    EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
                }
            }
        }

        private void DrawTimeline()
        {
            if (_action == null || _clip == null)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("动作时间轴", EditorStyles.boldLabel);
            float clipDuration = ActionAnimationCalibrationAnalyzer.GetEffectiveClipDuration(_clip, SelectedState);
            float duration = Mathf.Max(0.01f, Mathf.Max(_action.TotalDuration, clipDuration));
            Rect rect = GUILayoutUtility.GetRect(10f, 126f, GUILayout.ExpandWidth(true));
            DrawTimelineBackground(rect);

            Rect actionTrack = new(rect.x + 8f, rect.y + 24f, rect.width - 16f, 30f);
            float startupWidth = actionTrack.width * (_action.StartupTime / duration);
            float activeWidth = actionTrack.width * (_action.ActiveTime / duration);
            float recoveryWidth = actionTrack.width * (_action.RecoveryTime / duration);
            DrawPhaseRect(new Rect(actionTrack.x, actionTrack.y, startupWidth, actionTrack.height), StartupColor, "前摇");
            DrawPhaseRect(
                new Rect(actionTrack.x + startupWidth, actionTrack.y, activeWidth, actionTrack.height),
                ActiveColor,
                "判定");
            DrawPhaseRect(
                new Rect(actionTrack.x + startupWidth + activeWidth, actionTrack.y, recoveryWidth, actionTrack.height),
                RecoveryColor,
                "后摇");

            Rect clipTrack = new(actionTrack.x, rect.y + 66f, actionTrack.width * (clipDuration / duration), 14f);
            EditorGUI.DrawRect(clipTrack, ClipColor);
            GUI.Label(new Rect(actionTrack.x, clipTrack.y - 18f, 180f, 18f), "Clip / 动画事件", EditorStyles.miniLabel);

            AnimationEvent[] events = ActionAnimationCalibrationAnalyzer.GetSortedEvents(_clip);
            int visibleIndex = 0;
            foreach (AnimationEvent animationEvent in events)
            {
                if (!_showNonCombatEvents && !IsCombatEvent(animationEvent.functionName))
                {
                    continue;
                }

                float normalizedTime = Mathf.Clamp01(animationEvent.time / duration);
                float x = actionTrack.x + actionTrack.width * normalizedTime;
                Color eventColor = GetEventColor(animationEvent.functionName);
                EditorGUI.DrawRect(new Rect(x - 1f, rect.y + 58f, 2f, 50f), eventColor);
                GUI.Label(
                    new Rect(x + 3f, rect.y + 82f + (visibleIndex % 2) * 16f, 150f, 16f),
                    ShortEventName(animationEvent.functionName),
                    EditorStyles.miniLabel);
                visibleIndex++;
            }

            GUI.Label(new Rect(actionTrack.x, rect.y + 108f, 80f, 16f), "0s", EditorStyles.miniLabel);
            GUI.Label(
                new Rect(actionTrack.xMax - 80f, rect.y + 108f, 80f, 16f),
                $"{duration:0.###}s",
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperRight });
        }

        private static void DrawTimelineBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.13f, 1f));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, 220f, 18f), "Action 数据时序", EditorStyles.miniBoldLabel);
        }

        private static void DrawPhaseRect(Rect rect, Color color, string label)
        {
            if (rect.width <= 0f)
            {
                return;
            }

            EditorGUI.DrawRect(rect, color);
            if (rect.width >= 32f)
            {
                GUI.Label(rect, label, new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                });
            }
        }

        private void DrawEventTable()
        {
            if (_clip == null)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("动画事件", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    _showNonCombatEvents = EditorGUILayout.ToggleLeft(
                        "显示非战斗事件",
                        _showNonCombatEvents,
                        GUILayout.Width(120f));
                }

                AnimationEvent[] events = ActionAnimationCalibrationAnalyzer.GetSortedEvents(_clip);
                bool drewEvent = false;
                foreach (AnimationEvent animationEvent in events)
                {
                    bool combatEvent = IsCombatEvent(animationEvent.functionName);
                    if (!_showNonCombatEvents && !combatEvent)
                    {
                        continue;
                    }

                    drewEvent = true;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(
                            combatEvent ? EditorGUIUtility.IconContent("Animation.EventMarker") : GUIContent.none,
                            GUILayout.Width(20f));
                        EditorGUILayout.LabelField($"{animationEvent.time:0.###}s", GUILayout.Width(64f));
                        EditorGUILayout.SelectableLabel(
                            string.IsNullOrWhiteSpace(animationEvent.functionName)
                                ? "<空函数>"
                                : animationEvent.functionName,
                            GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                }

                if (!drewEvent)
                {
                    EditorGUILayout.HelpBox("当前 Clip 没有可显示的动画事件。", MessageType.Info);
                }
            }
        }

        private void DrawAnimatorMapping()
        {
            if (_controller == null)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Animator State 关联", EditorStyles.boldLabel);
                AnimatorStateRecord state = SelectedState;
                if (state == null)
                {
                    EditorGUILayout.HelpBox("当前 Controller 没有可用 State。", MessageType.Warning);
                    return;
                }

                DrawSummaryRow("State", state.Path);
                DrawSummaryRow("Motion", state.State.motion != null ? state.State.motion.name : "未配置");
                DrawSummaryRow(
                    "播放速度",
                    state.State.speedParameterActive
                        ? $"参数：{state.State.speedParameter}"
                        : state.State.speed.ToString("0.###"));

                if (state.IncomingTransitions.Count == 0)
                {
                    EditorGUILayout.HelpBox("没有扫描到进入该 State 的 State/Any State Transition。", MessageType.Warning);
                    return;
                }

                EditorGUILayout.LabelField("入口条件", EditorStyles.miniBoldLabel);
                foreach (AnimatorStateTransition transition in state.IncomingTransitions)
                {
                    string conditions = transition.conditions.Length == 0
                        ? "无条件"
                        : string.Join(
                            "  &  ",
                            transition.conditions.Select(condition =>
                                $"{condition.parameter} {condition.mode} {condition.threshold:0.###}"));
                    EditorGUILayout.LabelField($"• {conditions}", EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawScenePreviewSettings()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene View 空间校准", EditorStyles.boldLabel);
                _drawScenePreview = EditorGUILayout.Toggle("显示空间预览", _drawScenePreview);
                _previewRoot = (Transform)EditorGUILayout.ObjectField(
                    "动作根节点",
                    _previewRoot,
                    typeof(Transform),
                    true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("使用当前场景选择"))
                    {
                        _previewRoot = Selection.activeTransform;
                        SceneView.RepaintAll();
                    }

                    using (new EditorGUI.DisabledScope(_previewRoot == null))
                    {
                        if (GUILayout.Button("聚焦根节点"))
                        {
                            Selection.activeTransform = _previewRoot;
                            SceneView.lastActiveSceneView?.FrameSelected();
                        }
                    }
                }

                EditorGUILayout.LabelField(
                    "黄色：有效攻击距离；红色：Hitbox；蓝色：Projectile 路径；青色：Root Motion 估算位移。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawScenePreview(SceneView sceneView)
        {
            if (!_drawScenePreview || _action == null || _previewRoot == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_previewRoot.forward, Vector3.up);
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 origin = _previewRoot.position;
            Vector3 spawnPosition = origin
                + forward * _action.HitboxSpawnDistance
                + Vector3.up * _action.HitboxSpawnHeight;
            Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
            float handleSize = HandleUtility.GetHandleSize(spawnPosition);

            Color previousColor = Handles.color;
            Matrix4x4 previousMatrix = Handles.matrix;
            try
            {
                Handles.matrix = Matrix4x4.identity;
                Handles.color = new Color(1f, 0.78f, 0.15f, 0.9f);
                Handles.DrawWireDisc(origin, Vector3.up, _action.EffectiveAttackRange);
                Handles.DrawDottedLine(origin, spawnPosition, 4f);
                Handles.Label(
                    origin + forward * _action.EffectiveAttackRange,
                    $"有效距离 {_action.EffectiveAttackRange:0.##}m");

                Handles.color = new Color(1f, 0.18f, 0.12f, 1f);
                Handles.SphereHandleCap(0, spawnPosition, Quaternion.identity, handleSize * 0.08f, EventType.Repaint);
                Handles.Label(spawnPosition + Vector3.up * handleSize * 0.08f, "Hitbox Spawn");
                DrawHitboxColliders(_action.HitboxPrefab, spawnPosition, spawnRotation);

                DrawProjectilePath(spawnPosition, forward);
                DrawRootMotion(origin, forward, handleSize);
            }
            finally
            {
                Handles.color = previousColor;
                Handles.matrix = previousMatrix;
            }
        }

        private void DrawHitboxColliders(GameObject hitboxPrefab, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (hitboxPrefab == null)
            {
                return;
            }

            Transform prefabRoot = hitboxPrefab.transform;
            Matrix4x4 spawnMatrix = Matrix4x4.TRS(
                spawnPosition,
                spawnRotation,
                prefabRoot.localScale);
            foreach (Collider collider in hitboxPrefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled)
                {
                    continue;
                }

                Matrix4x4 relativeMatrix = prefabRoot.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                Handles.matrix = spawnMatrix * relativeMatrix;
                Handles.color = new Color(1f, 0.18f, 0.12f, 0.95f);
                switch (collider)
                {
                    case BoxCollider box:
                        Handles.DrawWireCube(box.center, box.size);
                        break;
                    case SphereCollider sphere:
                        DrawWireSphere(sphere.center, sphere.radius);
                        break;
                    case CapsuleCollider capsule:
                        DrawCapsuleBounds(capsule);
                        break;
                    case MeshCollider meshCollider when meshCollider.sharedMesh != null:
                        Bounds bounds = meshCollider.sharedMesh.bounds;
                        Handles.DrawWireCube(bounds.center, bounds.size);
                        break;
                    default:
                        Bounds fallbackBounds = collider.bounds;
                        Handles.matrix = Matrix4x4.identity;
                        Handles.DrawWireCube(fallbackBounds.center, fallbackBounds.size);
                        break;
                }
            }

            Handles.matrix = Matrix4x4.identity;
        }

        private static void DrawWireSphere(Vector3 center, float radius)
        {
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private static void DrawCapsuleBounds(CapsuleCollider capsule)
        {
            Vector3 size = Vector3.one * (capsule.radius * 2f);
            size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
            Handles.DrawWireCube(capsule.center, size);
        }

        private void DrawProjectilePath(Vector3 spawnPosition, Vector3 forward)
        {
            if (_action.HitboxPrefab == null
                || !_action.HitboxPrefab.TryGetComponent(out HitboxProjectile projectile))
            {
                return;
            }

            float distance = projectile.MaxDistance > 0f
                ? projectile.MaxDistance
                : projectile.Speed * projectile.Lifetime;
            if (distance <= 0f)
            {
                return;
            }

            Handles.matrix = Matrix4x4.identity;
            Handles.color = new Color(0.2f, 0.65f, 1f, 0.95f);
            Vector3 end = spawnPosition + forward * distance;
            Handles.DrawDottedLine(spawnPosition, end, 5f);
            Handles.Label(end, $"Projectile {distance:0.##}m");
        }

        private void DrawRootMotion(Vector3 origin, Vector3 fallbackForward, float handleSize)
        {
            if (!_action.UseRootMotion || _clip == null)
            {
                return;
            }

            Vector3 localDisplacement = _clip.averageSpeed * _clip.length * _action.RootMotionScale;
            localDisplacement.y = 0f;
            Vector3 worldDisplacement = _previewRoot.TransformDirection(localDisplacement);
            if (worldDisplacement.sqrMagnitude <= 0.0001f)
            {
                worldDisplacement = fallbackForward * 0.001f;
            }

            Vector3 end = origin + worldDisplacement;
            Handles.matrix = Matrix4x4.identity;
            Handles.color = new Color(0.15f, 0.9f, 0.9f, 0.95f);
            Handles.DrawLine(origin, end, 3f);
            Handles.ArrowHandleCap(
                0,
                end,
                Quaternion.LookRotation(worldDisplacement.normalized, Vector3.up),
                handleSize * 0.35f,
                EventType.Repaint);
            Handles.Label(end + Vector3.up * handleSize * 0.08f, $"RM 估算 {worldDisplacement.magnitude:0.##}m");
        }

        private void RebuildStates()
        {
            _states = ActionAnimationCalibrationAnalyzer.CollectStates(_controller);
            _stateIndex = _states.Count > 0 ? Mathf.Clamp(_stateIndex, 0, _states.Count - 1) : -1;
        }

        private void AutoSelectState(bool preferCurrentClip)
        {
            if (_controller == null)
            {
                _stateIndex = -1;
                return;
            }

            if (_states.Count == 0)
            {
                RebuildStates();
            }

            AnimatorStateRecord match = null;
            if (preferCurrentClip && _clip != null)
            {
                match = _states.FirstOrDefault(state =>
                    ActionAnimationCalibrationAnalyzer.MotionContainsClip(state.State.motion, _clip));
            }

            if (match == null && _action != null)
            {
                match = _states.FirstOrDefault(state =>
                    ActionAnimationCalibrationAnalyzer.HasExactActionMapping(state, _action)
                    && ActionAnimationCalibrationAnalyzer.HasActionTrigger(state));
                match ??= _states.FirstOrDefault(state =>
                    ActionAnimationCalibrationAnalyzer.HasActionTypeMapping(state, _action)
                    && ActionAnimationCalibrationAnalyzer.HasActionTrigger(state));
            }

            if (match == null && _clip != null)
            {
                match = _states.FirstOrDefault(state =>
                    ActionAnimationCalibrationAnalyzer.MotionContainsClip(state.State.motion, _clip));
            }

            if (match == null)
            {
                return;
            }

            _stateIndex = _states.IndexOf(match);
            AnimationClip stateClip = ActionAnimationCalibrationAnalyzer.FindFirstClip(match.State.motion);
            if (stateClip != null)
            {
                _clip = stateClip;
            }
        }

        private void SelectStateUsingClip()
        {
            if (_clip == null)
            {
                return;
            }

            int index = _states.FindIndex(state =>
                ActionAnimationCalibrationAnalyzer.MotionContainsClip(state.State.motion, _clip));
            if (index >= 0)
            {
                _stateIndex = index;
            }
        }

        private void RefreshAnalysis()
        {
            _issues = ActionAnimationCalibrationAnalyzer.Analyze(
                _action,
                _clip,
                _controller,
                SelectedState);
            Repaint();
        }

        private void HandleSelectionChanged()
        {
            if (AdoptSelection(false))
            {
                RefreshAnalysis();
            }
        }

        private bool AdoptSelection(bool forceGameObjectContext)
        {
            UnityEngine.Object selected = Selection.activeObject;
            bool changed = false;
            switch (selected)
            {
                case CombatActionDefinition action:
                    changed = _action != action;
                    _action = action;
                    break;
                case AnimationClip clip:
                    changed = _clip != clip;
                    _clip = clip;
                    break;
                case AnimatorController controller:
                    changed = _controller != controller;
                    _controller = controller;
                    RebuildStates();
                    break;
                case GameObject gameObject:
                    if (forceGameObjectContext || _previewRoot == null)
                    {
                        changed |= _previewRoot != gameObject.transform;
                        _previewRoot = gameObject.transform;
                    }

                    Animator animator = gameObject.GetComponentInChildren<Animator>(true)
                        ?? gameObject.GetComponentInParent<Animator>();
                    AnimatorController animatorController = ResolveAnimatorController(animator?.runtimeAnimatorController);
                    if (animatorController != null && animatorController != _controller)
                    {
                        _controller = animatorController;
                        RebuildStates();
                        changed = true;
                    }

                    break;
            }

            if (changed)
            {
                if (selected is AnimationClip)
                {
                    SelectStateUsingClip();
                }
                else
                {
                    AutoSelectState(false);
                }

                SceneView.RepaintAll();
            }

            return changed;
        }

        private static AnimatorController ResolveAnimatorController(RuntimeAnimatorController runtimeController)
        {
            return runtimeController switch
            {
                AnimatorController controller => controller,
                AnimatorOverrideController overrideController =>
                    overrideController.runtimeAnimatorController as AnimatorController,
                _ => null
            };
        }

        private static MessageType ToMessageType(CalibrationSeverity severity)
        {
            return severity switch
            {
                CalibrationSeverity.Error => MessageType.Error,
                CalibrationSeverity.Warning => MessageType.Warning,
                _ => MessageType.Info
            };
        }

        private static bool IsCombatEvent(string functionName)
        {
            return functionName == ActionAnimationCalibrationAnalyzer.HitboxStartEvent
                || functionName == ActionAnimationCalibrationAnalyzer.HitboxEndEvent
                || functionName == ActionAnimationCalibrationAnalyzer.CanCancelEvent
                || functionName == ActionAnimationCalibrationAnalyzer.ActionEndEvent;
        }

        private static Color GetEventColor(string functionName)
        {
            if (functionName == ActionAnimationCalibrationAnalyzer.HitboxStartEvent)
            {
                return new Color(1f, 0.18f, 0.12f, 1f);
            }

            if (functionName == ActionAnimationCalibrationAnalyzer.HitboxEndEvent)
            {
                return new Color(1f, 0.55f, 0.12f, 1f);
            }

            if (functionName == ActionAnimationCalibrationAnalyzer.CanCancelEvent)
            {
                return new Color(0.15f, 0.9f, 0.9f, 1f);
            }

            if (functionName == ActionAnimationCalibrationAnalyzer.ActionEndEvent)
            {
                return new Color(0.3f, 1f, 0.4f, 1f);
            }

            return Color.gray;
        }

        private static string ShortEventName(string functionName)
        {
            return functionName switch
            {
                ActionAnimationCalibrationAnalyzer.HitboxStartEvent => "Hitbox Start",
                ActionAnimationCalibrationAnalyzer.HitboxEndEvent => "Hitbox End",
                ActionAnimationCalibrationAnalyzer.CanCancelEvent => "Can Cancel",
                ActionAnimationCalibrationAnalyzer.ActionEndEvent => "Action End",
                _ => string.IsNullOrWhiteSpace(functionName) ? "<空>" : functionName
            };
        }
    }
}
