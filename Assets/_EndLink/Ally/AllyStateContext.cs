using UnityEngine;

namespace EndLink.Ally
{
    /// <summary>
    /// 闃熷弸鐘舵€佷笂涓嬫枃銆?    /// 缁熶竴淇濆瓨鐘舵€佽繍琛屾墍闇€鐨勫閮ㄤ緷璧栵紝閬垮厤姣忎釜鐘舵€佸弽澶?GetComponent銆?    /// </summary>
    public sealed class AllyStateContext
    {
        public AllyStateContext(
            AllyStateMachine stateMachine,
            Transform transform,
            AllyCombatDriver combatDriver)
        {
            StateMachine = stateMachine;
            Transform = transform;
            CombatDriver = combatDriver;
        }

        /// <summary>鎵€灞炵姸鎬佹満銆?/summary>
        public AllyStateMachine StateMachine { get; }

        /// <summary>闃熷弸鏍硅妭鐐?Transform銆?/summary>
        public Transform Transform { get; }

        /// <summary>闃熷弸鎴樻枟鎵ц鍣紝鍙礋璐ｆ墽琛屽姩浣滃拰鐢熸垚 Hitbox銆?/summary>
        public AllyCombatDriver CombatDriver { get; }

        /// <summary>褰撳墠璺熼殢鐩爣銆傜涓€鐗堝彧鎸佹湁寮曠敤锛屼笅涓€姝ユ帴璺熼殢绉诲姩鏃朵娇鐢ㄣ€?/summary>
        public Transform FollowTarget => StateMachine.FollowTarget;

        /// <summary>褰撳墠鍔╂垬鐩爣銆?/summary>
        public Transform CurrentAssistTarget => StateMachine.CurrentAssistTarget;

        /// <summary>鍔╂垬鐘舵€佹寔缁椂闂淬€?/summary>
        public float AssistDuration => StateMachine.CurrentAssistDuration;

        /// <summary>鍙楀嚮鐘舵€佹寔缁椂闂淬€?/summary>
        public float HitDuration => StateMachine.HitDuration;
    }
}

