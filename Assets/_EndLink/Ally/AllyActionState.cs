using EndLink.Combat;

namespace EndLink.Ally
{
    /// <summary>
    /// 队友通用动作状态。
    /// 用于承载主动技能、后续连携技或其他指令动作的执行窗口。
    /// </summary>
    public sealed class AllyActionState : AllyStateBase
    {
        private float _elapsedTime;
        private bool _executed;

        public AllyActionState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Action;

        public override void Enter()
        {
            _elapsedTime = 0f;
            _executed = false;

            CombatActionDefinition action = Context.CurrentAction;
            if (action == null)
            {
                AllyDebugLog.Raise(
                    Context.Transform.gameObject,
                    AllyDebugCategory.State,
                    "action enter failed: missing current action");
                return;
            }

            _executed = Context.ActionExecutor.TryExecute(action, Context.CurrentActionTarget);
            AllyDebugLog.Raise(
                Context.Transform.gameObject,
                AllyDebugCategory.Combat,
                $"action state execute result={_executed}, action={action.ActionId}, target={GetTransformName(Context.CurrentActionTarget)}");
        }

        public override void Tick(float deltaTime)
        {
            _elapsedTime += deltaTime;

            if (_executed && _elapsedTime < Context.ActionDuration)
            {
                return;
            }

            Context.StateMachine.CompleteAction();
        }

        private static string GetTransformName(UnityEngine.Object target)
        {
            return target != null ? target.name : "None";
        }
    }
}
