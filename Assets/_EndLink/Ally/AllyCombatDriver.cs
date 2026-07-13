using System.Collections.Generic;
using EndLink.Combat;
using EndLink.Core;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友战斗执行器。
    /// 只负责按 CombatActionDefinition 执行动作表现和 Hitbox 判定，不监听输入、不订阅事件、不决定何时出手。
    /// 自动助战、主动技能、连携技共享执行逻辑，但各自按动作资产独立计算冷却。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllyCombatDriver : MonoBehaviour, ICombatActionExecutor, ICombatAnimationEventListener
    {
        private const float AnimationEventTimeoutPadding = 1f;

        [Header("动作配置")]
        [Tooltip("队友自动助战使用的动作配置。当前用于主控命中敌人后，队友自动接近并持续攻击。")]
        [SerializeField]
        private CombatActionDefinition assistAction;

        [Tooltip("队友主动技能动作。由 PartyCombatRouter 的队友技能命令触发。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        [Tooltip("队友连携技动作配置。实际释放必须由连携窗口授权。")]
        [SerializeField]
        private CombatActionDefinition linkAction;

        [Header("瞄准")]
        [Tooltip("执行动作时是否先把队友水平转向目标。关闭后会使用队友当前 Z 轴正前方生成 Hitbox。")]
        [SerializeField]
        private bool faceTargetBeforeAttack = true;

        [Tooltip("目标距离过近导致方向不稳定时使用的备用前方方向。")]
        [SerializeField]
        private Vector3 fallbackForward = Vector3.forward;

        [Header("调试")]
        [Tooltip("配置缺失等执行失败情况是否打印 Debug.LogWarning。")]
        [SerializeField]
        private bool logExecutionFailures = true;

        private readonly ActionCooldownTracker<CombatActionDefinition> _cooldowns = new();
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

        /// <summary>队友自动助战动作配置。</summary>
        public CombatActionDefinition AssistAction => assistAction;

        /// <summary>队友主动技能动作配置。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>队友连携技动作配置。</summary>
        public CombatActionDefinition LinkAction => linkAction;

        /// <summary>是否已经配置自动助战动作。</summary>
        public bool HasAssistAction => assistAction != null;

        /// <summary>当前助战动作自己的冷却剩余时间，单位秒。</summary>
        public float AssistCooldownRemaining => GetCooldownRemaining(assistAction);

        /// <summary>最近一次成功执行动作的冷却剩余时间，单位秒。主要用于调试窗口。</summary>
        public float ActionCooldownRemaining => GetCooldownRemaining(_lastExecutedAction);

        /// <summary>最近一次成功执行动作的冷却总时长，单位秒。主要用于调试窗口。</summary>
        public float ActionCooldownDuration => _lastExecutedAction != null ? Mathf.Max(0f, _lastExecutedAction.Cooldown) : 0f;

        /// <summary>最近一次成功执行动作的归一化冷却，1 表示刚进入冷却，0 表示冷却结束。</summary>
        public float ActionCooldownNormalized
        {
            get
            {
                return ActionCooldownDuration > 0f
                    ? Mathf.Clamp01(ActionCooldownRemaining / ActionCooldownDuration)
                    : 0f;
            }
        }

        /// <summary>最近一次成功执行动作是否还在冷却中。</summary>
        public bool IsActionCoolingDown => ActionCooldownRemaining > 0f;

        /// <summary>
        /// 查询指定动作当前的冷却归一化进度。
        /// UI 使用这个接口读取队友主动技能槽，避免自动助战动作把主动技能 UI 染灰。
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

        /// <summary>当前是否已经过了助战动作自己的冷却，可以执行一次助战攻击。</summary>
        public bool CanAssist => CanExecute(assistAction);

        /// <summary>当前是否仍有动作正在执行。</summary>
        public bool IsExecutingAction => _currentActionDefinition != null;

        /// <summary>当前动作阶段。动画事件模式会随判定事件更新。</summary>
        public CombatActionPhase CurrentActionPhase => _currentActionDefinition == null
            ? CombatActionPhase.Completed
            : _currentActionDefinition.TimingSource == CombatActionTimingSource.AnimationEventDriven
                ? _animationEventPhase
                : _currentActionTimeline?.Phase ?? CombatActionPhase.Completed;

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

        /// <summary>判断指定动作当前是否具备基础执行条件。</summary>
        public bool CanExecute(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null
                && actionDefinition.HitboxPrefab != null
                && _currentActionDefinition == null
                && _cooldowns.IsReady(actionDefinition, Time.time);
        }

        /// <summary>
        /// 运行时替换助战动作。主要用于调试、队伍配置系统或简单 PlayMode 测试。
        /// </summary>
        public void SetAssistAction(CombatActionDefinition action)
        {
            assistAction = action;
        }

        /// <summary>
        /// 执行一次自动助战动作。调用者负责判断现在是否应该出手。
        /// </summary>
        public bool ExecuteAssist(Transform target)
        {
            return TryExecute(assistAction, target);
        }

        /// <summary>
        /// 执行指定队友动作。
        /// 调用者负责判断动作来自自动助战、玩家命令技能，还是连携窗口授权的连携攻击。
        /// </summary>
        public bool TryExecute(CombatActionDefinition actionDefinition, Transform target = null)
        {
            if (actionDefinition == null)
            {
                AllyDebugLog.Raise(gameObject, AllyDebugCategory.Combat, "execute action failed: missing action definition");
                LogFailure("AllyCombatDriver 缺少动作配置，无法执行动作。");
                return false;
            }

            GameObject hitboxPrefab = actionDefinition.HitboxPrefab;

            if (hitboxPrefab == null)
            {
                AllyDebugLog.Raise(
                    gameObject,
                    AllyDebugCategory.Combat,
                    $"execute action failed: action={actionDefinition.ActionId} missing hitbox prefab");
                LogFailure($"AllyCombatDriver 的动作 {actionDefinition.ActionId} 缺少 Hitbox Prefab。");
                return false;
            }

            if (!CanExecute(actionDefinition))
            {
                AllyDebugLog.Raise(
                    gameObject,
                    AllyDebugCategory.Combat,
                    $"execute action skipped: action={actionDefinition.ActionId}, cooldown remaining={GetCooldownRemaining(actionDefinition):F2}");
                return false;
            }

            Vector3 forward = ResolveAttackForward(target);

            if (faceTargetBeforeAttack)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            _lastExecutedAction = actionDefinition;
            _cooldowns.StartCooldown(actionDefinition, Time.time, actionDefinition.Cooldown);
            StartActionExecution(actionDefinition, target, forward);
            _actionLockReceiver?.NotifyActionStarted();

            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.Combat,
                $"execute action={actionDefinition.ActionId}, target={GetTransformName(target)}, nextCd={actionDefinition.Cooldown:F2}");

            CombatEventsBus.RaiseActionStarted(
                gameObject,
                target != null ? target.gameObject : null,
                actionDefinition);

            return true;
        }

        public float GetCooldownRemaining(CombatActionDefinition actionDefinition)
        {
            return _cooldowns.GetRemaining(actionDefinition, Time.time);
        }

        /// <summary>
        /// 打断当前动作。普通驻留 Hitbox 会立即关闭，已经发射的 Projectile 保留自身生命周期。
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
                            $"AllyCombatDriver 的动画驱动动作 {_currentActionDefinition.ActionId} 未及时收到 ActionEnd，已按数据总时长安全结束。");
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

            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);
                hitbox.Configure(
                    _currentActionDefinition.FlatDamage,
                    _currentActionDefinition.DamageType,
                    _currentActionDefinition.KnockbackForce,
                    _currentActionDefinition.CombatTagToApply,
                    _currentActionDefinition.CombatTagDuration,
                    _currentActionDefinition.CombatTagStackCount,
                    _currentActionDefinition);

                if (hitbox is not HitboxProjectile)
                {
                    _activeHitboxInstance = hitboxInstance;
                }

                return;
            }

            _activeHitboxInstance = hitboxInstance;

            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.Combat,
                $"spawned hitbox missing HitboxBase: prefab={hitboxInstance.name}, action={_currentActionDefinition.ActionId}");
            LogFailure($"AllyCombatDriver 生成的 Hitbox 预制体 {hitboxInstance.name} 缺少 HitboxBase。");
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

        private void LogFailure(string message)
        {
            if (logExecutionFailures)
            {
                Debug.LogWarning(message, this);
            }
        }

        private static string GetTransformName(Transform target)
        {
            return target != null ? target.name : "None";
        }

        /// <summary>
        /// 按动作资产分别记录冷却结束时间。
        /// 泛型用于轻量测试，运行时实际使用 CombatActionDefinition 作为 key。
        /// </summary>
        public sealed class ActionCooldownTracker<TAction>
            where TAction : class
        {
            private readonly Dictionary<TAction, float> _nextReadyTimes = new();

            public bool IsReady(TAction action, float currentTime)
            {
                return GetRemaining(action, currentTime) <= 0f;
            }

            public float GetRemaining(TAction action, float currentTime)
            {
                if (action == null)
                {
                    return 0f;
                }

                return _nextReadyTimes.TryGetValue(action, out float nextReadyTime)
                    ? Mathf.Max(0f, nextReadyTime - currentTime)
                    : 0f;
            }

            public void StartCooldown(TAction action, float currentTime, float cooldown)
            {
                if (action == null)
                {
                    return;
                }

                _nextReadyTimes[action] = currentTime + Mathf.Max(0f, cooldown);
            }
        }
    }
}
