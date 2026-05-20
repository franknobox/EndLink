namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸鐘舵€佹帴鍙ｃ€?    /// 鐘舵€佹湰韬笉缁ф壙 MonoBehaviour锛屽彧鎺ユ敹鐘舵€佹満缁熶竴鍒嗗彂鐨勭敓鍛藉懆鏈熴€?    /// </summary>
    public interface IAllyState
    {
        /// <summary>褰撳墠鐘舵€佺殑鍞竴鏍囪瘑銆?/summary>
        AllyStateId StateId { get; }

        /// <summary>杩涘叆鐘舵€佹椂璋冪敤涓€娆°€?/summary>
        void Enter();

        /// <summary>褰撳墠鐘舵€佹瘡甯ц皟鐢ㄣ€?/summary>
        void Tick(float deltaTime);

        /// <summary>绂诲紑鐘舵€佹椂璋冪敤涓€娆°€?/summary>
        void Exit();
    }
}

