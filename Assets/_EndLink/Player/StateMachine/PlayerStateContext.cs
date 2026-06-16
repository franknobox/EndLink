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
        private CharacterHealth _health;

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

        /// <summary>玩家战斗动作的统一执行接口。</summary>
        public ICombatActionExecutor ActionExecutor => CombatDriver;

        /// <summary>
        /// 玩家通用生命组件。用于闪避临时免伤等和生命系统有关的轻量接线。
        /// </summary>
        public CharacterHealth Health
        {
            get
            {
                if (_health == null)
                {
                    _health = Transform.GetComponent<CharacterHealth>();
                }

                return _health;
            }
        }

        /// <summary>
        /// 攻击状态的基础持续时间。
        /// 胶囊白模阶段先用时间驱动，后续可改为动画事件驱动。
        /// </summary>
        public float AttackDuration
        {
            get
            {
                CombatActionDefinition basicAttack = CombatDriver != null ? CombatDriver.BasicAttackAction : null;
                return basicAttack != null
                    ? Mathf.Max(StateMachine.AttackDuration, basicAttack.TotalDuration)
                    : StateMachine.AttackDuration;
            }
        }

        /// <summary>
        /// 攻击期间移动输入倍率。0 表示站桩攻击，1 表示完全保留移动。
        /// </summary>
        public float AttackMoveInputScale => StateMachine.AttackMoveInputScale;

        /// <summary>
        /// 通用技能状态的基础持续时间。
        /// 白模阶段先用固定时间表示一次技能施放窗口，后续可由动画事件或技能配置驱动。
        /// </summary>
        public float SkillDuration => StateMachine.SkillDuration;

        /// <summary>
        /// 技能期间移动输入倍率。0 表示站桩施法，1 表示完全保留移动。
        /// </summary>
        public float SkillMoveInputScale => StateMachine.SkillMoveInputScale;

        /// <summary>
        /// 闪避状态持续时间。
        /// </summary>
        public float DodgeDuration => StateMachine.DodgeDuration;

        /// <summary>
        /// 一次闪避期望移动距离。
        /// </summary>
        public float DodgeDistance => StateMachine.DodgeDistance;

        /// <summary>
        /// 闪避开始后的临时免伤窗口。
        /// </summary>
        public float DodgeInvincibleDuration => StateMachine.DodgeInvincibleDuration;

        /// <summary>
        /// 受击状态的基础持续时间。
        /// 白模阶段先用固定硬直时间，后续可根据攻击强度、受击动画或韧性系统调整。
        /// </summary>
        public float HitDuration => StateMachine.HitDuration;

        /// <summary>
        /// 受击期间移动输入倍率。0 表示完全失控，1 表示保留完整移动输入。
        /// </summary>
        public float HitMoveInputScale => StateMachine.HitMoveInputScale;

        /// <summary>
        /// 当前移动输入是否超过死区。
        /// </summary>
        public bool HasMoveInput => InputReader.MoveInput.sqrMagnitude > PlayerStateBase.MoveInputDeadZoneSqr;

        /// <summary>
        /// 当前是否允许开始一次攻击。
        /// 这里检查普攻动作自身的基础执行条件；硬直、受击和禁用输入由状态机外层状态约束处理。
        /// </summary>
        public bool CanStartAttack => ActionExecutor != null
            && ActionExecutor.CanExecute(CombatDriver.BasicAttackAction);

        /// <summary>
        /// 当前是否允许开始一次技能或连携动作。
        /// 这里检查当前动作资源与冷却；资源、禁用输入和受击硬直可以继续在状态机或动作系统中扩展。
        /// </summary>
        public bool CanStartSkill => ActionExecutor != null
            && StateMachine.CurrentAction != null
            && ActionExecutor.CanExecute(StateMachine.CurrentAction);

        /// <summary>
        /// 执行主控主动技能动作。
        /// 状态机负责决定能否进入 Skill 状态，战斗驱动只负责实际表现和判定。
        /// </summary>
        public bool ExecuteCurrentAction()
        {
            return ActionExecutor != null
                && ActionExecutor.TryExecute(StateMachine.CurrentAction, StateMachine.CurrentActionTarget);
        }

        /// <summary>
        /// 当前是否允许开始闪避。
        /// </summary>
        public bool CanStartDodge => StateMachine.CanStartDodge;

        /// <summary>
        /// 当前是否允许开始基础跳跃。
        /// </summary>
        public bool CanStartJump => Controller.CanJump;

        /// <summary>
        /// 消费一次攻击输入。
        /// </summary>
        public bool ConsumeAttackPressed()
        {
            return InputReader.ConsumeAttackPressed();
        }

        /// <summary>
        /// 消费一次闪避输入。
        /// </summary>
        public bool ConsumeDodgePressed()
        {
            return InputReader.ConsumeDodgePressed();
        }

        /// <summary>
        /// 消费一次跳跃输入。
        /// </summary>
        public bool ConsumeJumpPressed()
        {
            return InputReader.ConsumeJumpPressed();
        }

        /// <summary>
        /// 请求控制器执行一次基础跳跃。
        /// </summary>
        public bool TryJump()
        {
            return Controller.TryJump();
        }

        /// <summary>
        /// 消费一次技能请求。
        /// 当前由 PartyCombatRouter 调用 PlayerStateMachine.RequestSkill 写入，状态机在 Tick 中消费。
        /// </summary>
        public bool ConsumeSkillRequested()
        {
            return StateMachine.ConsumeSkillRequest();
        }
    }
}
