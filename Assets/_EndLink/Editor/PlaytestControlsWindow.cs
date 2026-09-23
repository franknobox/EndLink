using EndLink.Combat;
using EndLink.Core;
using EndLink.Enemies;
using EndLink.World;
using UnityEditor;
using UnityEngine;

namespace EndLink.Editor
{
    /// <summary>
    /// Play Mode 中使用的轻量测试控制台。
    /// 只修改本次运行的状态，不保存场景，也不维护第二套配置数据。
    /// </summary>
    internal sealed class PlaytestControlsWindow : EditorWindow
    {
        private const string MenuPath = "EndLink/Debug/Playtest Controls";
        private const float RefreshInterval = 0.25f;

        private double _nextRefreshTime;
        private PlayerStateMachine _playerStateMachine;
        private PlayerHealth _playerHealth;
        private CombatTagContainer _playerTags;
        private PlayerWeaponController _weaponController;
        private WorldRespawnManager _respawnManager;
        private EnemyActor[] _enemies = System.Array.Empty<EnemyActor>();

        [MenuItem(MenuPath)]
        private static void Open()
        {
            PlaytestControlsWindow window = GetWindow<PlaytestControlsWindow>("Playtest Controls");
            window.minSize = new Vector2(390f, 410f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update += HandleEditorUpdate;
            RefreshReferences();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= HandleEditorUpdate;
        }

        private void OnGUI()
        {
            DrawHeader();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "进入 Play Mode 后可使用快捷控制。所有操作只影响本次运行，不会保存到场景。",
                    MessageType.Info);
                return;
            }

            RefreshReferencesIfNeeded();
            DrawPlayerSection();
            DrawWeaponSection();
            DrawEnemySection();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Playtest 快捷控制", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "运行时恢复、重置与定位，不写回场景配置。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
        }

        private void DrawPlayerSection()
        {
            EditorGUILayout.LabelField("玩家", EditorStyles.boldLabel);

            if (_playerStateMachine == null)
            {
                EditorGUILayout.HelpBox("当前场景没有找到 PlayerStateMachine。", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string healthText = _playerHealth != null
                    ? $"{_playerHealth.CurrentHealth} / {_playerHealth.MaxHealth}"
                    : "未找到 PlayerHealth";
                EditorGUILayout.LabelField("对象", _playerStateMachine.name);
                EditorGUILayout.LabelField("状态", _playerStateMachine.CurrentStateId.ToString());
                EditorGUILayout.LabelField("生命", healthText);
                EditorGUILayout.LabelField(
                    "当前检查点",
                    _respawnManager != null && !string.IsNullOrWhiteSpace(_respawnManager.CurrentSpawnPointId)
                        ? _respawnManager.CurrentSpawnPointId
                        : "未配置");

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_playerHealth == null || _playerHealth.IsDead))
                    {
                        if (GUILayout.Button("回满生命"))
                        {
                            _playerHealth.ResetHealth();
                        }
                    }

                    using (new EditorGUI.DisabledScope(_playerTags == null))
                    {
                        if (GUILayout.Button("清空标签"))
                        {
                            _playerTags.ClearTags();
                        }
                    }

                    if (GUILayout.Button("清除冷却"))
                    {
                        _playerStateMachine.ClearCooldowns();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重置战斗状态"))
                    {
                        _playerStateMachine.ResetForRespawn();
                    }

                    using (new EditorGUI.DisabledScope(_respawnManager == null))
                    {
                        if (GUILayout.Button("返回当前检查点"))
                        {
                            _respawnManager.RespawnNow();
                            RefreshReferences();
                        }
                    }

                    if (GUILayout.Button("选中玩家", GUILayout.Width(86f)))
                    {
                        SelectAndPing(_playerStateMachine.gameObject);
                    }
                }
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawWeaponSection()
        {
            EditorGUILayout.LabelField("武器形态", EditorStyles.boldLabel);

            if (_weaponController == null)
            {
                EditorGUILayout.HelpBox("当前玩家没有找到 PlayerWeaponController。", MessageType.Info);
                EditorGUILayout.Space(6f);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("当前形态", _weaponController.CurrentForm.ToString());

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawUnlockButton(PlayerWeaponForm.B, "解锁 B 形态");
                    DrawUnlockButton(PlayerWeaponForm.C, "解锁 C 形态");
                }
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawEnemySection()
        {
            int deadCount = 0;
            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i] != null && _enemies[i].Health != null && _enemies[i].Health.IsDead)
                {
                    deadCount++;
                }
            }

