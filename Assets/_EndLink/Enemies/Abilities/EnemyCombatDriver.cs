using System.Collections.Generic;
using EndLink.Combat;
using EndLink.Core;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗执行器。
    /// 只负责按 CombatActionDefinition 执行动作表现和 Hitbox 判定，不决定何时出手、不选择目标、不切状态。
    /// 当前阶段作为正式近战敌人的攻击能力基底，由 Combat 内部行为层或后续行为树调用。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    public sealed class EnemyCombatDriver : MonoBehaviour, ICombatActionExecutor, ICombatAnimationEventListener
    {
        private const float AnimationEventTimeoutPadding = 1f;

        [Header("动作配置")]
        [Tooltip("敌人普通攻击动作。基础 Combat 行为会在每轮攻击前和技能一起参与选择。")]
        [SerializeField]
        private CombatActionDefinition basicAttackAction;

        [Tooltip("敌人技能动作。基础 Combat 行为会按状态机配置的技能概率选择；未配置时只使用普通攻击。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        [Header("瞄准")]
        [Tooltip("执行动作时是否先把敌人水平转向目标。关闭后会使用敌人当前 Z 轴正前方生成 Hitbox。")]
        [SerializeField]
        private bool faceTargetBeforeAttack = true;

        [Tooltip("目标距离过近导致方向不稳定时使用的备用前方方向。")]
        [SerializeField]
        private Vector3 fallbackForward = Vector3.forward;

        [Header("调试")]
        [Tooltip("动作配置缺失、Hitbox Prefab 缺失等执行失败情况是否打印 Warning。")]
        [SerializeField]
        private bool logExecutionFailures = true;

        private readonly Dictionary<CombatActionDefinition, float> _nextReadyTimes = new();
        private ICombatActionLockReceiver _actionLockReceiver;
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
        /// 动作时间线成功开始时触发。
        /// 供 Animator 桥接和表现层监听；AI 决策不应依赖该事件。
        /// </summary>
        public event System.Action<CombatActionDefinition> ActionStarted;

        /// <summary>敌人普通攻击动作。</summary>
        public CombatActionDefinition BasicAttackAction => basicAttackAction;

        /// <summary>敌人技能动作。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>当前是否仍有动作时间线正在推进。</summary>
        public bool IsExecutingAction => _currentActionDefinition != null;

        /// <summary>当前动作所处阶段。没有动作时返回 Completed。</summary>
        public CombatActionPhase CurrentActionPhase => _currentActionDefinition == null
            ? CombatActionPhase.Completed
            : _currentActionDefinition.TimingSource == CombatActionTimingSource.AnimationEventDriven
                ? _animationEventPhase
                : _currentActionTimeline?.Phase ?? CombatActionPhase.Completed;

        /// <summary>当前正在执行的动作配置。没有动作时为空。</summary>
        public CombatActionDefinition CurrentAction => _currentActionDefinition;

        /// <summary>是否已经配置普通攻击动作。</summary>
        public bool HasBasicAttackAction => basicAttackAction != null;

        /// <summary>普通攻击是否已经冷却完成。</summary>
        public bool CanBasicAttack => CanExecute(basicAttackAction);

        /// <summary>最近一次成功执行动作的冷却剩余时间。</summary>
        public float ActionCooldownRemaining => GetCooldownRemaining(_lastExecutedAction);

        /// <summary>最近一次成功执行动作的冷却总时长。</summary>
        public float ActionCooldownDuration => _lastExecutedAction != null ? Mathf.Max(0f, _lastExecutedAction.Cooldown) : 0f;

        /// <summary>最近一次成功执行动作的归一化冷却进度。</summary>
        public float ActionCooldownNormalized
        {
            get
            {
                return ActionCooldownDuration > 0f
                    ? Mathf.Clamp01(ActionCooldownRemaining / ActionCooldownDuration)
                    : 0f;
            }
        }

        /// <summary>最近一次成功执行的动作是否仍在冷却。</summary>
        public bool IsActionCoolingDown => ActionCooldownRemaining > 0f;

        private void Awake()
        {
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
        /// 查询指定动作当前的冷却剩余时间。
        /// </summary>
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
        /// 查询指定动作当前的归一化冷却进度。
        /// </summary>
        public float GetCooldownNormalized(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null)
            {
                return 0f;
            }

            float cooldown = Mathf.Max(0f, actionDefinition.Cooldown);
            return cooldown > 0f ? Mathf.Clamp01(GetCooldownRemaining(actionDefinition) / cooldown) : 0f;
        }

        /// <summary>
        /// 指定动作当前是否可以执行。
        /// </summary>
        public bool CanExecute(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null
                && actionDefinition.HitboxPrefab != null
                && _currentActionDefinition == null
                && GetCooldownRemaining(actionDefinition) <= 0f;
        }

        /// <summary>
        /// 执行一次敌人普通攻击。
        /// 调用者负责判断当前状态、攻击距离、前后摇和是否应该出手。
        /// </summary>
        public bool ExecuteBasicAttack(Transform target)
        {
            return TryExecute(basicAttackAction, target);
        }

        /// <summary>
        /// 为下一轮基础战斗行为选择普攻或技能。
        /// 两者都可用时按技能概率选择；只有一个可用时直接使用该动作；
        /// 都在冷却时返回更早就绪的动作，供 AI 提前按该动作的距离进行定位。
        /// </summary>
        public CombatActionDefinition SelectCombatAction(float skillChance, bool preferSkill = false)
        {
            if (preferSkill && skillAction != null)
            {
                return skillAction;
            }

            bool basicReady = CanExecute(basicAttackAction);
            bool skillReady = CanExecute(skillAction);

            if (basicReady && skillReady)
            {
                return Random.value < Mathf.Clamp01(skillChance)
                    ? skillAction
                    : basicAttackAction;
            }

            if (skillReady)
            {
                return skillAction;
            }

            if (basicReady)
            {
                return basicAttackAction;
            }

            if (basicAttackAction == null)
            {
                return skillAction;
            }

            if (skillAction == null)
            {
                return basicAttackAction;
            }

            return GetCooldownRemaining(skillAction) < GetCooldownRemaining(basicAttackAction)
                ? skillAction
                : basicAttackAction;
        }

        /// <summary>
        /// 取消当前尚未结束的动作时间线。
        /// 已经记录的动作冷却不会回退；普通驻留 Hitbox 会立即关闭，已经发射的弹体继续遵循自身生命周期。
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

        /// <summary>
        /// 重置动作执行器的全部运行时状态，包括当前动作、动作冷却和最近执行动作。
        /// 用于敌人生命重置、重新启用和后续对象池复用。
        /// </summary>
        public void ResetRuntimeState()
        {
            EndCurrentHitbox();
            ClearCurrentActionExecution();
            _nextReadyTimes.Clear();
            _lastExecutedAction = null;
        }

        /// <summary>
        /// 执行指定敌人动作。
        /// 当前只做 Hitbox 生成、运行时参数配置、冷却记录和事件播报。
        /// </summary>
        public bool TryExecute(CombatActionDefinition actionDefinition, Transform target = null)
        {
            if (actionDefinition == null)
            {
                LogFailure("EnemyCombatDriver 缺少动作配置，无法执行动作。");
                return false;
            }

            if (actionDefinition.HitboxPrefab == null)
            {
                LogFailure($"EnemyCombatDriver 的动作 {actionDefinition.ActionId} 缺少 Hitbox Prefab。");
                return false;
            }

            if (!CanExecute(actionDefinition))
            {
                return false;
            }

            Vector3 attackForward = ResolveAttackForward(target);

            if (faceTargetBeforeAttack)
            {
                transform.rotation = Quaternion.LookRotation(attackForward, Vector3.up);
            }

            _lastExecutedAction = actionDefinition;
            _nextReadyTimes[actionDefinition] = Time.time + Mathf.Max(0f, actionDefinition.Cooldown);
            StartActionExecution(actionDefinition, target, attackForward);
            _actionLockReceiver?.NotifyActionStarted();
            ActionStarted?.Invoke(actionDefinition);

            CombatEventsBus.RaiseActionStarted(
                gameObject,
                target != null ? target.gameObject : null,
                actionDefinition);

            return true;
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
                return;
            }

            LogFailure($"EnemyCombatDriver 生成的 Hitbox 预制体 {hitboxInstance.name} 缺少 HitboxBase。");
        }

        private void StartActionExecution(CombatActionDefinition actionDefinition, Transform target, Vector3 forward)
        {
            _currentActionDefinition = actionDefinition;
            _currentActionTarget = target;
            _currentActionForward = forward.sqrMagnitude > 0.0001f
                ? forward.normalized
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
                        LogFailure(
                            $"EnemyCombatDriver 的动画驱动动作 {_currentActionDefinition.ActionId} 未及时收到 ActionEnd，已按数据总时长安全结束。");
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

            Vector3 spawnPosition = transform.position
                + _currentActionForward * _currentActionDefinition.HitboxSpawnDistance
                + Vector3.up * _currentActionDefinition.HitboxSpawnHeight;
            Quaternion spawnRotation = Quaternion.LookRotation(_currentActionForward, Vector3.up);
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

        private Vector3 ResolveAttackForward(Transform target)
        {
            if (target != null
                && CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget))
            {
                target = combatTarget.LockPoint;
            }

            if (target != null)
            {
                Vector3 toTarget = Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up);

                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    return toTarget.normalized;
                }
            }

            Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            if (currentForward.sqrMagnitude > 0.0001f)
            {
                return currentForward.normalized;
            }

            Vector3 normalizedFallback = Vector3.ProjectOnPlane(fallbackForward, Vector3.up);
            return normalizedFallback.sqrMagnitude > 0.0001f ? normalizedFallback.normalized : Vector3.forward;
        }

        private void LogFailure(string message)
        {
            if (logExecutionFailures)
            {
                Debug.LogWarning(message, this);
            }
        }
    }
}
