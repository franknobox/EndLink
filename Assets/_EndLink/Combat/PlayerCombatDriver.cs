using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 玩家战斗驱动器。
    /// 不读取输入、不决定状态是否能切换，只负责执行攻击表现和攻击判定。
    /// 当前胶囊白模阶段的执行内容是：在角色正前方生成 Hitbox，并在短时间后销毁。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatDriver : MonoBehaviour
    {
        [Header("动作配置")]
        [Tooltip("玩家普攻配置。配置后会优先使用该资产里的伤害、冷却、Hitbox、生成位置和标签参数。")]
        [SerializeField]
        private CombatActionDefinition basicAttackDefinition;

        [Header("兼容默认 Hitbox")]
        [Tooltip("攻击时生成的 Hitbox 预制体。通常拖入带有碰撞体/命中检测脚本的 Hitbox prefab。")]
        [SerializeField]
        private GameObject hitboxPrefab;

        [Tooltip("Hitbox 生成在角色正前方的距离。角色本地 Z 轴正方向视为前方。")]
        [SerializeField, Min(0f)]
        private float spawnDistance = 1f;

        [Tooltip("Hitbox 生成高度偏移。用于把近战波从脚底抬到角色腰部或胸口高度。")]
        [SerializeField]
        private float spawnHeight = 1f;

        [Tooltip("Hitbox 自动销毁时间。近战波胶囊阶段建议保持很短，例如 0.2 秒。")]
        [SerializeField, Min(0.01f)]
        private float hitboxLifetime = 0.2f;

        [Header("兼容默认攻击节奏")]
        [Tooltip("攻击冷却时间。冷却未结束时，状态机不会允许进入新的攻击状态。")]
        [SerializeField, Min(0f)]
        private float attackCooldown = 0.45f;

        private PlayerTargeting _targeting;
        private float _nextAttackTime;

        /// <summary>
        /// 当前是否已经结束冷却，可以发起下一次攻击。
        /// </summary>
        public bool CanAttack => Time.time >= _nextAttackTime;

        /// <summary>
        /// 当前使用的普攻配置资产。为空时使用本组件上的兼容默认字段。
        /// </summary>
        public CombatActionDefinition BasicAttackDefinition => basicAttackDefinition;

        /// <summary>
        /// 当前是否已经配置普攻数据资产。
        /// </summary>
        public bool HasBasicAttackDefinition => basicAttackDefinition != null;

        /// <summary>
        /// 当前普攻冷却时间。优先来自 CombatActionDefinition，未配置时回退到组件字段。
        /// </summary>
        public float CurrentAttackCooldown => GetAttackCooldown();

        /// <summary>
        /// 设置普攻配置资产。主要用于运行时配置、调试工具或简单编译测试。
        /// </summary>
        public void SetBasicAttackDefinition(CombatActionDefinition definition)
        {
            basicAttackDefinition = definition;
        }

        /// <summary>
        /// 设置 Hitbox 预制体。主要用于运行时配置或简单编译测试。
        /// </summary>
        public void SetHitboxPrefab(GameObject prefab)
        {
            hitboxPrefab = prefab;
        }

        private void Awake()
        {
            TryGetComponent(out _targeting);
        }

        /// <summary>
        /// 执行一次攻击表现和攻击判定。
        /// 调用者应先通过 CanAttack 判断冷却；这里仍保留防御性检查，避免外部误调用。
        /// </summary>
        public bool ExecuteAttack()
        {
            CombatActionDefinition actionDefinition = basicAttackDefinition;

            if (!CanAttack)
            {
                return false;
            }

            GameObject selectedHitboxPrefab = GetHitboxPrefab(actionDefinition);

            if (selectedHitboxPrefab == null)
            {
                Debug.LogWarning("PlayerCombatDriver 缺少 Hitbox 预制体，无法生成攻击判定。", this);
                return false;
            }

            Vector3 spawnPosition = transform.position
                + transform.forward * GetHitboxSpawnDistance(actionDefinition)
                + Vector3.up * GetHitboxSpawnHeight(actionDefinition);
            Quaternion spawnRotation = transform.rotation;

            GameObject hitboxInstance = Instantiate(selectedHitboxPrefab, spawnPosition, spawnRotation);

            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);

                if (actionDefinition != null)
                {
                    hitbox.Configure(
                        actionDefinition.DamageAmount,
                        actionDefinition.KnockbackForce,
                        actionDefinition.CombatTagToApply,
                        actionDefinition.CombatTagDuration);
                }
            }

            Destroy(hitboxInstance, GetHitboxLifetime(actionDefinition));
            CombatEventsBus.RaiseActionStarted(gameObject, GetCurrentTargetObject(), actionDefinition);

            _nextAttackTime = Time.time + GetAttackCooldown();
            return true;
        }

        private GameObject GetCurrentTargetObject()
        {
            if (_targeting == null || !_targeting.HasTarget)
            {
                return null;
            }

            return _targeting.CurrentTarget != null ? _targeting.CurrentTarget.gameObject : null;
        }

        private float GetAttackCooldown()
        {
            return basicAttackDefinition != null ? basicAttackDefinition.Cooldown : attackCooldown;
        }

        private GameObject GetHitboxPrefab(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition != null && actionDefinition.HitboxPrefab != null)
            {
                return actionDefinition.HitboxPrefab;
            }

            return hitboxPrefab;
        }

        private float GetHitboxSpawnDistance(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null ? actionDefinition.HitboxSpawnDistance : spawnDistance;
        }

        private float GetHitboxSpawnHeight(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null ? actionDefinition.HitboxSpawnHeight : spawnHeight;
        }

        private float GetHitboxLifetime(CombatActionDefinition actionDefinition)
        {
            return actionDefinition != null ? actionDefinition.HitboxLifetime : hitboxLifetime;
        }
    }
}