            EditorGUILayout.LabelField("敌人", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("已发现", $"{_enemies.Length}（死亡 {deadCount}）");
                EditorGUILayout.HelpBox(
                    "初版在当前位置恢复敌人的生命、平衡、动作冷却、状态和标签，不传送回出生位置。",
                    MessageType.None);

                using (new EditorGUI.DisabledScope(_enemies.Length == 0))
                {
                    if (GUILayout.Button("重置全部敌人"))
                    {
                        ResetAllEnemies();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("刷新引用"))
                    {
                        RefreshReferences();
                    }

                    using (new EditorGUI.DisabledScope(_enemies.Length == 0))
                    {
                        if (GUILayout.Button("选中第一个敌人"))
                        {
                            SelectAndPing(_enemies[0].gameObject);
                        }
                    }
                }
            }
        }

        private void DrawUnlockButton(PlayerWeaponForm form, string label)
        {
            bool unlocked = _weaponController.IsFormUnlocked(form);
            using (new EditorGUI.DisabledScope(unlocked))
            {
                if (GUILayout.Button(unlocked ? $"{form} 已解锁" : label))
                {
                    _weaponController.UnlockForm(form);
                }
            }
        }

        private void ResetAllEnemies()
        {
            for (int i = 0; i < _enemies.Length; i++)
            {
                EnemyActor enemy = _enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                if (!enemy.gameObject.activeSelf)
                {
                    enemy.gameObject.SetActive(true);
                }

                enemy.Health?.ResetHealth();
                enemy.Balance?.ResetBalance();
                enemy.GetComponent<CombatTagContainer>()?.ClearTags();
                enemy.GetComponent<EnemyStateMachine>()?.ResetRuntimeState();
                enemy.Motor?.Stop();
            }

            RefreshReferences();
            ShowNotification(new GUIContent($"已重置 {_enemies.Length} 个敌人"));
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            RefreshReferences();
            Repaint();
        }

        private void HandleEditorUpdate()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshInterval;
            Repaint();
        }

        private void RefreshReferencesIfNeeded()
        {
            if (_playerStateMachine == null || EditorApplication.timeSinceStartup >= _nextRefreshTime)
            {
                RefreshReferences();
                _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshInterval;
            }
        }

        private void RefreshReferences()
        {
            if (!EditorApplication.isPlaying)
            {
                _playerStateMachine = null;
                _playerHealth = null;
                _playerTags = null;
                _weaponController = null;
                _respawnManager = null;
                _enemies = System.Array.Empty<EnemyActor>();
                return;
            }

            _playerStateMachine = Object.FindFirstObjectByType<PlayerStateMachine>();
            if (_playerStateMachine != null)
            {
                GameObject player = _playerStateMachine.gameObject;
                player.TryGetComponent(out _playerHealth);
                player.TryGetComponent(out _playerTags);
                player.TryGetComponent(out _weaponController);
            }
            else
            {
                _playerHealth = null;
                _playerTags = null;
                _weaponController = null;
            }

            _respawnManager = WorldRespawnManager.Active != null
                ? WorldRespawnManager.Active
                : Object.FindFirstObjectByType<WorldRespawnManager>();
            _enemies = Object.FindObjectsByType<EnemyActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static void SelectAndPing(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            Selection.activeGameObject = gameObject;
            EditorGUIUtility.PingObject(gameObject);
        }
    }
}
