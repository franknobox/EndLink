using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家战斗驱动器。
    /// 只保存玩家可释放的动作槽位，并按照 CombatActionDefinition 执行动作表现和 Hitbox 判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatDriver : MonoBehaviour
    {
        [Header("动作槽位")]
        [Tooltip("玩家普攻动作。鼠标左键会由玩家状态机触发该动作。")]
        [SerializeField]
        private CombatActionDefinition basicAttackAction;

        [Tooltip("玩家主动技能动作。后续由 PartyCombatRouter 的 Q 命令触发。")]
        [SerializeField]
        private CombatActionDefinition skillAction;

        [Tooltip("玩家连携技动作配置。不能被普通输入直接释放，必须由后续连携机制确认窗口后调用。")]
        [SerializeField]
        private CombatActionDefinition linkAction;

        private PlayerTargeting _targeting;
        private float _nextActionTime;

        /// <summary>玩家普攻动作。</summary>
        public CombatActionDefinition BasicAttackAction => basicAttackAction;

        /// <summary>玩家主动技能动作。</summary>
        public CombatActionDefinition SkillAction => skillAction;

        /// <summary>玩家连携技动作配置。实际释放必须由连携机制授权。</summary>
        public CombatActionDefinition LinkAction => linkAction;

        /// <summary>当前是否可以释放下一次动作。</summary>
        public bool CanAttack => Time.time >= _nextActionTime;

        private void Awake()
        {
            TryGetComponent(out _targeting);
        }

        /// <summary>
        /// 执行普攻动作。
        /// </summary>
        public bool ExecuteAttack()
        {
            return ExecuteAction(basicAttackAction);
        }

        /// <summary>
        /// 执行指定玩家动作。
        /// 该方法不判断玩家状态机是否允许出手，只负责动作资源、冷却和 Hitbox 执行。
        /// </summary>
        public bool ExecuteAction(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null)
            {
                Debug.LogWarning("PlayerCombatDriver 缺少动作配置，无法执行动作。", this);
                return false;
            }

            if (!CanAttack)
            {
                return false;
            }

            if (actionDefinition.HitboxPrefab == null)
            {
                Debug.LogWarning($"PlayerCombatDriver 的动作 {actionDefinition.ActionId} 缺少 Hitbox Prefab。", this);
                return false;
            }

            Vector3 spawnPosition = transform.position
                + transform.forward * actionDefinition.HitboxSpawnDistance
                + Vector3.up * actionDefinition.HitboxSpawnHeight;
            Quaternion spawnRotation = transform.rotation;

            GameObject hitboxInstance = Instantiate(actionDefinition.HitboxPrefab, spawnPosition, spawnRotation);
            ConfigureHitbox(hitboxInstance, actionDefinition);
            Destroy(hitboxInstance, actionDefinition.HitboxLifetime);

            CombatEventsBus.RaiseActionStarted(gameObject, GetCurrentTargetObject(), actionDefinition);
            _nextActionTime = Time.time + actionDefinition.Cooldown;
            return true;
        }

        private void ConfigureHitbox(GameObject hitboxInstance, CombatActionDefinition actionDefinition)
        {
            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);
                hitbox.Configure(
                    actionDefinition.DamageAmount,
                    actionDefinition.KnockbackForce,
                    actionDefinition.CombatTagToApply,
                    actionDefinition.CombatTagDuration);
            }
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
