using System.Collections.Generic;
using UnityEngine;

namespace EndLink.Enemies
{
    /// <summary>
    /// 敌人战斗大状态。
    /// 第一版不执行具体行为；后续行为树会挂在这里，负责追击、站位、攻击和技能等细节。
    /// </summary>
    public sealed class EnemyCombatState : EnemyStateBase
    {
        private readonly List<Collider> _radiusColliderBuffer = new();
        private Transform _cachedRadiusTarget;
        private float _selfPlanarRadius;
        private float _targetPlanarRadius;

        public EnemyCombatState(EnemyStateContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public override EnemyStateId StateId => EnemyStateId.Combat;

        /// <inheritdoc />
        public override void Enter()
        {
            _selfPlanarRadius = EstimatePlanarRadius(Context.Transform);
            RefreshTargetRadiusIfNeeded(Context.CurrentTarget);

            if (!Context.HasValidTarget)
            {
                Context.StateMachine.ChangeState(EnemyStateId.Idle);
            }
        }

        /// <inheritdoc />
        public override void Exit()
        {
            Context.Motor?.Stop();
            _cachedRadiusTarget = null;
            _targetPlanarRadius = 0f;
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

            float stopDistance = CalculateCollisionAwareStopDistance(target);
            Context.Motor?.MoveTo(target.position, stopDistance, deltaTime);
            Context.Motor?.FaceTarget(target, deltaTime);
        }

        private float CalculateCollisionAwareStopDistance(Transform target)
        {
            RefreshTargetRadiusIfNeeded(target);

            float surfaceGap = Mathf.Max(0f, Context.CombatChaseStopDistance);
            return _selfPlanarRadius + _targetPlanarRadius + surfaceGap;
        }

        private void RefreshTargetRadiusIfNeeded(Transform target)
        {
            if (_cachedRadiusTarget == target)
            {
                return;
            }

            _cachedRadiusTarget = target;
            _targetPlanarRadius = EstimatePlanarRadius(target);
        }

        private float EstimatePlanarRadius(Transform root)
        {
            if (root == null)
            {
                return 0f;
            }

            CharacterController characterController = root.GetComponentInParent<CharacterController>();
            if (characterController != null)
            {
                float scale = Mathf.Max(
                    Mathf.Abs(characterController.transform.lossyScale.x),
                    Mathf.Abs(characterController.transform.lossyScale.z));

                return Mathf.Max(0f, characterController.radius * scale);
            }

            _radiusColliderBuffer.Clear();
            root.GetComponentsInChildren(false, _radiusColliderBuffer);

            float radius = 0f;
            for (int i = 0; i < _radiusColliderBuffer.Count; i++)
            {
                Collider candidate = _radiusColliderBuffer[i];
                if (candidate == null || !candidate.enabled || candidate.isTrigger)
                {
                    continue;
                }

                Bounds bounds = candidate.bounds;
                Vector3 extents = bounds.extents;
                radius = Mathf.Max(radius, extents.x, extents.z);
            }

            return radius;
        }

        private bool IsTargetBeyondLeash(Transform target)
        {
            if (target == null || Context.CombatLeashDistance <= 0f)
            {
                return false;
            }

            Vector3 offset = target.position - Context.Transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude > Context.CombatLeashDistance * Context.CombatLeashDistance;
        }
    }
}
