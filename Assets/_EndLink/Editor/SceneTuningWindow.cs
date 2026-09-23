using System;
using System.Collections.Generic;
using EndLink.Combat;
using EndLink.Core;
using EndLink.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EndLink.Editor
{
    /// <summary>
    /// 集中编辑当前场景中的高频联调参数。
    /// 所有字段直接写入来源组件，不保存中间副本或额外预设。
    /// </summary>
    internal sealed class SceneTuningWindow : EditorWindow
    {
        private const string MenuPath = "EndLink/Debug/Scene Tuning";

        private static readonly GUIContent[] TabLabels =
        {
            new("玩家"),
            new("镜头"),
            new("战斗"),
            new("敌人")
        };

        private static readonly PropertySpec[] PlayerMovementProperties =
        {
            new("moveSpeed", "移动速度"),
            new("sprintSpeed", "冲刺速度"),
            new("accelerationSmoothTime", "加速平滑"),
            new("decelerationSmoothTime", "减速平滑"),
            new("rotationSharpness", "转向响应"),
            new("gravity", "重力"),
            new("groundedStickForce", "贴地速度"),
            new("jumpHeight", "跳跃高度"),
            new("jumpCooldown", "跳跃冷却"),
            new("combatKnockbackDuration", "击退持续时间")
        };

        private static readonly PropertySpec[] CharacterControllerProperties =
        {
            new("m_Height", "胶囊高度"),
            new("m_Radius", "胶囊半径"),
            new("m_Center", "胶囊中心"),
            new("m_SkinWidth", "Skin Width"),
            new("m_StepOffset", "台阶高度"),
            new("m_SlopeLimit", "坡度限制")
        };

        private static readonly PropertySpec[] PlayerStateProperties =
        {
            new("attackDuration", "数据攻击时长"),
            new("attackMoveInputScale", "攻击移动倍率"),
            new("attackTrackingRotationSharpness", "攻击追踪转向"),
            new("attackInputBufferDuration", "攻击输入缓冲"),
            new("actionCancelOptions", "动作取消许可"),
            new("guardMoveInputScale", "格挡移动倍率"),
            new("skillDuration", "数据技能时长"),
            new("skillMoveInputScale", "技能移动倍率"),
            new("dodgeDuration", "闪避时长"),
            new("dodgeDistance", "闪避距离"),
            new("dodgeCooldown", "闪避冷却"),
            new("dodgeInvincibleDuration", "闪避无敌时间"),
            new("hitDuration", "受击硬直"),
            new("hitMoveInputScale", "受击移动倍率")
        };

        private static readonly PropertySpec[] ViewModeProperties =
        {
            new("viewMode", "当前模式"),
            new("fastActionSettings", "Fast Action Settings", true),
            new("soulsLikeSettings", "Souls Like Settings", true)
        };

        private static readonly PropertySpec[] AimViewProperties =
        {
            new("aimDistance", "瞄准距离"),
            new("aimFieldOfView", "瞄准视野"),
            new("aimShoulderOffset", "瞄准肩位"),
            new("aimEnterSmoothTime", "进入瞄准平滑"),
            new("aimExitSmoothTime", "退出瞄准平滑"),
            new("hardLockRotationSmoothTime", "硬锁旋转平滑")
        };

        private static readonly PropertySpec[] CameraBobProperties =
        {
            new("enableLocomotionBob", "启用步态晃动"),
            new("walkBobAmplitude", "步行晃动幅度"),
            new("sprintBobAmplitude", "冲刺晃动幅度"),
            new("bobFrequency", "步行/冲刺频率"),
            new("fullBobSpeed", "完整晃动速度"),
            new("minimumBobSpeed", "最低生效速度"),
            new("bobBlendSpeed", "晃动混合速度"),
            new("aimBobMultiplier", "瞄准晃动倍率")
        };

        private static readonly PropertySpec[] TargetSwitchProperties =
        {
            new("mouseTargetSwitchThreshold", "鼠标切换阈值"),
            new("gamepadTargetSwitchThreshold", "手柄切换阈值"),
            new("gamepadTargetSwitchResetThreshold", "手柄回中阈值"),
            new("targetSwitchCooldown", "切换冷却")
        };

        private static readonly PropertySpec[] CharacterStatsProperties =
        {
            new("baseAttackPower", "基础攻击力"),
            new("knockbackTakenMultiplier", "承受击退倍率")
        };

        private static readonly PropertySpec[] ComboProperties =
        {
            new("inputWindowStart", "输入窗口开始"),
            new("inputWindowEnd", "输入窗口结束"),
            new("queuedStepTimeout", "排队超时"),
            new("stepDistance", "攻击踏步距离"),
            new("stepDuration", "攻击踏步时长"),
            new("targetStopDistance", "目标停止距离"),
            new("maxTargetAssistDistance", "最大辅助距离")
        };

        private static readonly PropertySpec[] GuardProperties =
        {
            new("parryWindowDuration", "弹反窗口"),
            new("guardAngle", "格挡角度"),
            new("blockedDamageMultiplier", "格挡伤害倍率"),
            new("showFallbackVisual", "显示占位反馈"),
            new("feedbackLocalOffset", "反馈局部偏移"),
            new("feedbackRadius", "反馈半径")
        };

        private static readonly PropertySpec[] AimCombatProperties =
        {
            new("aimLayerMask", "瞄准 Layer"),
            new("maxAimDistance", "最远瞄准距离")
        };

        private static readonly PropertySpec[] FeedbackProperties =
        {
            new("enableHitstop", "Hitstop"),
            new("enableCameraImpulse", "镜头冲击"),
            new("enableRumble", "手柄震动"),
            new("enableAudio", "音效"),
            new("enableVfx", "VFX")
        };

        private static readonly PropertySpec[] EnemyHealthProperties =
        {
            new("maxHealth", "最大生命"),
            new("disableCollidersOnDeath", "死亡禁用碰撞"),
            new("deactivateOnDeath", "死亡后停用"),
            new("deathDeactivateDelay", "停用延迟")
        };

        private static readonly PropertySpec[] EnemyBalanceProperties =
        {
            new("maxBalance", "最大 Balance"),
            new("recoveryDelay", "恢复延迟"),
            new("recoveryPerSecond", "每秒恢复")
        };

        private static readonly PropertySpec[] EnemyMotorProperties =
        {
            new("moveSpeed", "移动速度"),
            new("accelerationSmoothTime", "加速平滑"),
            new("rotationSpeed", "转向速度"),
            new("gravity", "重力"),
            new("groundedStickForce", "贴地速度"),
            new("useNavMeshWhenAvailable", "使用 NavMesh"),
            new("navMeshSampleDistance", "NavMesh 采样距离"),
            new("collisionPushMultiplier", "碰撞推挤倍率"),
            new("combatKnockbackDuration", "击退持续时间")
        };

        private static readonly PropertySpec[] EnemyStateProperties =
        {
            new("detectionEnabled", "启用感知"),
            new("detectionRadius", "感知半径"),
            new("requiredAlertTime", "警觉时间"),
            new("hitDuration", "受击硬直"),
            new("poise", "韧性"),
            new("hitReactCooldown", "受击反应冷却"),
            new("staggerDuration", "失衡时长"),
            new("combatChaseStopDistance", "追击停止距离"),
            new("combatAttackRangeTolerance", "攻击距离容差"),
            new("combatAttackInnerOffset", "攻击内圈偏移"),
            new("combatBasicAttacksBeforeSkill", "技能前普攻次数"),
            new("combatSkillChance", "技能概率"),
            new("rangedMinimumDistance", "远程最小距离"),
            new("rangedPreferredDistance", "远程偏好距离"),
            new("rangedRequireLineOfSight", "远程要求视线"),
            new("rangedRepositionDistance", "远程换位距离"),
            new("maxChaseRadius", "最大追击半径"),
            new("lostTargetDelay", "丢失目标延迟")
        };

        private static readonly PropertySpec[] EnemyActionProperties =
        {
            new("basicAttackAction", "普通攻击 Action"),
            new("skillAction", "技能 Action"),
            new("faceTargetBeforeAttack", "攻击前面向目标")
        };

        private static readonly PropertySpec[] CoordinatorProperties =
        {
            new("coordinationRadius", "协调半径"),
            new("scanInterval", "扫描间隔"),
            new("attackCoordinationEnabled", "启用攻击协调"),
            new("maxSimultaneousAttackers", "同时攻击数量"),
            new("attackGrantInterval", "攻击许可间隔"),
            new("attackReservationDuration", "许可保留时间"),
            new("softPositioningEnabled", "启用软站位"),
            new("softPositionMinDistance", "软站位最小距离"),
            new("softPositionMaxDistance", "软站位最大距离"),
            new("softPositionMinSpacing", "软站位间距"),
            new("observationPauseInterval", "观察停顿范围"),
            new("observationMoveDistance", "观察移动距离"),
            new("attackPrepareDelay", "攻击准备延迟"),
            new("attackPrepareMoveDistance", "准备移动距离"),
            new("attackPrepareMoveDuration", "准备移动时长")
        };

        private TuningTab _tab;
        private Vector2 _scrollPosition;
        private Vector2 _changesScrollPosition;
        private bool _showPendingChanges;
        private PlayerStateMachine _playerStateMachine;
        private PlayerController _playerController;
        private CharacterController _characterController;
        private PlayerViewController _viewController;
        private CharacterStats _playerStats;
        private PlayerComboController _comboController;
        private PlayerGuardController _guardController;
        private PlayerAimController _aimController;
        private CombatFeedbackDispatcher _feedbackDispatcher;
        private EnemyActor _selectedEnemy;
        private EnemyCombatCoordinator _coordinator;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            SceneTuningWindow window = GetWindow<SceneTuningWindow>("Scene Tuning");
            window.minSize = new Vector2(430f, 520f);
            window.Show();
        }

        internal static void OpenPendingChanges()
        {
            SceneTuningWindow window = GetWindow<SceneTuningWindow>("Scene Tuning");
            window.minSize = new Vector2(430f, 520f);
            window._showPendingChanges = true;
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += RefreshSceneReferences;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            Selection.selectionChanged += HandleSelectionChanged;
            SceneTuningChangeTracker.Changed += HandleTrackedChangesChanged;
            RefreshSceneReferences();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= RefreshSceneReferences;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            Selection.selectionChanged -= HandleSelectionChanged;
            SceneTuningChangeTracker.Changed -= HandleTrackedChangesChanged;
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawPendingChanges();
            _tab = (TuningTab)GUILayout.Toolbar((int)_tab, TabLabels, GUILayout.Height(24f));
            EditorGUILayout.Space(5f);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            switch (_tab)
            {
                case TuningTab.Camera:
                    DrawCameraTab();
                    break;
                case TuningTab.Combat:
                    DrawCombatTab();
                    break;
                case TuningTab.Enemy:
                    DrawEnemyTab();
                    break;
                default:
                    DrawPlayerTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("场景联调", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(EditorApplication.isPlaying ? "Play Mode：实时值" : "Edit Mode：保存到场景", EditorStyles.miniLabel);
                if (SceneTuningChangeTracker.PendingCount > 0
                    && GUILayout.Button($"待应用 {SceneTuningChangeTracker.PendingCount}", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                {
                    _showPendingChanges = !_showPendingChanges;
                }

                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    RefreshSceneReferences();
                }
            }
        }

        private void DrawPendingChanges()
        {
            int pendingCount = SceneTuningChangeTracker.PendingCount;
            if (pendingCount == 0 || !_showPendingChanges)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"待应用的运行时差异（{pendingCount}）", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "只包含通过本窗口修改的白名单字段。应用后会写入场景并支持 Undo。",
                    EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("应用选中项"))
                    {
                        int applied = SceneTuningChangeTracker.ApplySelected(out int failed);
                        if (failed > 0)
                        {
                            EditorUtility.DisplayDialog(
                                "部分参数未应用",
                                $"成功应用 {applied} 项，{failed} 项因对象、字段或引用失效而未应用。",
                                "确定");
                        }
                    }

                    if (GUILayout.Button("全选"))
                    {
                        SetAllChangesSelected(true);
                    }

                    if (GUILayout.Button("全不选"))
                    {
                        SetAllChangesSelected(false);
                    }

                    if (GUILayout.Button("全部放弃"))
                    {
                        if (EditorUtility.DisplayDialog("放弃运行时差异", "确定放弃全部待应用参数吗？", "放弃", "取消"))
                        {
                            SceneTuningChangeTracker.DiscardAll();
                        }
                    }
                }

                _changesScrollPosition = EditorGUILayout.BeginScrollView(
                    _changesScrollPosition,
                    GUILayout.MinHeight(90f),
                    GUILayout.MaxHeight(260f));

                TrackedChange[] changes = new TrackedChange[pendingCount];
                for (int i = 0; i < pendingCount; i++)
                {
                    changes[i] = SceneTuningChangeTracker.Changes[i];
                }

                foreach (TrackedChange change in changes)
                {
                    DrawTrackedChange(change);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawTrackedChange(TrackedChange change)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool selected = EditorGUILayout.Toggle(change.selected, GUILayout.Width(18f));
                    if (selected != change.selected)
                    {
                        SceneTuningChangeTracker.SetSelected(change, selected);
                    }

                    EditorGUILayout.LabelField(
                        $"{change.category} / {change.propertyLabel}",
                        EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(SceneTuningChangeTracker.ResolveComponent(change) == null))
                    {
                        if (GUILayout.Button("定位", GUILayout.Width(44f)))
                        {
                            SelectAndPing(SceneTuningChangeTracker.ResolveComponent(change));
                        }
                    }
                }

                EditorGUILayout.LabelField(
                    $"{change.objectName} · {change.componentName}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"{change.before.ToDisplayString()}  →  {change.after.ToDisplayString()}",
                    EditorStyles.wordWrappedLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("应用", GUILayout.Width(52f)))
                    {
                        SceneTuningChangeTracker.ApplyResult result = SceneTuningChangeTracker.Apply(change);
                        if (result != SceneTuningChangeTracker.ApplyResult.Applied)
                        {
                            EditorUtility.DisplayDialog("无法应用", "来源对象、字段或资源引用已经失效。", "确定");
                        }
                    }

                    if (GUILayout.Button("放弃", GUILayout.Width(52f)))
                    {
                        SceneTuningChangeTracker.Discard(change);
                    }
                }
            }
        }

        private static void SetAllChangesSelected(bool selected)
        {
            TrackedChange[] changes = new TrackedChange[SceneTuningChangeTracker.PendingCount];
            for (int i = 0; i < changes.Length; i++)
            {
                changes[i] = SceneTuningChangeTracker.Changes[i];
            }

            foreach (TrackedChange change in changes)
            {
                SceneTuningChangeTracker.SetSelected(change, selected);
            }
        }

        private void DrawPlayerTab()
        {
            if (!RequirePlayer())
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    $"当前状态：{_playerStateMachine.CurrentStateId} / 移动速度：{_playerController.PlanarSpeed:0.00} m/s / "
                    + $"贴地：{_playerController.IsGrounded}",
                    MessageType.None);
            }

            DrawComponentSection("移动与跳跃", _playerController, PlayerMovementProperties);
            DrawComponentSection("CharacterController", _characterController, CharacterControllerProperties);
            DrawComponentSection("状态与动作许可", _playerStateMachine, PlayerStateProperties);
        }

        private void DrawCameraTab()
        {
            if (_viewController == null)
            {
                DrawMissingMessage("当前场景没有找到 PlayerViewController。");
                return;
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    $"当前模式：{_viewController.ViewMode} / 瞄准覆盖：{_viewController.IsAimViewActive}",
                    MessageType.None);
            }

            DrawComponentSection("视角模式预设", _viewController, ViewModeProperties);
            DrawComponentSection("射击与硬锁镜头", _viewController, AimViewProperties);
            DrawComponentSection("移动步态", _viewController, CameraBobProperties);
            DrawComponentSection("硬锁目标切换", _viewController, TargetSwitchProperties);
        }

        private void DrawCombatTab()
        {
            if (!RequirePlayer())
            {
                return;
            }

            DrawComponentSection("角色战斗数值", _playerStats, CharacterStatsProperties);
            DrawComponentSection("普攻连段与踏步", _comboController, ComboProperties);
            DrawComponentSection("格挡与弹反", _guardController, GuardProperties);
            DrawComponentSection("射击瞄准检测", _aimController, AimCombatProperties);
            DrawComponentSection("场景反馈通道", _feedbackDispatcher, FeedbackProperties);
        }

        private void DrawEnemyTab()
        {
            DrawEnemySelector();
            if (_selectedEnemy == null)
            {
                DrawMissingMessage("当前场景没有可调教的 EnemyActor，或尚未指定敌人。");
            }
            else
            {
                EnemyStateMachine stateMachine = _selectedEnemy.GetComponent<EnemyStateMachine>();
                if (EditorApplication.isPlaying && stateMachine != null)
                {
                    EditorGUILayout.HelpBox(
                        $"当前状态：{stateMachine.CurrentStateId} / 阶段：{stateMachine.CurrentCombatPhase} / "
                        + $"生命：{_selectedEnemy.Health.CurrentHealth}/{_selectedEnemy.Health.MaxHealth} / "
                        + $"Balance：{_selectedEnemy.Balance.CurrentBalance:0.#}/{_selectedEnemy.Balance.MaxBalance:0.#}",
                        MessageType.None);
                }

                DrawComponentSection("生命", _selectedEnemy.Health, EnemyHealthProperties);
                DrawComponentSection("Balance 与失衡", _selectedEnemy.Balance, EnemyBalanceProperties);
                DrawComponentSection("移动", _selectedEnemy.Motor, EnemyMotorProperties);
                DrawComponentSection("状态与战斗距离", stateMachine, EnemyStateProperties);
                DrawComponentSection("动作执行", _selectedEnemy.CombatDriver, EnemyActionProperties);
            }

            EditorGUILayout.Space(8f);
            _coordinator = (EnemyCombatCoordinator)EditorGUILayout.ObjectField(
                "围攻协调器",
                _coordinator,
                typeof(EnemyCombatCoordinator),
                true);
            DrawComponentSection("围攻节奏", _coordinator, CoordinatorProperties);
        }

        private void DrawEnemySelector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _selectedEnemy = (EnemyActor)EditorGUILayout.ObjectField(
                    "当前敌人",
                    _selectedEnemy,
                    typeof(EnemyActor),
                    true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("使用当前选择"))
                    {
                        _selectedEnemy = ResolveEnemyFromSelection();
                    }

                    using (new EditorGUI.DisabledScope(_selectedEnemy == null))
                    {
                        if (GUILayout.Button("定位敌人"))
                        {
                            SelectAndPing(_selectedEnemy);
                        }
                    }
                }
            }
        }

        private bool RequirePlayer()
        {
            if (_playerStateMachine != null && _playerController != null)
            {
                return true;
            }

            DrawMissingMessage("当前活动场景没有找到完整的 PlayerStateMachine / PlayerController。");
            return false;
        }

        private static void DrawMissingMessage(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        private static void DrawComponentSection(string title, Component component, PropertySpec[] properties)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    if (component != null)
                    {
                        EditorGUILayout.LabelField(component.GetType().Name, EditorStyles.miniLabel, GUILayout.Width(150f));
                        if (GUILayout.Button("定位", GUILayout.Width(44f)))
                        {
                            SelectAndPing(component);
                        }
                    }
                }

                if (component == null)
                {
                    EditorGUILayout.HelpBox("来源组件不存在。", MessageType.Info);
                    return;
                }

                SerializedObject serializedObject = new(component);
                serializedObject.UpdateIfRequiredOrScript();
                Dictionary<string, CapturedProperty> valuesBeforeEdit = EditorApplication.isPlaying
                    ? CaptureProperties(serializedObject, properties)
                    : null;
                EditorGUI.BeginChangeCheck();

                for (int i = 0; i < properties.Length; i++)
                {
                    PropertySpec spec = properties[i];
                    SerializedProperty property = serializedObject.FindProperty(spec.Path);
                    if (property == null)
                    {
                        EditorGUILayout.HelpBox($"字段已失效：{component.GetType().Name}.{spec.Path}", MessageType.Warning);
                        continue;
                    }

                    if (spec.Expanded)
                    {
                        property.isExpanded = true;
                    }

                    EditorGUILayout.PropertyField(property, new GUIContent(spec.Label), true);
                }

                if (!EditorGUI.EndChangeCheck())
                {
                    return;
                }

                Undo.RecordObject(component, $"Tune {component.GetType().Name}");
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(component);

                if (EditorApplication.isPlaying && valuesBeforeEdit != null)
                {
                    serializedObject.UpdateIfRequiredOrScript();
                    RecordChangedProperties(component, title, serializedObject, valuesBeforeEdit);
                }

                if (!EditorApplication.isPlaying && component.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                }
            }
        }

        private static Dictionary<string, CapturedProperty> CaptureProperties(
            SerializedObject serializedObject,
            PropertySpec[] properties)
        {
            Dictionary<string, CapturedProperty> captured = new();
            foreach (PropertySpec spec in properties)
            {
                SerializedProperty property = serializedObject.FindProperty(spec.Path);
                if (property == null)
                {
                    continue;
                }

                CaptureProperty(captured, property, spec.Label);
            }

            return captured;
        }

        private static void CaptureProperty(
            Dictionary<string, CapturedProperty> captured,
            SerializedProperty property,
            string rootLabel)
        {
            if (property.propertyType != SerializedPropertyType.Generic || property.isArray)
            {
                TryAddCapturedProperty(captured, property, rootLabel);
                return;
            }

            SerializedProperty iterator = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = iterator.propertyType == SerializedPropertyType.Generic && !iterator.isArray;
                if (!enterChildren)
                {
                    string childLabel = $"{rootLabel} / {iterator.displayName}";
                    TryAddCapturedProperty(captured, iterator, childLabel);
                }
            }
        }

        private static void TryAddCapturedProperty(
            Dictionary<string, CapturedProperty> captured,
            SerializedProperty property,
            string label)
        {
            if (SerializedValue.TryCapture(property, out SerializedValue value))
            {
                captured[property.propertyPath] = new CapturedProperty(label, value);
            }
        }

        private static void RecordChangedProperties(
            Component component,
            string category,
            SerializedObject serializedObject,
            Dictionary<string, CapturedProperty> valuesBeforeEdit)
        {
            foreach (KeyValuePair<string, CapturedProperty> pair in valuesBeforeEdit)
            {
                SerializedProperty property = serializedObject.FindProperty(pair.Key);
                if (property == null || !SerializedValue.TryCapture(property, out SerializedValue after))
                {
                    continue;
                }

                SceneTuningChangeTracker.Record(
                    component,
                    category,
                    pair.Value.Label,
                    pair.Value.Value,
                    after);
            }
        }

        private void RefreshSceneReferences()
        {
            _playerStateMachine = FindSceneComponent<PlayerStateMachine>();
            _playerController = _playerStateMachine != null
                ? _playerStateMachine.GetComponent<PlayerController>()
                : FindSceneComponent<PlayerController>();
            _characterController = _playerController != null
                ? _playerController.GetComponent<CharacterController>()
                : null;
            _viewController = FindSceneComponent<PlayerViewController>();

            GameObject player = _playerStateMachine != null ? _playerStateMachine.gameObject : null;
            _playerStats = player != null ? player.GetComponent<CharacterStats>() : null;
            _comboController = player != null ? player.GetComponent<PlayerComboController>() : null;
            _guardController = player != null ? player.GetComponent<PlayerGuardController>() : null;
            _aimController = player != null ? player.GetComponent<PlayerAimController>() : null;
            _feedbackDispatcher = FindSceneComponent<CombatFeedbackDispatcher>();

            if (_selectedEnemy == null || _selectedEnemy.gameObject.scene != SceneManager.GetActiveScene())
            {
                _selectedEnemy = ResolveEnemyFromSelection() ?? FindSceneComponent<EnemyActor>();
            }

            if (_coordinator == null || _coordinator.gameObject.scene != SceneManager.GetActiveScene())
            {
                _coordinator = FindSceneComponent<EnemyCombatCoordinator>();
            }

            Repaint();
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            EditorApplication.delayCall += RefreshSceneReferences;
        }

        private void HandleSelectionChanged()
        {
            EnemyActor selected = ResolveEnemyFromSelection();
            if (selected != null)
            {
                _selectedEnemy = selected;
                Repaint();
            }
        }

        private void HandleTrackedChangesChanged()
        {
            if (SceneTuningChangeTracker.PendingCount == 0)
            {
                _showPendingChanges = false;
            }

            Repaint();
        }

        private static T FindSceneComponent<T>() where T : Component
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static EnemyActor ResolveEnemyFromSelection()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null ? selected.GetComponentInParent<EnemyActor>() : null;
        }

        private static void SelectAndPing(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private enum TuningTab
        {
            Player = 0,
            Camera = 1,
            Combat = 2,
            Enemy = 3
        }

        private readonly struct PropertySpec
        {
            public PropertySpec(string path, string label, bool expanded = false)
            {
                Path = path;
                Label = label;
                Expanded = expanded;
            }

            public string Path { get; }

            public string Label { get; }

            public bool Expanded { get; }
        }

        private readonly struct CapturedProperty
        {
            public CapturedProperty(string label, SerializedValue value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }

            public SerializedValue Value { get; }
        }
    }
}
