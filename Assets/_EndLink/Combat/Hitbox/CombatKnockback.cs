using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 可接收战斗命中击退的移动执行器接口。
    /// 该接口只表达攻击带来的瞬时位移，不用于敌人正常移动时的碰撞推挤。
    /// </summary>
    public interface ICombatKnockbackReceiver
    {
        /// <summary>
        /// 施加一段已经完成方向和倍率计算的瞬时击退位移。
        /// 第一版只处理 XZ 平面，不包含击飞或持续受力。
        /// </summary>
        void ApplyCombatKnockback(Vector3 displacement);
    }

    /// <summary>
    /// 战斗击退的统一计算与分发入口。
    /// </summary>
    public static class CombatKnockback
    {
        private const float MinimumDisplacementSqr = 0.0001f;

        /// <summary>
        /// 根据命中方向、动作基础击退距离和受击者倍率，尝试施加瞬时击退。
        /// 未配置 CharacterStats 时按 1 倍处理，确保普通目标仍能使用基础击退。
        /// 目标会先通过 CombatTarget 归一到 RootTransform，再向父级查找数值和击退接收器，
        /// 避免 Collider、生命、移动组件拆在不同节点时伤害生效但击退丢失。
        /// </summary>
        public static bool TryApply(
            GameObject target,
            Vector3 hitDirection,
            float baseKnockbackDistance)
        {
            if (target == null || baseKnockbackDistance <= 0f)
            {
                return false;
            }

            hitDirection.y = 0f;
            if (hitDirection.sqrMagnitude <= MinimumDisplacementSqr)
            {
                return false;
            }

            Transform targetRoot = ResolveTargetRoot(target);
            CharacterStats stats = targetRoot.GetComponentInParent<CharacterStats>();
            float multiplier = stats != null ? stats.KnockbackTakenMultiplier : 1f;
            float finalDistance = Mathf.Max(0f, baseKnockbackDistance) * multiplier;
            if (finalDistance <= 0f)
            {
                return false;
            }

            ICombatKnockbackReceiver receiver = targetRoot.GetComponentInParent<ICombatKnockbackReceiver>();
            if (receiver == null)
            {
                return false;
            }

            receiver.ApplyCombatKnockback(hitDirection.normalized * finalDistance);
            return true;
        }

        private static Transform ResolveTargetRoot(GameObject target)
        {
            if (CombatTargetUtility.TryResolve(target, out ICombatTarget combatTarget)
                && combatTarget.RootTransform != null)
            {
                return combatTarget.RootTransform;
            }

            return target.transform;
        }
    }
}
