using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 战斗角色 Animator 参数协议。
    /// 这里只定义稳定的通用参数名和 Hash，不定义具体连段、技能或动画状态名称。
    /// </summary>
    public static class CombatAnimatorParams
    {
        public const string MoveSpeed = nameof(MoveSpeed);
        public const string IsMoving = nameof(IsMoving);
        public const string IsGrounded = nameof(IsGrounded);
        public const string StateId = nameof(StateId);
        public const string ActionId = nameof(ActionId);
        public const string ActionType = nameof(ActionType);
        public const string ActionTrigger = nameof(ActionTrigger);
        public const string HitTrigger = nameof(HitTrigger);
        public const string DeadTrigger = nameof(DeadTrigger);
        public const string DodgeTrigger = nameof(DodgeTrigger);

        public static readonly int MoveSpeedHash = Animator.StringToHash(MoveSpeed);
        public static readonly int IsMovingHash = Animator.StringToHash(IsMoving);
        public static readonly int IsGroundedHash = Animator.StringToHash(IsGrounded);
        public static readonly int StateIdHash = Animator.StringToHash(StateId);
        public static readonly int ActionIdHash = Animator.StringToHash(ActionId);
        public static readonly int ActionTypeHash = Animator.StringToHash(ActionType);
        public static readonly int ActionTriggerHash = Animator.StringToHash(ActionTrigger);
        public static readonly int HitTriggerHash = Animator.StringToHash(HitTrigger);
        public static readonly int DeadTriggerHash = Animator.StringToHash(DeadTrigger);
        public static readonly int DodgeTriggerHash = Animator.StringToHash(DodgeTrigger);
    }
}
