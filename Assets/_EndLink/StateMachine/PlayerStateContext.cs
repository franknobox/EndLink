using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家状态上下文。
    /// 统一保存状态运行所需的外部依赖，避免每个状态内部反复 GetComponent。
    /// </summary>
    public sealed class PlayerStateContext
    {
        public PlayerStateContext(
            PlayerStateMachine stateMachine,
            Transform transform,
            PlayerInputReader inputReader,
            PlayerController controller,
            PlayerCombatDriver combatDriver)
        {
            StateMachine = stateMachine;
            Transform = transform;
            InputReader = inputReader;
            Controller = controller;
            CombatDriver = combatDriver;
        }

        /// <summary>
        /// 所属状态机，用于状态内部发起切换。
        /// </summary>
        public PlayerStateMachine StateMachine { get; }

        /// <summary>
        /// 玩家根节点 Transform。
        /// </summary>
        public Transform Transform { get; }

        /// <summary>
        /// 玩家输入读取器，只用于读取缓存后的输入值。
        /// </summary>
        public PlayerInputReader InputReader { get; }

        /// <summary>
        /// 玩家移动控制器，只提供实际移动能力。
        /// </summary>
        public PlayerController Controller { get; }

        /// <summary>
        /// 玩家战斗驱动器。状态机决定是否进入攻击，战斗驱动器只负责执行攻击表现和判定。
        /// </summary>
        public PlayerCombatDriver CombatDriver { get; }

        /// <summary>
        /// 攻击状态的基础持续时间。
        /// 胶囊白模阶段先用时间驱动，后续可改为动画事件驱动。
        /// </summary>
        public float AttackDuration => StateMachine.AttackDuration;

        /// <summary>
        /// 攻击期间移动输入倍率。0 表示站桩攻击，1 表示完全保留移动。
        /// </summary>
        public float AttackMoveInputScale => StateMachine.AttackMoveInputScale;

        /// <summary>
        /// 当前移动输入是否超过死区。
        /// </summary>
        public bool HasMoveInput => InputReader.MoveInput.sqrMagnitude > PlayerStateBase.MoveInputDeadZoneSqr;

        /// <summary>
        /// 当前是否允许开始一次攻击。
        /// 这里先只检查攻击冷却；后续可以继续加入硬直、受击、禁用输入等条件。
        /// </summary>
        public bool CanStartAttack => CombatDriver.CanAttack;

        /// <summary>
        /// 消费一次攻击输入。
        /// </summary>
        public bool ConsumeAttackPressed()
        {
            return InputReader.ConsumeAttackPressed();
        }
    }
}
