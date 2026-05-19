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
        [Header("Hitbox")]
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

        [Header("攻击节奏")]
        [Tooltip("攻击冷却时间。冷却未结束时，状态机不会允许进入新的攻击状态。")]
        [SerializeField, Min(0f)]
        private float attackCooldown = 0.45f;

        private float _nextAttackTime;

        /// <summary>
        /// 当前是否已经结束冷却，可以发起下一次攻击。
        /// </summary>
        public bool CanAttack => Time.time >= _nextAttackTime;

        /// <summary>
        /// 设置 Hitbox 预制体。主要用于运行时配置或简单编译测试。
        /// </summary>
        public void SetHitboxPrefab(GameObject prefab)
        {
            hitboxPrefab = prefab;
        }

        /// <summary>
        /// 执行一次攻击表现和攻击判定。
        /// 调用者应先通过 CanAttack 判断冷却；这里仍保留防御性检查，避免外部误调用。
        /// </summary>
        public bool ExecuteAttack()
        {
            if (!CanAttack)
            {
                return false;
            }

            if (hitboxPrefab == null)
            {
                Debug.LogWarning("PlayerCombatDriver 缺少 Hitbox 预制体，无法生成攻击判定。", this);
                return false;
            }

            Vector3 spawnPosition = transform.position
                + transform.forward * spawnDistance
                + Vector3.up * spawnHeight;
            Quaternion spawnRotation = transform.rotation;

            GameObject hitboxInstance = Instantiate(hitboxPrefab, spawnPosition, spawnRotation);

            if (hitboxInstance.TryGetComponent(out HitboxBase hitbox))
            {
                hitbox.Initialize(gameObject);
            }

            Destroy(hitboxInstance, hitboxLifetime);

            _nextAttackTime = Time.time + attackCooldown;
            return true;
        }
    }
}
