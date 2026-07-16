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
            PlayerCombatDriver combatDriver,
            PlayerTargeting targeting,
            PlayerComboController comboController,
            PlayerGuardController guardController)
        {
            StateMachine = stateMachine;
            Transform = transform;
            InputReader = inputReader;
            Controller = controller;
            CombatDriver = combatDriver;
            Targeting = targeting;
            ComboController = comboController;
            GuardController = guardController;
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

        /// <summary>玩家目标选择组件。为空时攻击仍可按角色当前朝向正常执行。</summary>
        public PlayerTargeting Targeting { get; }

        /// <summary>玩家普攻连段控制器。未挂载时 Attack 保持单段普攻兼容行为。</summary>
        public PlayerComboController ComboController { get; }

        /// <summary>玩家格挡弹反规则组件。未挂载时不会进入 Guard 状态。</summary>
        public PlayerGuardController GuardController { get; }

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
        /// 攻击期间移动输入倍率。0 表示站桩攻击，1 表示完全保留移动。
        /// </summary>
        public float AttackMoveInputScale => StateMachine.AttackMoveInputScale;

        /// <summary>防御期间保留的移动输入倍率。</summary>
        public float GuardMoveInputScale => StateMachine.GuardMoveInputScale;

        /// <summary>攻击期间朝当前有效目标平滑转向的速度。</summary>
        public float AttackTrackingRotationSharpness => StateMachine.AttackTrackingRotationSharpness;

        /// <summary>
        /// 数据驱动技能状态的基础持续时间；动画事件模式由 ActionEnd 决定退出。
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
        public bool CanStartAttack
        {
            get
            {
                CombatActionDefinition fallbackAction = CombatDriver != null ? CombatDriver.BasicAttackAction : null;
                CombatActionDefinition firstAction = ComboController != null
                    ? ComboController.GetFirstAction(fallbackAction)
                    : fallbackAction;
                return ActionExecutor != null && ActionExecutor.CanExecute(firstAction);
            }
        }

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

        /// <summary>当前是否允许进入防御状态。</summary>
        public bool CanStartGuard => GuardController != null && InputReader.GuardHeld;

        /// <summary>
        /// 当前是否允许开始基础跳跃。
        /// </summary>
        public bool CanStartJump => Controller.CanJump;

        /// <summary>
        /// 尝试消费一次仍在有效期内的攻击缓冲。
        /// 动作暂时不可执行时不会提前清空输入。
        /// </summary>
        public bool TryConsumeBufferedAttack()
        {
            return StateMachine.TryConsumeAttackBuffer(CanStartAttack);
        }

        /// <summary>
        /// 攻击期间让角色继续平滑朝向当前软锁点。
        /// 没有目标或转向速度为 0 时保持当前朝向。
        /// </summary>
        public bool TickAttackTargetFacing(float deltaTime)
        {
            if (Targeting == null
                || !Targeting.HasTarget
                || Targeting.CurrentLockPoint == null
                || AttackTrackingRotationSharpness <= 0f)
            {
                return false;
            }

            Vector3 toTarget = Targeting.CurrentLockPoint.position - Transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= PlayerStateBase.MoveInputDeadZoneSqr)
            {
                return false;
            }

            Controller.FaceDirection(toTarget, false, deltaTime, AttackTrackingRotationSharpness);
            return true;
        }

        /// <summary>
        /// 消费一次闪避输入。
        /// </summary>
        public bool ConsumeDodgePressed()
        {
            return InputReader.ConsumeDodgePressed();
        }

        /// <summary>返回当前有效目标的唯一根节点；存在硬锁时优先返回硬锁目标。</summary>
        public Transform GetCurrentAttackTarget()
        {
            return Targeting != null && Targeting.HasTarget ? Targeting.CurrentTarget : null;
        }

        /// <summary>获取指定普攻动作的状态持续时间。</summary>
        public float GetAttackDuration(CombatActionDefinition action)
        {
            return action != null
                ? Mathf.Max(StateMachine.AttackDuration, action.TotalDuration)
                : StateMachine.AttackDuration;
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
