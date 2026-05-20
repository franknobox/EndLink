using UnityEngine;

namespace EndLink.Combat
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

        private float _nextAssistTime;

        /// <summary>当前队友助战动作配置。</summary>
        public CombatActionDefinition AssistAction => assistAction;

        /// <summary>当前是否已经过了动作冷却，可以尝试执行助战。</summary>
        public bool CanAssist => assistAction != null && Time.time >= _nextAssistTime;

        /// <summary>
        /// 运行时替换助战动作。
        /// 主要用于调试、后续队伍配置系统，或简单编译/PlayMode 测试。
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
            if (assistAction == null)
            {
                LogFailure("AllyCombatDriver 缺少 Assist Action，无法执行助战。");
                return false;
            }

            if (!CanAssist)
            {
                return false;
            }

            GameObject hitboxPrefab = assistAction.HitboxPrefab;

            if (hitboxPrefab == null)
            {
                LogFailure("AllyCombatDriver 的 Assist Action 缺少 Hitbox Prefab。");
                return false;
            }

            Vector3 forward = ResolveAttackForward(target);

            if (faceTargetBeforeAttack)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            Vector3 spawnPosition = transform.position
                + forward * assistAction.HitboxSpawnDistance
                + Vector3.up * assistAction.HitboxSpawnHeight;
            Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

            GameObject hitboxInstance = Instantiate(hitboxPrefab, spawnPosition, spawnRotation);

            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);
                hitbox.Configure(
                    assistAction.DamageAmount,
                    assistAction.KnockbackForce,
                    assistAction.CombatTagToApply,
                    assistAction.CombatTagDuration);
            }

            Destroy(hitboxInstance, assistAction.HitboxLifetime);

            _nextAssistTime = Time.time + assistAction.Cooldown;
            CombatEventsBus.RaiseActionStarted(
                gameObject,
                target != null ? target.gameObject : null,
                assistAction);

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
    }
}
