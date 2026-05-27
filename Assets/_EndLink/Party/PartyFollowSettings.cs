using System;
using EndLink.Ally;
using UnityEngine;

namespace EndLink.Party
{
    /// <summary>
    /// 固定小队共享的队友跟随参数。
    /// PartyManager 负责统一配置并分发给每个 AllyFollowMotor，AllyFollowMotor 只负责实际移动执行。
    /// </summary>
    [Serializable]
    public sealed class PartyFollowSettings
    {
        [Header("距离")]
        [Tooltip("距离目标队形点小于该值时停止移动，用于避免到点后微小抖动。")]
        [SerializeField, Min(0f)]
        private float stopDistance = 0.15f;

        [Tooltip("队形点软半径。进入这个范围就算到位，不会强制踩死一个精确点。")]
        [SerializeField, Min(0f)]
        private float followSlotSoftness = 0.75f;

        [Tooltip("跟随死区半径。队友站定后，主控仍在这个半径内移动时不会触发重新跟随；走出后才更新队形点。设置为 0 表示关闭。")]
        [SerializeField, Min(0f)]
        private float followDeadZoneRadius = 5f;

        [Header("移动")]
        [Tooltip("队友朝队形点移动的基础最大速度，单位是米/秒。")]
        [SerializeField, Min(0f)]
        private float moveSpeed = 4f;

        [Tooltip("主控正在冲刺时，队友跟随移动速度的倍率。只影响 Follow 状态下的追随移动。")]
        [SerializeField, Min(1f)]
        private float sprintSyncSpeedMultiplier = 1.5f;

        [Tooltip("接近目标点时的速度阻尼时间。值越小越跟手，值越大越柔和。")]
        [SerializeField, Min(0.001f)]
        private float arrivalSmoothTime = 0.12f;

        [Tooltip("距离队形点超过该值时进入追赶模式。设置为 0 表示不启用追赶加速。")]
        [SerializeField, Min(0f)]
        private float catchUpDistance = 5f;

        [Tooltip("追赶模式下的速度倍率。只有距离超过 catchUpDistance 时生效。")]
        [SerializeField, Min(1f)]
        private float catchUpSpeedMultiplier = 1.75f;

        [Tooltip("距离队形点超过该值时直接瞬移归位。设置为 0 表示不启用瞬移归位。")]
        [SerializeField, Min(0f)]
        private float teleportDistance = 15f;

        [Header("转向")]
        [Tooltip("队友转向速度，单位是角度/秒。移动时优先面向移动方向。")]
        [SerializeField, Min(0f)]
        private float rotationSpeed = 540f;

        [Tooltip("停止跟随后如何处理朝向。Keep Current Rotation 可以避免队友站定后一直盯着主控。")]
        [SerializeField]
        private AllyIdleFacingMode idleFacingMode = AllyIdleFacingMode.FaceFollowTargetForward;

        [Header("简易避让")]
        [Tooltip("是否启用第一版角色间简易避让。只做局部排斥，不做 NavMesh 寻路。")]
        [SerializeField]
        private bool avoidanceEnabled = true;

        [Tooltip("队友离跟随目标小于该半径时，会被轻微推离主控。")]
        [SerializeField, Min(0f)]
        private float followTargetAvoidRadius = 1.15f;

        [Tooltip("队友离其他队友小于该半径时，会被轻微推开。需要配置 avoidanceLayerMask 才能检测到其他队友。")]
        [SerializeField, Min(0f)]
        private float allyAvoidRadius = 1f;

        [Tooltip("避让修正强度。值越大，队友越倾向于绕开主控和其他队友。")]
        [SerializeField, Min(0f)]
        private float avoidanceStrength = 1.25f;

        [Tooltip("参与队友间避让检测的 Layer。建议给队友角色设置单独 Layer 后在这里勾选。主控避让不依赖该 Layer。")]
        [SerializeField]
        private LayerMask avoidanceLayerMask;

        public float StopDistance => stopDistance;
        public float FollowSlotSoftness => followSlotSoftness;
        public float FollowDeadZoneRadius => followDeadZoneRadius;
        public float MoveSpeed => moveSpeed;
        public float SprintSyncSpeedMultiplier => sprintSyncSpeedMultiplier;
        public float ArrivalSmoothTime => arrivalSmoothTime;
        public float CatchUpDistance => catchUpDistance;
        public float CatchUpSpeedMultiplier => catchUpSpeedMultiplier;
        public float TeleportDistance => teleportDistance;
        public float RotationSpeed => rotationSpeed;
        public AllyIdleFacingMode IdleFacingMode => idleFacingMode;
        public bool AvoidanceEnabled => avoidanceEnabled;
        public float FollowTargetAvoidRadius => followTargetAvoidRadius;
        public float AllyAvoidRadius => allyAvoidRadius;
        public float AvoidanceStrength => avoidanceStrength;
        public LayerMask AvoidanceLayerMask => avoidanceLayerMask;

        /// <summary>
        /// 限制参数范围，避免 Inspector 输入非法值后影响移动计算。
        /// </summary>
        public void Normalize()
        {
            stopDistance = Mathf.Max(0f, stopDistance);
            followSlotSoftness = Mathf.Max(0f, followSlotSoftness);
            followDeadZoneRadius = Mathf.Max(0f, followDeadZoneRadius);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            sprintSyncSpeedMultiplier = Mathf.Max(1f, sprintSyncSpeedMultiplier);
            arrivalSmoothTime = Mathf.Max(0.001f, arrivalSmoothTime);
            catchUpDistance = Mathf.Max(0f, catchUpDistance);
            catchUpSpeedMultiplier = Mathf.Max(1f, catchUpSpeedMultiplier);
            teleportDistance = Mathf.Max(0f, teleportDistance);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            followTargetAvoidRadius = Mathf.Max(0f, followTargetAvoidRadius);
            allyAvoidRadius = Mathf.Max(0f, allyAvoidRadius);
            avoidanceStrength = Mathf.Max(0f, avoidanceStrength);
        }
    }
}
