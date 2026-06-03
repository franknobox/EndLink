using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家通用技能状态。
    /// 当前只负责表示“角色正在释放某个技能”的状态窗口，具体技能表现、判定和资源消耗
    /// 后续应交给 SkillDriver 或技能配置系统处理，避免状态类膨胀。
    /// </summary>
    public sealed class PlayerSkillState : PlayerStateBase
    {
        private float _elapsedTime;
        private bool _executed;

        public PlayerSkillState(PlayerStateContext context) : base(context)
        {
        }

        public override PlayerStateId StateId => PlayerStateId.Skill;

        public override void Enter()
        {
            _elapsedTime = 0f;
            _executed = Context.ExecuteSkillAction();
        }

        public override void Tick(float deltaTime)
        {
            if (!_executed)
            {
                Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
                return;
            }

            _elapsedTime += deltaTime;

            Vector2 skillMoveInput = Context.InputReader.MoveInput * Context.SkillMoveInputScale;
            Context.Controller.TickMovement(skillMoveInput, deltaTime);

            if (_elapsedTime < Context.SkillDuration)
            {
                return;
            }

            Context.StateMachine.ChangeState(Context.HasMoveInput ? PlayerStateId.Move : PlayerStateId.Idle);
        }
    }
}
