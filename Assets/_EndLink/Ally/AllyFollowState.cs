namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸璺熼殢鐘舵€併€?    /// 绗竴鐗堝彧浣滀负鐘舵€佸崰浣嶏紝涓嶅疄闄呯Щ鍔紱鍚庣画鎺?Follow Motor 鎴?NavMesh/CharacterController 璺熼殢閫昏緫銆?    /// </summary>
    public sealed class AllyFollowState : AllyStateBase
    {
        public AllyFollowState(AllyStateContext context) : base(context)
        {
        }

        public override AllyStateId StateId => AllyStateId.Follow;

        public override void Tick(float deltaTime)
        {
            if (Context.FollowTarget == null)
            {
                Context.StateMachine.ChangeState(AllyStateId.Idle);
            }
        }
    }
}

