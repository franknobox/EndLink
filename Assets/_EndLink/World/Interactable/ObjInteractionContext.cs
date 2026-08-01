using EndLink.Combat;
using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 一次武器环境交互的运行时上下文。
    /// 只携带功能执行需要的通用信息，不让世界功能依赖具体 Combat Action 或 Hitbox 类型。
    /// </summary>
    public readonly struct ObjInteractionContext
    {
        public ObjInteractionContext(
            GameObject interactor,
            PlayerWeaponForm weaponForm,
            Collider hitCollider,
            Vector3 hitPoint,
            Vector3 hitDirection,
            float impact)
        {
            Interactor = interactor;
            WeaponForm = weaponForm;
            HitCollider = hitCollider;
            HitPoint = hitPoint;
            HitDirection = hitDirection.sqrMagnitude > 0.0001f
                ? hitDirection.normalized
                : Vector3.forward;
            Impact = Mathf.Max(0f, impact);
        }

        /// <summary>发起交互的角色对象，当前通常为玩家根物体。</summary>
        public GameObject Interactor { get; }

        /// <summary>命中发生时玩家正在使用的 A、B、C 武器形态。</summary>
        public PlayerWeaponForm WeaponForm { get; }

        /// <summary>本次被命中的交互 Collider。</summary>
        public Collider HitCollider { get; }

        /// <summary>用于特效、音效和空间反馈的命中点。</summary>
        public Vector3 HitPoint { get; }

        /// <summary>从攻击来源指向交互物的水平命中方向。</summary>
        public Vector3 HitDirection { get; }

        /// <summary>可选冲击强度，当前由 Hitbox 的击退参数传入，重物等功能可按需使用。</summary>
        public float Impact { get; }
    }
}
