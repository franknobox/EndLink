using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家闪避状态。
    /// 第一版用于白模阶段验证近战敌人规避手感：有移动输入时按输入方向闪避；
    /// 没有移动输入时默认向角色正后方后撤。
    /// </summary>
    public sealed class PlayerDodgeState : PlayerStateBase
    {
        private Vector3 _dodgeDirection;
        private float _elapsedTime;
        private bool _faceDodgeDirection;

        public PlayerDodgeState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Dodge;

        public override void Enter()
        {
            _elapsedTime = 0f;
            _dodgeDirection = ResolveDodgeDirection(out _faceDodgeDirection);
            Context.StateMachine.MarkDodgeStarted();

            if (Context.DodgeInvincibleDuration > 0f)
            {
                Context.Health?.SetTemporaryInvincible(Context.DodgeInvincibleDuration);
            }
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;

            float normalizedTime = Context.DodgeDuration > 0f
                ? Mathf.Clamp01(_elapsedTime / Context.DodgeDuration)
                : 1f;

            float baseSpeed = Context.DodgeDuration > 0f
                ? Context.DodgeDistance / Context.DodgeDuration
                : 0f;

            // 前段更快、后段收住一点；平均倍率约为 1，便于保持总闪避距离接近配置值。
            float speedMultiplier = Mathf.Lerp(1.4f, 0.6f, normalizedTime);
            Context.Controller.TickDodgeMovement(
                _dodgeDirection,
                baseSpeed * speedMultiplier,
                deltaTime,
                _faceDodgeDirection);

            if (_elapsedTime < Context.DodgeDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
        }

        private Vector3 ResolveDodgeDirection(out bool faceDodgeDirection)
        {
            if (Context.HasMoveInput)
            {
                Vector3 inputDirection = Context.Controller.GetDesiredMoveDirection(Context.InputReader.MoveInput);
                if (inputDirection.sqrMagnitude > MoveInputDeadZoneSqr)
                {
                    faceDodgeDirection = true;
                    return inputDirection.normalized;
                }
            }

            faceDodgeDirection = false;
            Vector3 backward = -Context.Transform.forward;
            backward.y = 0f;

            if (backward.sqrMagnitude <= MoveInputDeadZoneSqr)
            {
                return Vector3.back;
            }

            return backward.normalized;
        }
    }
}
