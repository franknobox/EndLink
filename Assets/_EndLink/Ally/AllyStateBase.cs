namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸鐘舵€佸熀绫汇€?    /// 鍙繚瀛樺叡浜笂涓嬫枃鍜岄粯璁ょ敓鍛藉懆鏈燂紝鍏蜂綋鐘舵€佸彧瀹炵幇鑷繁鍏冲績鐨勯€昏緫銆?    /// </summary>
    public abstract class AllyStateBase : IAllyState
    {
        protected AllyStateBase(AllyStateContext context)
        {
            Context = context;
        }

        public abstract AllyStateId StateId { get; }

        protected AllyStateContext Context { get; }

        public virtual void Enter()
        {
        }

        public abstract void Tick(float deltaTime);

        public virtual void Exit()
        {
        }
    }
}

