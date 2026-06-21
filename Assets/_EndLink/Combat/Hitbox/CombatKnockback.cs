using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 可接收战斗命中击退的移动执行器接口。
    /// 该接口只表达攻击带来的总击退位移，不用于敌人正常移动时的碰撞推挤。
    /// </summary>
    public interface ICombatKnockbackReceiver
    {
        /// <summary>
        /// 接收一段已经完成方向和倍率计算的总击退位移。
        /// 具体移动器可以瞬时执行，也可以转换为短时衰减运动。
        /// </summary>
        void ApplyCombatKnockback(Vector3 displacement);
    }

    /// <summary>
    /// 把一次总击退位移拆分为短时间内逐帧衰减的水平运动。
    /// 前段位移更快、后段逐渐收住，并保证累计位移等于输入总量。
    /// </summary>
    public sealed class CombatKnockbackMotion
    {
        private const float MinimumDisplacementSqr = 0.0001f;
        private const float MinimumDuration = 0.01f;

        private Vector3 _remainingDisplacement;
        private float _remainingDuration;

        /// <summary>当前是否还有待执行的击退位移。</summary>
        public bool IsActive => _remainingDuration > 0f
            && _remainingDisplacement.sqrMagnitude > MinimumDisplacementSqr;

        /// <summary>
        /// 添加一次总击退位移并刷新衰减时间。
        /// 连续受击会叠加尚未完成的位移，再从新的持续时间开始衰减。
        /// </summary>
        public void AddDisplacement(Vector3 displacement, float duration)
        {
            displacement.y = 0f;
            if (displacement.sqrMagnitude <= MinimumDisplacementSqr)
            {
                return;
            }

            _remainingDisplacement += displacement;
            if (_remainingDisplacement.sqrMagnitude <= MinimumDisplacementSqr)
            {
                Clear();
                return;
            }

            _remainingDuration = Mathf.Max(MinimumDuration, duration);
        }

        /// <summary>计算并消费当前帧应执行的水平位移。</summary>
        public Vector3 Tick(float deltaTime)
        {
            if (!IsActive || deltaTime <= 0f)
            {
                return Vector3.zero;
            }

            if (deltaTime >= _remainingDuration)
            {
                Vector3 finalDisplacement = _remainingDisplacement;
                Clear();
                return finalDisplacement;
            }

            float normalizedStep = deltaTime / _remainingDuration;
            float remainingRatio = 1f - normalizedStep;
            float displacementRatio = 1f - remainingRatio * remainingRatio;
            Vector3 frameDisplacement = _remainingDisplacement * displacementRatio;

            _remainingDisplacement -= frameDisplacement;
            _remainingDuration -= deltaTime;
            return frameDisplacement;
        }

        /// <summary>清空尚未完成的击退运动。</summary>
        public void Clear()
        {
            _remainingDisplacement = Vector3.zero;
            _remainingDuration = 0f;
        }
    }

    /// <summary>
    /// 战斗击退的统一计算与分发入口。
    /// </summary>
    public static class CombatKnockback
    {
        /// <summary>玩家和基础地面敌人的默认击退衰减时间。</summary>
        public const float DefaultMotionDuration = 0.12f;

        private const float MinimumDisplacementSqr = 0.0001f;

        /// <summary>
        /// 根据命中方向、动作基础击退距离和受击者倍率，尝试提交总击退位移。
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
