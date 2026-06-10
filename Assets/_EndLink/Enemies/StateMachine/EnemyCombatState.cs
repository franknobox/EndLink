using EndLink.Combat;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗大状态。
    /// 当前负责基础追击、停止距离和面向目标；之后可在这里接入行为树，处理站位、攻击和技能细节。
    /// </summary>
    public sealed class EnemyCombatState : EnemyStateBase
    {
        private float _selfPlanarRadius;

        public EnemyCombatState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Combat;

        /// <inheritdoc />
        public override void Enter()
        {
            _selfPlanarRadius = EstimateSelfPlanarRadius();

            if (!Context.HasValidTarget)
            {
                Context.StateMachine.ChangeState(EnemyStateId.Idle);
            }
        }

        /// <inheritdoc />
        public override void Exit()
        {
            Context.Motor?.Stop();
        }

        /// <inheritdoc />
        public override void Tick(float deltaTime)
        {
            if (!Context.HasValidTarget)
            {
                Context.Motor?.Stop();
                Context.StateMachine.ChangeState(EnemyStateId.Idle);
                return;
            }

            Transform target = Context.CurrentTarget;
            if (IsTargetBeyondLeash(target))
            {
                Context.Motor?.Stop();
                Context.StateMachine.SetTarget(null);
                Context.StateMachine.ChangeState(EnemyStateId.Idle);
                return;
            }

            Vector3 approachPoint = CombatTargetUtility.GetClosestPoint(
                target,
                Context.Transform.position);
            float stopDistance = _selfPlanarRadius + Mathf.Max(0f, Context.CombatChaseStopDistance);
            Context.Motor?.MoveTo(approachPoint, stopDistance, deltaTime);

            Context.Motor?.FaceTarget(target, deltaTime);
        }

        private float EstimateSelfPlanarRadius()
        {
            CharacterController characterController = Context.Transform.GetComponent<CharacterController>();
            if (characterController != null)
            {
                float scale = Mathf.Max(
                    Mathf.Abs(characterController.transform.lossyScale.x),
                    Mathf.Abs(characterController.transform.lossyScale.z));

                return Mathf.Max(0f, characterController.radius * scale);
            }

            return 0f;
        }

        private bool IsTargetBeyondLeash(Transform target)
        {
            if (target == null || Context.CombatLeashDistance <= 0f)
            {
                return false;
            }

            return CombatTargetUtility.GetSurfaceDistance(
                target,
                Context.Transform.position) > Context.CombatLeashDistance;
        }
    }
}
