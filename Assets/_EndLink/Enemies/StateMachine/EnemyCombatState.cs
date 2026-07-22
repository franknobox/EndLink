using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗大状态。
    /// 负责 Combat 生命周期、Home 追击边界和目标丢失处理；
    /// 接近、定位、攻击和恢复会根据 EnemyActor 的战斗定位，
    /// 分别交给近战 EnemyCombatBehavior 或远程 EnemyCombatBehaviorR 推进。
    /// </summary>
    public sealed class EnemyCombatState : EnemyStateBase
    {
        private readonly EnemyCombatBehavior _combatBehavior;
        private readonly EnemyCombatBehaviorR _combatBehaviorR;
        private float _lostTargetElapsed;

        public EnemyCombatState(EnemyStateContext context) : base(context)
        {
            if (UsesRangedBehavior(context))
            {
                _combatBehaviorR = new EnemyCombatBehaviorR(context);
            }
            else
            {
                _combatBehavior = new EnemyCombatBehavior(context);
            }
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Combat;

        /// <summary>当前 Combat 内部行为阶段，供动画桥接和调试读取。</summary>
        public EnemyCombatPhase CurrentPhase => _combatBehaviorR != null
            ? _combatBehaviorR.CurrentPhase
            : _combatBehavior.CurrentPhase;

        /// <summary>重置固定普攻/技能循环计数。</summary>
        public void ResetActionPattern()
        {
            if (_combatBehaviorR != null)
            {
                _combatBehaviorR.ResetActionPattern();
                return;
            }

            _combatBehavior.ResetActionPattern();
        }

        /// <inheritdoc />
        public override void Enter()
        {
            _lostTargetElapsed = 0f;
            if (_combatBehaviorR != null)
            {
                _combatBehaviorR.Enter();
                return;
            }

            _combatBehavior.Enter();
        }

        /// <inheritdoc />
        public override void Exit()
        {
            if (_combatBehaviorR != null)
            {
                _combatBehaviorR.Exit();
                return;
            }

            _combatBehavior.Exit();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (IsBeyondHomeLeash())
            {
                Context.StateMachine.RequestReturn();
                return;
            }

            if (!Context.HasValidTarget)
            {
                CancelCurrentAttempt();
                TickLostTarget(deltaTime);
                return;
            }

            _lostTargetElapsed = 0f;
            if (_combatBehaviorR != null)
            {
                _combatBehaviorR.Tick(deltaTime);
                return;
            }

            _combatBehavior.Tick(deltaTime);
        }

        private void CancelCurrentAttempt()
        {
            if (_combatBehaviorR != null)
            {
                _combatBehaviorR.CancelCurrentAttempt();
                return;
            }

            _combatBehavior.CancelCurrentAttempt();
        }

        private static bool UsesRangedBehavior(EnemyStateContext context)
        {
            EnemyActor actor = context?.Actor;
            return actor != null
                && (actor.CombatRole == EnemyCombatRole.GroundRanged
                    || actor.CombatRole == EnemyCombatRole.FlyingRanged);
        }

        private void TickLostTarget(float deltaTime)
        {
            Context.Motor?.Stop();
            _lostTargetElapsed += Mathf.Max(0f, deltaTime);

            if (_lostTargetElapsed < Context.LostTargetDelay)
            {
                return;
            }

            Context.StateMachine.SetTarget(null);
            Context.StateMachine.RequestReturn();
        }

        private bool IsBeyondHomeLeash()
        {
            if (Context.MaxChaseRadius <= 0f)
            {
                return false;
            }

            Vector3 offset = Context.Transform.position - Context.HomePosition;
            offset.y = 0f;
            return offset.sqrMagnitude > Context.MaxChaseRadius * Context.MaxChaseRadius;
        }
    }
}
