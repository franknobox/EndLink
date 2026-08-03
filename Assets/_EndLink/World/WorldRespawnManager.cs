using System;
using System.Collections;
using EndLink.Combat;
using EndLink.Core;
using EndLink.Party;
using UnityEngine;
using UnityEngine.Events;

namespace EndLink.World
{
    /// <summary>
    /// 场景级玩家出生与复活调度器。
    /// 负责维护初始出生点和当前检查点，并在玩家死亡后统一清理战斗状态、传送和恢复生命。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldRespawnManager : MonoBehaviour
    {
        [Header("玩家")]
        [Tooltip("固定主控角色根物体。PlayerController、PlayerStateMachine 和生命等组件应挂在该物体上。")]
        [SerializeField]
        private Transform playerRoot;

        [Header("开场出生")]
        [Tooltip("场景默认出生位置，同时作为尚未激活检查点时的复活位置。")]
        [SerializeField]
        private WorldSpawnPoint initialSpawnPoint;

        [Tooltip("进入场景时是否立即把玩家放到 Initial Spawn Point。关闭后仍会把它登记为默认复活点。")]
        [SerializeField]
        private bool placePlayerAtInitialPoint = true;

        [Header("复活")]
        [Tooltip("玩家死亡后等待多少真实时间再复活，不受 Hitstop 或 Time.timeScale 影响。")]
        [SerializeField, Min(0f)]
        private float respawnDelay = 1f;

        [Header("坠落死亡")]
        [Tooltip("玩家根物体低于该世界 Y 高度时立即死亡。默认 -50 表示跌出 Y=-50 的地图下界后进入正常复活流程。")]
        [SerializeField]
        private float fallDeathHeight = -50f;

        [Header("可选上下文")]
        [Tooltip("小队战斗上下文。配置后，战斗中默认不能使用检查点，并会在复活时清空战斗上下文。")]
        [SerializeField]
        private PartyCombatContext partyCombatContext;

        [Header("事件")]
        [Tooltip("当前复活点发生变化时触发。参数为新的出生点。")]
        [SerializeField]
        private WorldSpawnPointEvent onCheckpointChanged = new();

        [Tooltip("玩家完成复活后触发。参数为本次使用的出生点。")]
        [SerializeField]
        private WorldSpawnPointEvent onPlayerRespawned = new();

        private static WorldRespawnManager _active;

        private PlayerController _playerController;
        private PlayerStateMachine _playerStateMachine;
        private PlayerHealth _playerHealth;
        private CombatTagContainer _tagContainer;
        private PlayerTargeting _playerTargeting;
        private WorldSpawnPoint _currentSpawnPoint;
        private Coroutine _respawnCoroutine;
        private bool _subscribedToPlayerDeath;

        /// <summary>当前场景已经启用的唯一复活调度器。</summary>
        public static WorldRespawnManager Active => _active;

        /// <summary>固定主控角色根物体。</summary>
        public Transform PlayerRoot => playerRoot;

        /// <summary>当前死亡后会返回的出生点。</summary>
        public WorldSpawnPoint CurrentSpawnPoint => _currentSpawnPoint;

        /// <summary>当前复活点的稳定 ID，未配置时返回空字符串。</summary>
        public string CurrentSpawnPointId => _currentSpawnPoint != null ? _currentSpawnPoint.PointId : string.Empty;

        /// <summary>当前是否正在等待复活。</summary>
        public bool IsRespawning => _respawnCoroutine != null;

        /// <summary>当前复活点变化事件。</summary>
        public event Action<WorldSpawnPoint> CheckpointChanged;

        /// <summary>玩家完成复活事件。</summary>
        public event Action<WorldSpawnPoint> PlayerRespawned;

        /// <summary>Inspector 可配置的当前复活点变化事件。</summary>
        public WorldSpawnPointEvent OnCheckpointChanged => onCheckpointChanged;

        /// <summary>Inspector 可配置的玩家复活完成事件。</summary>
        public WorldSpawnPointEvent OnPlayerRespawned => onPlayerRespawned;

        private void Awake()
        {
            CachePlayerReferences();
        }

        private void OnEnable()
        {
            if (_active != null && _active != this)
            {
                Debug.LogError("场景中同时启用了多个 WorldRespawnManager，只能保留一个。", this);
                enabled = false;
                return;
            }

            _active = this;
            CachePlayerReferences();
            SubscribePlayerDeath();
        }

        private void Start()
        {
            if (initialSpawnPoint == null)
            {
                Debug.LogWarning("WorldRespawnManager 缺少 Initial Spawn Point，玩家死亡后将无法复活。", this);
                return;
            }

            SetCurrentSpawnPoint(initialSpawnPoint);
            if (placePlayerAtInitialPoint)
            {
                MovePlayerTo(initialSpawnPoint);
            }
        }

        private void Update()
        {
            if (playerRoot == null
                || _playerHealth == null
                || _playerHealth.IsDead
                || IsRespawning
                || playerRoot.position.y > fallDeathHeight)
            {
                return;
            }

            _playerHealth.Kill();
        }

        private void OnDisable()
        {
            UnsubscribePlayerDeath();

            if (_respawnCoroutine != null)
            {
                StopCoroutine(_respawnCoroutine);
                _respawnCoroutine = null;
            }

            if (_active == this)
            {
                _active = null;
            }
        }

        private void Reset()
        {
            CachePlayerReferences();
        }

        private void OnValidate()
        {
            respawnDelay = Mathf.Max(0f, respawnDelay);
        }

        /// <summary>
        /// 判断指定对象是否可以使用检查点。
        /// 默认只接受固定主控，并在配置了战斗上下文时阻止战斗中休整。
        /// </summary>
        public bool CanUseCheckpoint(GameObject interactor, bool allowDuringCombat)
        {
            if (!IsPlayerObject(interactor))
            {
                return false;
            }

            return allowDuringCombat || partyCombatContext == null || !partyCombatContext.IsInCombat;
        }

        /// <summary>
        /// 激活指定检查点并立即恢复玩家生命、清除战斗标签和锁定目标。
        /// 重复使用当前检查点仍会执行休整恢复，但不会重复广播检查点变化。
        /// </summary>
        public bool ActivateCheckpoint(WorldSpawnPoint spawnPoint, GameObject interactor)
        {
            if (spawnPoint == null || !IsPlayerObject(interactor))
            {
                return false;
            }

            SetCurrentSpawnPoint(spawnPoint);
            RestorePlayerVitals();
            ClearPlayerCombatContext();
            return true;
        }

        /// <summary>立即在当前检查点复活玩家，主要供调试、重试 UI 或后续流程系统调用。</summary>
        public bool RespawnNow()
        {
            if (_respawnCoroutine != null)
            {
                StopCoroutine(_respawnCoroutine);
                _respawnCoroutine = null;
            }

            return ExecuteRespawn();
        }

        private void HandlePlayerDead()
        {
            if (_respawnCoroutine == null)
            {
                _respawnCoroutine = StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            // PlayerHealth 会在 OnDead 事件返回后才请求 Dead 状态，因此至少延迟一帧再执行恢复。
            yield return null;

            float respawnAt = Time.realtimeSinceStartup + respawnDelay;
            while (Time.realtimeSinceStartup < respawnAt)
            {
                yield return null;
            }

            _respawnCoroutine = null;
            ExecuteRespawn();
        }

        private bool ExecuteRespawn()
        {
            WorldSpawnPoint spawnPoint = _currentSpawnPoint != null
                ? _currentSpawnPoint
                : initialSpawnPoint;
            if (spawnPoint == null)
            {
                Debug.LogError("没有可用的 WorldSpawnPoint，无法复活玩家。", this);
                return false;
            }

            CachePlayerReferences();
            if (playerRoot == null)
            {
                Debug.LogError("WorldRespawnManager 缺少 Player Root，无法复活玩家。", this);
                return false;
            }

            ClearPlayerCombatContext();
            MovePlayerTo(spawnPoint);
            RestorePlayerVitals();
            _playerStateMachine?.ResetForRespawn();

            onPlayerRespawned.Invoke(spawnPoint);
            PlayerRespawned?.Invoke(spawnPoint);
            return true;
        }

        private void SetCurrentSpawnPoint(WorldSpawnPoint spawnPoint)
        {
            if (spawnPoint == null || _currentSpawnPoint == spawnPoint)
            {
                return;
            }

            _currentSpawnPoint = spawnPoint;
            onCheckpointChanged.Invoke(spawnPoint);
            CheckpointChanged?.Invoke(spawnPoint);
        }

        private void MovePlayerTo(WorldSpawnPoint spawnPoint)
        {
            if (playerRoot == null || spawnPoint == null)
            {
                return;
            }

            Vector3 eulerAngles = spawnPoint.transform.eulerAngles;
            Quaternion playerRotation = Quaternion.Euler(0f, eulerAngles.y, 0f);

            if (_playerController != null)
            {
                _playerController.Teleport(spawnPoint.transform.position, playerRotation);
                return;
            }

            playerRoot.SetPositionAndRotation(spawnPoint.transform.position, playerRotation);
        }

        private void RestorePlayerVitals()
        {
            _playerHealth?.ResetHealth();
            _tagContainer?.ClearTags();
        }

        private void ClearPlayerCombatContext()
        {
            _playerTargeting?.ClearHardLock();
            _playerTargeting?.ClearTarget();
            partyCombatContext?.ClearCombatContext();
        }

        private bool IsPlayerObject(GameObject interactor)
        {
            if (playerRoot == null || interactor == null)
            {
                return false;
            }

            Transform interactorTransform = interactor.transform;
            return interactorTransform == playerRoot || interactorTransform.IsChildOf(playerRoot);
        }

        private void CachePlayerReferences()
        {
            if (playerRoot == null)
            {
                return;
            }

            playerRoot.TryGetComponent(out _playerController);
            playerRoot.TryGetComponent(out _playerStateMachine);
            playerRoot.TryGetComponent(out _playerHealth);
            playerRoot.TryGetComponent(out _tagContainer);
            playerRoot.TryGetComponent(out _playerTargeting);
        }

        private void SubscribePlayerDeath()
        {
            if (_subscribedToPlayerDeath || _playerHealth == null)
            {
                return;
            }

            _playerHealth.OnDead.AddListener(HandlePlayerDead);
            _subscribedToPlayerDeath = true;
        }

        private void UnsubscribePlayerDeath()
        {
            if (!_subscribedToPlayerDeath || _playerHealth == null)
            {
                return;
            }

            _playerHealth.OnDead.RemoveListener(HandlePlayerDead);
            _subscribedToPlayerDeath = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _active = null;
        }
    }

    /// <summary>出生点事件，参数为当前生效的 WorldSpawnPoint。</summary>
    [Serializable]
    public sealed class WorldSpawnPointEvent : UnityEvent<WorldSpawnPoint>
    {
    }
}
