using System.Collections.Generic;
using EndLink.Core;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家战斗驱动器。
    /// 只保存玩家可释放的动作槽位，并按照 CombatActionDefinition 执行动作表现和 Hitbox 判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatDriver : MonoBehaviour, ICombatActionExecutor, ICombatAnimationEventListener
    {
        private const float AnimationEventTimeoutPadding = 1f;

        [Header("动作槽位")]
        [Tooltip("玩家普攻动作。鼠标左键会由玩家状态机触发该动作。")]
        [SerializeField]
        private CombatActionDefinition basicAttackAction;

        [Tooltip("玩家主动技能动作。由 PartyCombatRouter 的主控技能命令触发。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        [Tooltip("玩家连携技动作配置。不能被普通输入直接释放，必须由连携窗口确认后调用。")]
        [SerializeField]
        private CombatActionDefinition linkAction;

        private PlayerTargeting _targeting;
        private PlayerController _playerController;
        private ICombatActionLockReceiver _actionLockReceiver;
        private readonly Dictionary<CombatActionDefinition, float> _nextReadyTimes = new();
        private CombatActionDefinition _lastExecutedAction;
        private CombatActionDefinition _currentActionDefinition;
        private Transform _currentActionTarget;
        private Vector3 _currentActionForward = Vector3.forward;
        private CombatActionTimeline _currentActionTimeline;
        private CombatActionPhase _animationEventPhase = CombatActionPhase.Completed;
        private GameObject _activeHitboxInstance;
        private float _animationEventElapsed;
        private bool _hasTriggeredActionEffect;
        private bool _hasEndedHitboxWindow;
        private bool _hasLoggedAnimationTimeout;

        /// <summary>
        /// 动作成功开始时触发。供 Animator 桥接层写入 ActionId、ActionType 并触发对应动画。
        /// </summary>
        public event System.Action<CombatActionDefinition> ActionStarted;

        /// <summary>玩家普攻动作。</summary>
        public CombatActionDefinition BasicAttackAction => basicAttackAction;

        /// <summary>玩家主动技能动作。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>玩家连携技动作配置。实际释放必须由连携窗口授权。</summary>
        public CombatActionDefinition LinkAction => linkAction;

        /// <summary>当前是否可以释放下一次动作。</summary>
        public bool CanAttack => CanExecute(basicAttackAction);

        /// <summary>当前动作冷却剩余时间，单位秒。</summary>
        public float ActionCooldownRemaining => GetCooldownRemaining(_lastExecutedAction);

        /// <summary>最近一次成功执行动作写入的冷却总时长，单位秒。</summary>
        public float ActionCooldownDuration => _lastExecutedAction != null
            ? Mathf.Max(0f, _lastExecutedAction.Cooldown)
            : 0f;

        /// <summary>当前动作冷却归一化进度，1 表示刚进入冷却，0 表示冷却结束。</summary>
        public float ActionCooldownNormalized
        {
            get
            {
                return ActionCooldownDuration > 0f
                    ? Mathf.Clamp01(ActionCooldownRemaining / ActionCooldownDuration)
                    : 0f;
            }
        }

        /// <summary>当前是否处于动作冷却中。</summary>
        public bool IsActionCoolingDown => ActionCooldownRemaining > 0f;

        /// <summary>当前是否仍有动作时序正在推进。</summary>
        public bool IsExecutingAction => _currentActionDefinition != null;

        /// <summary>当前动作阶段。动画事件模式会随 HitboxStart / HitboxEnd 更新。</summary>
        public CombatActionPhase CurrentActionPhase => _currentActionDefinition == null
            ? CombatActionPhase.Completed
            : _currentActionDefinition.TimingSource == CombatActionTimingSource.AnimationEventDriven
                ? _animationEventPhase
                : _currentActionTimeline?.Phase ?? CombatActionPhase.Completed;

        /// <summary>当前正在执行的动作资产。</summary>
        public CombatActionDefinition CurrentActionDefinition => _currentActionDefinition;

        /// <summary>
        /// 查询指定动作当前的冷却归一化进度。
        /// 玩家按动作资产独立记录冷却，普攻、技能和连携技不会互相覆盖冷却。
        /// </summary>
        public float GetCooldownNormalized(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null)
            {
                return 0f;
            }

            float cooldown = Mathf.Max(0f, actionDefinition.Cooldown);
            return cooldown > 0f
                ? Mathf.Clamp01(GetCooldownRemaining(actionDefinition) / cooldown)
                : 0f;
        }

        private void Awake()
        {
            TryGetComponent(out _targeting);
            TryGetComponent(out _playerController);
            _actionLockReceiver = GetComponent<ICombatActionLockReceiver>();
        }

        private void Update()
        {
            TickCurrentAction(Time.deltaTime);
        }

        private void OnDisable()
        {
            CancelCurrentAction();
        }

        /// <summary>
        /// 执行普攻动作。
        /// </summary>
        public bool ExecuteAttack()
        {
            return TryExecute(basicAttackAction);
        }

        /// <summary>
        /// 判断指定动作当前是否具备最基础的执行条件。
        /// 状态机可以用它决定是否接受动作请求，避免请求成功后才发现动作仍在冷却或缺少 Hitbox。
        /// </summary>
        public bool CanExecute(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null
                && actionDefinition.HitboxPrefab != null
                && _currentActionDefinition == null
                && GetCooldownRemaining(actionDefinition) <= 0f;
        }

        /// <summary>
        /// 执行指定玩家动作。
        /// 该方法不判断玩家状态机是否允许出手，只负责动作资源、冷却和 Hitbox 执行。
        /// </summary>
        public bool TryExecute(CombatActionDefinition actionDefinition, Transform targetOverride = null)
        {
            if (actionDefinition == null)
            {
                Debug.LogWarning("PlayerCombatDriver 缺少动作配置，无法执行动作。", this);
                return false;
            }

            if (actionDefinition.HitboxPrefab == null)
            {
                Debug.LogWarning($"PlayerCombatDriver 的动作 {actionDefinition.ActionId} 缺少 Hitbox Prefab。", this);
                return false;
            }

            if (!CanExecute(actionDefinition))
            {
                return false;
            }

            Vector3 attackForward = ResolveAttackForward(targetOverride);
            FaceAttackDirection(attackForward);
            Vector3 hitboxForward = ResolveCurrentForward(attackForward);

            GameObject actionTarget = targetOverride != null
                ? targetOverride.gameObject
                : GetCurrentTargetObject();
            CombatEventsBus.RaiseActionStarted(gameObject, actionTarget, actionDefinition);
            _lastExecutedAction = actionDefinition;
            _nextReadyTimes[actionDefinition] = Time.time + Mathf.Max(0f, actionDefinition.Cooldown);
            StartActionExecution(actionDefinition, targetOverride, hitboxForward);
            _actionLockReceiver?.NotifyActionStarted();
            ActionStarted?.Invoke(actionDefinition);
            return true;
        }

        /// <inheritdoc />
        public float GetCooldownRemaining(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null)
            {
                return 0f;
            }

            return _nextReadyTimes.TryGetValue(actionDefinition, out float nextReadyTime)
                ? Mathf.Max(0f, nextReadyTime - Time.time)
                : 0f;
        }

        /// <summary>
        /// 取消尚未完成的玩家动作时序。
        /// 受击、死亡或主动退出攻击状态时调用，避免前摇中的 Hitbox 在状态结束后继续生成。
        /// </summary>
        public void CancelCurrentAction()
        {
            if (_currentActionDefinition == null)
            {
                return;
            }

            EndCurrentHitbox();
            ClearCurrentActionExecution();
            _actionLockReceiver?.NotifyActionInterrupted();
        }

        private void FaceAttackDirection(Vector3 attackForward)
        {
            if (_playerController == null)
            {
                TryGetComponent(out _playerController);
            }

            if (_playerController != null)
            {
                _playerController.FaceDirection(attackForward, true);
                return;
            }

            if (attackForward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(attackForward, Vector3.up);
            }
        }

        private Vector3 ResolveCurrentForward(Vector3 fallbackForward)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                return forward.normalized;
            }

            return fallbackForward.sqrMagnitude > 0.0001f
                ? fallbackForward.normalized
                : Vector3.forward;
        }

        private Vector3 ResolveAttackForward(Transform targetOverride)
        {
            Transform attackTarget = ResolveLockPoint(targetOverride);
            if (attackTarget == null && _targeting != null && _targeting.HasTarget)
            {
                attackTarget = _targeting.CurrentLockPoint;
            }

            if (attackTarget != null)
            {
                Vector3 toTarget = attackTarget.position - transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    return toTarget.normalized;
                }
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;

            return forward.sqrMagnitude > 0.0001f
                ? forward.normalized
                : Vector3.forward;
        }

        private static Transform ResolveLockPoint(Transform target)
        {
            return target != null
                && CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget)
                    ? combatTarget.LockPoint
                    : target;
        }

        private void ConfigureHitbox(GameObject hitboxInstance, CombatActionDefinition actionDefinition)
        {
            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);
                hitbox.Configure(
                    actionDefinition.FlatDamage,
                    actionDefinition.DamageType,
                    actionDefinition.KnockbackForce,
                    actionDefinition.CombatTagToApply,
                    actionDefinition.CombatTagDuration,
                    actionDefinition.CombatTagStackCount,
                    actionDefinition);
            }
        }

        private void StartActionExecution(CombatActionDefinition actionDefinition, Transform targetOverride, Vector3 hitboxForward)
        {
            _currentActionDefinition = actionDefinition;
            _currentActionTarget = targetOverride;
            _currentActionForward = hitboxForward.sqrMagnitude > 0.0001f
                ? hitboxForward.normalized
                : Vector3.forward;
            _animationEventElapsed = 0f;
            _animationEventPhase = CombatActionPhase.Startup;
            _hasTriggeredActionEffect = false;
            _hasEndedHitboxWindow = false;
            _hasLoggedAnimationTimeout = false;
            _activeHitboxInstance = null;

            if (actionDefinition.TimingSource == CombatActionTimingSource.AnimationEventDriven)
            {
                _currentActionTimeline = null;
                return;
            }

            _currentActionTimeline = new CombatActionTimeline(
                actionDefinition.StartupTime,
                actionDefinition.ActiveTime,
                actionDefinition.RecoveryTime);
            _currentActionTimeline.Begin(out bool triggerEffect);

            if (triggerEffect)
            {
                TriggerCurrentActionEffect();
            }
        }

        private void TickCurrentAction(float deltaTime)
        {
            if (_currentActionDefinition == null)
            {
                return;
            }

            if (_currentActionDefinition.TimingSource == CombatActionTimingSource.AnimationEventDriven)
            {
                _animationEventElapsed += Mathf.Max(0f, deltaTime);
                float timeout = Mathf.Max(
                    AnimationEventTimeoutPadding,
                    _currentActionDefinition.TotalDuration + AnimationEventTimeoutPadding);
                if (_animationEventElapsed >= timeout)
                {
                    if (!_hasLoggedAnimationTimeout)
                    {
                        _hasLoggedAnimationTimeout = true;
                        Debug.LogWarning(
                            $"PlayerCombatDriver 的动画驱动动作 {_currentActionDefinition.ActionId} 未及时收到 ActionEnd，已按数据总时长安全结束。",
                            this);
                    }

                    CompleteCurrentAction();
                }

                return;
            }

            if (_currentActionTimeline == null)
            {
                CompleteCurrentAction();
                return;
            }

            CombatActionPhase previousPhase = _currentActionTimeline.Phase;
            _currentActionTimeline.Tick(deltaTime, out bool triggerEffect, out bool completed);

            if (triggerEffect)
            {
                TriggerCurrentActionEffect();
            }

            if (previousPhase != CombatActionPhase.Recovery
                && _currentActionTimeline.Phase == CombatActionPhase.Recovery)
            {
                EndCurrentHitbox();
            }

            if (completed)
            {
                CompleteCurrentAction();
            }
        }

        /// <inheritdoc />
        public void OnActionHitboxStart()
        {
            if (!IsCurrentActionAnimationDriven() || _hasEndedHitboxWindow)
            {
                return;
            }

            _animationEventPhase = CombatActionPhase.Active;
            TriggerCurrentActionEffect();
        }

        /// <inheritdoc />
        public void OnActionHitboxEnd()
        {
            if (!IsCurrentActionAnimationDriven())
            {
                return;
            }

            _hasEndedHitboxWindow = true;
            _animationEventPhase = CombatActionPhase.Recovery;
            EndCurrentHitbox();
        }

        /// <inheritdoc />
        public void OnActionCanCancel()
        {
            if (IsCurrentActionAnimationDriven())
            {
                _actionLockReceiver?.NotifyActionCanCancel();
            }
        }

        /// <inheritdoc />
        public void OnActionEnd()
        {
            if (!IsCurrentActionAnimationDriven())
            {
                return;
            }

            _animationEventPhase = CombatActionPhase.Completed;
            CompleteCurrentAction();
        }

        private void TriggerCurrentActionEffect()
        {
            if (_currentActionDefinition == null
                || _hasTriggeredActionEffect
                || _hasEndedHitboxWindow)
            {
                return;
            }

            _hasTriggeredActionEffect = true;

            // 判定生成时读取角色实时正前方，使前摇期间的软锁跟随转向能同步影响 Hitbox 朝向。
            Vector3 spawnForward = ResolveCurrentForward(_currentActionForward);
            Vector3 spawnPosition = transform.position
                + spawnForward * _currentActionDefinition.HitboxSpawnDistance
                + Vector3.up * _currentActionDefinition.HitboxSpawnHeight;
            Quaternion spawnRotation = Quaternion.LookRotation(spawnForward, Vector3.up);

            GameObject hitboxInstance = Instantiate(_currentActionDefinition.HitboxPrefab, spawnPosition, spawnRotation);
            ConfigureHitbox(hitboxInstance, _currentActionDefinition);

            if (!hitboxInstance.TryGetComponent<HitboxProjectile>(out _))
            {
                _activeHitboxInstance = hitboxInstance;
            }
        }

        private void EndCurrentHitbox()
        {
            if (_activeHitboxInstance == null)
            {
                return;
            }

            _activeHitboxInstance.SetActive(false);
            Destroy(_activeHitboxInstance);
            _activeHitboxInstance = null;
        }

        private void CompleteCurrentAction()
        {
            if (_currentActionDefinition == null)
            {
                return;
            }

            EndCurrentHitbox();
            ClearCurrentActionExecution();
            _actionLockReceiver?.NotifyActionEnd();
        }

        private bool IsCurrentActionAnimationDriven()
        {
            return _currentActionDefinition != null
                && _currentActionDefinition.TimingSource == CombatActionTimingSource.AnimationEventDriven;
        }

        private void ClearCurrentActionExecution()
        {
            _currentActionTimeline = null;
            _currentActionDefinition = null;
            _currentActionTarget = null;
            _currentActionForward = Vector3.forward;
            _animationEventPhase = CombatActionPhase.Completed;
            _activeHitboxInstance = null;
            _animationEventElapsed = 0f;
            _hasTriggeredActionEffect = false;
            _hasEndedHitboxWindow = false;
            _hasLoggedAnimationTimeout = false;
        }

        private GameObject GetCurrentTargetObject()
        {
            if (_targeting == null || !_targeting.HasTarget)
            {
                return null;
            }

            return _targeting.CurrentTarget != null ? _targeting.CurrentTarget.gameObject : null;
        }
    }
}
