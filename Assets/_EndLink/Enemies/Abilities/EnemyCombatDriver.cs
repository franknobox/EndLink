using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗执行器。
    /// 只负责按 CombatActionDefinition 执行动作表现和 Hitbox 判定，不决定何时出手、不选择目标、不切状态。
    /// 当前阶段作为正式近战敌人的攻击能力基底，由 Combat 状态内部逻辑或行为树调用。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    public sealed class EnemyCombatDriver : MonoBehaviour, ICombatActionExecutor
    {
        [Header("动作配置")]
        [Tooltip("敌人普通攻击动作。近战敌人的 Combat 行为会优先调用它。")]
        [SerializeField]
        private CombatActionDefinition basicAttackAction;

        [Tooltip("敌人技能动作。当前先预留，不会被基础 Combat 状态自动调用。")]
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
        private CombatActionDefinition _lastExecutedAction;
        private CombatActionDefinition _currentActionDefinition;
        private Transform _currentActionTarget;
        private Vector3 _currentActionForward = Vector3.forward;
        private CombatActionTimeline _currentActionTimeline;

        /// <summary>敌人普通攻击动作。</summary>
        public CombatActionDefinition BasicAttackAction => basicAttackAction;

        /// <summary>敌人技能动作。</summary>
        public CombatActionDefinition SkillAction => skillAction;

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

        private void Update()
        {
            TickCurrentAction(Time.deltaTime);
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
                && _currentActionTimeline == null
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
            if (_currentActionTimeline == null || _currentActionDefinition == null)
            {
                return;
            }

            _currentActionTimeline.Tick(deltaTime, out bool triggerEffect, out bool completed);

            if (triggerEffect)
            {
                TriggerCurrentActionEffect();
            }

            if (completed)
            {
                ClearCurrentActionExecution();
            }
        }

        private void TriggerCurrentActionEffect()
        {
            if (_currentActionDefinition == null)
            {
                return;
            }

            Vector3 spawnPosition = transform.position
                + _currentActionForward * _currentActionDefinition.HitboxSpawnDistance
                + Vector3.up * _currentActionDefinition.HitboxSpawnHeight;
            Quaternion spawnRotation = Quaternion.LookRotation(_currentActionForward, Vector3.up);
            GameObject hitboxInstance = Instantiate(_currentActionDefinition.HitboxPrefab, spawnPosition, spawnRotation);
            ConfigureHitbox(hitboxInstance, _currentActionDefinition);
        }

        private void ClearCurrentActionExecution()
        {
            _currentActionTimeline = null;
            _currentActionDefinition = null;
            _currentActionTarget = null;
            _currentActionForward = Vector3.forward;
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
