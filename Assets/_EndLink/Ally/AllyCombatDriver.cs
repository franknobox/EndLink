using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友战斗执行器。
    /// 只负责按 CombatActionDefinition 执行动作表现和 Hitbox 判定，不监听输入、不订阅事件、不决定何时出手。
    /// 自动助战、主动技能、连携技共享执行逻辑，但各自按动作资产独立计算冷却。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllyCombatDriver : MonoBehaviour
    {
        [Header("动作配置")]
        [Tooltip("队友自动助战使用的动作配置。当前用于主控命中敌人后，队友自动接近并持续攻击。")]
        [SerializeField]
        private CombatActionDefinition assistAction;

        [Tooltip("队友主动技能动作。由 PartyCombatRouter 的队友技能命令触发。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        [Tooltip("队友连携技动作配置。实际释放必须由后续连携机制授权。")]
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
        private CombatActionDefinition _lastExecutedAction;

        /// <summary>队友自动助战动作配置。</summary>
        public CombatActionDefinition AssistAction => assistAction;

        /// <summary>队友主动技能动作配置。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>队友连携技动作配置。</summary>
        public CombatActionDefinition LinkAction => linkAction;

        /// <summary>是否已经配置自动助战动作。</summary>
        public bool HasAssistAction => assistAction != null;

        /// <summary>当前助战动作自己的冷却剩余时间，单位秒。</summary>
        public float AssistCooldownRemaining => GetActionCooldownRemaining(assistAction);

        /// <summary>最近一次成功执行动作的冷却剩余时间，单位秒。主要用于调试窗口。</summary>
        public float ActionCooldownRemaining => GetActionCooldownRemaining(_lastExecutedAction);

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
        public float GetActionCooldownNormalized(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null)
            {
                return 0f;
            }

            float cooldown = Mathf.Max(0f, actionDefinition.Cooldown);
            return cooldown > 0f ? Mathf.Clamp01(GetActionCooldownRemaining(actionDefinition) / cooldown) : 0f;
        }

        /// <summary>当前是否已经过了助战动作自己的冷却，可以执行一次助战攻击。</summary>
        public bool CanAssist => assistAction != null && _cooldowns.IsReady(assistAction, Time.time);

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
            return ExecuteAction(assistAction, target);
        }

        /// <summary>
        /// 执行指定队友动作。
        /// 调用者负责判断动作来自自动助战、玩家命令技能，还是连携机制授权的连携攻击。
        /// </summary>
        public bool ExecuteAction(CombatActionDefinition actionDefinition, Transform target)
        {
            if (actionDefinition == null)
            {
                AllyDebugLog.Raise(gameObject, AllyDebugCategory.Combat, "execute action failed: missing action definition");
                LogFailure("AllyCombatDriver 缺少动作配置，无法执行动作。");
                return false;
            }

            if (!_cooldowns.IsReady(actionDefinition, Time.time))
            {
                AllyDebugLog.Raise(
                    gameObject,
                    AllyDebugCategory.Combat,
                    $"execute action skipped: action={actionDefinition.ActionId}, cooldown remaining={GetActionCooldownRemaining(actionDefinition):F2}");
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

            Vector3 forward = ResolveAttackForward(target);

            if (faceTargetBeforeAttack)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            Vector3 spawnPosition = transform.position
                + forward * actionDefinition.HitboxSpawnDistance
                + Vector3.up * actionDefinition.HitboxSpawnHeight;
            Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

            GameObject hitboxInstance = Instantiate(hitboxPrefab, spawnPosition, spawnRotation);

            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);
                hitbox.Configure(
                    actionDefinition.DamageAmount,
                    actionDefinition.KnockbackForce,
                    actionDefinition.CombatTagToApply,
                    actionDefinition.CombatTagDuration,
                    actionDefinition.CombatTagStackCount);
            }
            else
            {
                AllyDebugLog.Raise(
                    gameObject,
                    AllyDebugCategory.Combat,
                    $"spawned hitbox has no HitboxBase, prefab={hitboxPrefab.name}");
            }

            _lastExecutedAction = actionDefinition;
            _cooldowns.StartCooldown(actionDefinition, Time.time, actionDefinition.Cooldown);

            AllyDebugLog.Raise(
                gameObject,
                AllyDebugCategory.Combat,
                $"execute action={actionDefinition.ActionId}, target={GetTransformName(target)}, spawn={spawnPosition}, nextCd={actionDefinition.Cooldown:F2}");

            CombatEventsBus.RaiseActionStarted(
                gameObject,
                target != null ? target.gameObject : null,
                actionDefinition);

            return true;
        }

        private float GetActionCooldownRemaining(CombatActionDefinition actionDefinition)
        {
            return _cooldowns.GetRemaining(actionDefinition, Time.time);
        }

        private Vector3 ResolveAttackForward(Transform target)
        {
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
