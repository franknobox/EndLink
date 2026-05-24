using EndLink.Combat;
using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友战斗执行器。
    /// 只负责按照 CombatActionDefinition 执行一次助战动作：朝向目标、生成 Hitbox、写入伤害/标签数据、广播动作开始事件。
    /// 它不监听输入、不订阅事件、不决定什么时候出手；这些决策由 AllyBrain 或后续更完整的 AI 层负责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AllyCombatDriver : MonoBehaviour
    {
        [Header("动作配置")]
        [Tooltip("队友助战使用的动作配置。当前胶囊白模阶段建议先配置成 LinkAttack 或 BasicAttack，用于验证队友能响应事件并生成 Hitbox。")]
        [SerializeField]
        private CombatActionDefinition assistAction;

        [Tooltip("队友主动技能动作。后续由 PartyCombatRouter 的队友技能命令触发。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        [Tooltip("队友连携技动作配置。不能被普通输入直接释放，必须由后续连携机制确认窗口后调用。")]
        [SerializeField]
        private CombatActionDefinition linkAction;

        [Header("瞄准")]
        [Tooltip("执行助战时是否先把队友水平转向目标。关闭后会始终使用队友当前 Z 轴正前方生成 Hitbox。")]
        [SerializeField]
        private bool faceTargetBeforeAttack = true;

        [Tooltip("目标距离过近导致方向不稳定时使用的备用前方方向。通常保持默认即可。")]
        [SerializeField]
        private Vector3 fallbackForward = Vector3.forward;

        [Header("调试")]
        [Tooltip("配置缺失、冷却未结束等导致执行失败时是否打印 Debug.LogWarning。")]
        [SerializeField]
        private bool logExecutionFailures = true;

        private float _nextActionTime;
        private float _lastActionCooldown;
        private CombatActionDefinition _lastCooldownAction;

        /// <summary>当前队友助战动作配置。</summary>
        public CombatActionDefinition AssistAction => assistAction;

        /// <summary>队友主动技能动作。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>队友连携技动作配置。实际释放必须由连携机制授权。</summary>
        public CombatActionDefinition LinkAction => linkAction;

        /// <summary>是否已经配置助战动作。用于判断队友能否进入助战流程，不代表冷却已经结束。</summary>
        public bool HasAssistAction => assistAction != null;

        /// <summary>当前助战动作剩余冷却时间。</summary>
        public float AssistCooldownRemaining => ActionCooldownRemaining;

        /// <summary>当前动作冷却剩余时间，单位秒。</summary>
        public float ActionCooldownRemaining => Mathf.Max(0f, _nextActionTime - Time.time);

        /// <summary>最近一次成功执行动作写入的冷却总时长，单位秒。</summary>
        public float ActionCooldownDuration => _lastActionCooldown;

        /// <summary>当前动作冷却归一化进度，1 表示刚进入冷却，0 表示冷却结束。</summary>
        public float ActionCooldownNormalized
        {
            get
            {
                return _lastActionCooldown > 0f
                    ? Mathf.Clamp01(ActionCooldownRemaining / _lastActionCooldown)
                    : 0f;
            }
        }

        /// <summary>当前是否处于动作冷却中。</summary>
        public bool IsActionCoolingDown => ActionCooldownRemaining > 0f;

        /// <summary>
        /// 查询指定动作当前的冷却归一化进度。
        /// 当前第一版队友只有一个动作锁，但 UI 需要知道“这个槽位自己的动作”是否在冷却，避免助战动作染灰主动技能槽。
        /// </summary>
        public float GetActionCooldownNormalized(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null || _lastCooldownAction != actionDefinition)
            {
                return 0f;
            }

            return ActionCooldownNormalized;
        }

        /// <summary>当前是否已经过了动作冷却，可以真正执行一次助战攻击。</summary>
        public bool CanAssist => assistAction != null && Time.time >= _nextActionTime;

        /// <summary>
        /// 运行时替换助战动作。
        /// 主要用于调试、后续队伍配置系统，或简单 PlayMode 测试。
        /// </summary>
        public void SetAssistAction(CombatActionDefinition action)
        {
            assistAction = action;
        }

        /// <summary>
        /// 执行一次助战动作。
        /// 调用者负责判断是否应该出手；这里仅做执行所需的冷却和资源防御检查。
        /// </summary>
        public bool ExecuteAssist(Transform target)
        {
            return ExecuteAction(assistAction, target);
        }

        /// <summary>
        /// 执行指定队友动作。
        /// 调用者负责判断这个动作来自自动助战、玩家命令技能还是已被连携机制授权的连携技。
        /// </summary>
        public bool ExecuteAction(CombatActionDefinition actionDefinition, Transform target)
        {
            if (actionDefinition == null)
            {
                AllyDebugLog.Raise(gameObject, AllyDebugCategory.Combat, "execute action failed: missing action definition");
                LogFailure("AllyCombatDriver 缺少动作配置，无法执行动作。");
                return false;
            }

            if (Time.time < _nextActionTime)
            {
                AllyDebugLog.Raise(
                    gameObject,
                    AllyDebugCategory.Combat,
                    $"execute action skipped: cooldown remaining={AssistCooldownRemaining:F2}");
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
                    actionDefinition.CombatTagDuration);
            }
            else
            {
                AllyDebugLog.Raise(
                    gameObject,
                    AllyDebugCategory.Combat,
                    $"spawned hitbox has no HitboxBase, prefab={hitboxPrefab.name}");
            }

            Destroy(hitboxInstance, actionDefinition.HitboxLifetime);

            _lastCooldownAction = actionDefinition;
            _lastActionCooldown = actionDefinition.Cooldown;
            _nextActionTime = Time.time + _lastActionCooldown;
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
    }
}
