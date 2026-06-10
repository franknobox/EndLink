using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家通用技能状态。
    /// 负责表示“角色正在释放某个动作”的状态窗口，并在进入状态时调用 PlayerCombatDriver 执行当前动作。
    /// 具体 Hitbox、伤害、标签和冷却仍由 CombatActionDefinition 与 PlayerCombatDriver 负责。
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
            _executed = Context.ExecuteCurrentAction();
        }

        public override void Tick(float deltaTime)
        {
            Context.ConsumeJumpPressed();

            if (!_executed)
            {
                Context.StateMachine.CompleteAction();
                return;
            }

            _elapsedTime += deltaTime;

            Vector2 skillMoveInput = Context.InputReader.MoveInput * Context.SkillMoveInputScale;
            Context.Controller.TickMovement(skillMoveInput, deltaTime);

            if (_elapsedTime < Context.SkillDuration)
            {
                return;
            }

            Context.StateMachine.CompleteAction();
        }
    }
}
