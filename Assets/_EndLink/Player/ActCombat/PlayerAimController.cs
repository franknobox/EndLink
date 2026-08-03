using System;
using EndLink.Core;
using UnityEngine;

namespace EndLink.Combat
{
    /// <summary>
    /// 主角 B 形态的射击瞄准协调器。
    /// 负责瞄准输入许可、屏幕中心射线、角色朝向和镜头请求；不生成 Projectile，也不结算伤害。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerWeaponController))]
    [RequireComponent(typeof(PlayerCombatDriver))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerStateMachine))]
    public sealed class PlayerAimController : MonoBehaviour
    {
        private const int RaycastBufferSize = 16;

        [Header("瞄准引用")]
        [Tooltip("用于从屏幕中心发射瞄准射线的实际渲染相机。为空时会在运行时缓存 Main Camera。")]
        [SerializeField]
        private Camera aimCamera;

        [Tooltip("负责应用瞄准镜头临时覆盖的玩家视角控制器。为空时会在场景中查找一次。")]
        [SerializeField]
        private PlayerViewController viewController;

        [Header("瞄准检测")]
        [Tooltip("瞄准射线可以命中的物理 Layer。通常包含 Enemy、Environment 和 Interactable，不应包含 Player。")]
        [SerializeField]
        private LayerMask aimLayerMask = ~0;

        [Tooltip("准星没有命中物体时使用的最远瞄准距离。")]
        [SerializeField, Min(1f)]
        private float maxAimDistance = 60f;

        private readonly RaycastHit[] _raycastHits = new RaycastHit[RaycastBufferSize];
        private PlayerWeaponController _weaponController;
        private PlayerCombatDriver _combatDriver;
        private PlayerInputReader _inputReader;
        private PlayerController _playerController;
        private PlayerStateMachine _stateMachine;
        private PlayerTargeting _targeting;
        private Transform _aimFacingTarget;
        private bool _aimInputArmed = true;

        /// <summary>瞄准显隐变化事件。准星 UI 只需监听该事件。</summary>
        public event Action<bool> AimStateChanged;

        /// <summary>当前是否处于 B 形态瞄准。</summary>
        public bool IsAiming { get; private set; }

        /// <summary>当前屏幕中心射线解析出的世界空间瞄准点。</summary>
        public Vector3 AimPoint { get; private set; }

        /// <summary>从玩家根节点指向当前瞄准点的三维方向。</summary>
        public Vector3 AimDirection
        {
            get
            {
                Vector3 direction = AimPoint - transform.position;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            }
        }

        /// <summary>当前 B 形态是否要求先瞄准才能开始基础攻击。</summary>
        public bool RequiresAimForBasicAttack => _weaponController != null
            && _weaponController.CurrentForm == PlayerWeaponForm.B;

        /// <summary>B 形态会占用右键语义，因此状态机不应同时进入 Guard。</summary>
        public bool BlocksGuard => RequiresAimForBasicAttack;

        private void Reset()
        {
            int excludedLayers = LayerMask.GetMask("Player", "Ally");
            aimLayerMask = ~excludedLayers;
            maxAimDistance = 60f;
        }

        private void Awake()
        {
            _weaponController = GetComponent<PlayerWeaponController>();
            _combatDriver = GetComponent<PlayerCombatDriver>();
            _inputReader = GetComponent<PlayerInputReader>();
            _playerController = GetComponent<PlayerController>();
            _stateMachine = GetComponent<PlayerStateMachine>();
            TryGetComponent(out _targeting);
            ResolveViewReferences();
            CreateAimFacingTarget();
        }

        private void OnEnable()
        {
            _weaponController.FormChanged += OnWeaponFormChanged;
            _combatDriver.ActionStarted += OnActionStarted;
            _aimInputArmed = _inputReader == null || !_inputReader.AimHeld;
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.FormChanged -= OnWeaponFormChanged;
            }

            if (_combatDriver != null)
            {
                _combatDriver.ActionStarted -= OnActionStarted;
            }

            EndAim(false);
        }

        private void OnDestroy()
        {
            if (_aimFacingTarget == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_aimFacingTarget.gameObject);
            }
            else
            {
                DestroyImmediate(_aimFacingTarget.gameObject);
            }
        }

        private void OnValidate()
        {
            maxAimDistance = Mathf.Max(1f, maxAimDistance);
        }

        /// <summary>
        /// 推进一帧瞄准输入与瞄准状态。
        /// 由 PlayerStateMachine 在捕获攻击输入前调用，保证同一帧内先确认瞄准，再判断是否允许射击。
        /// </summary>
        public void TickAim()
        {
            if (_inputReader == null)
            {
                return;
            }

            if (!_inputReader.AimHeld)
            {
                _aimInputArmed = true;
                EndAim(false);
                return;
            }

            if (IsAiming)
            {
                if (!CanMaintainAim())
                {
                    EndAim(true);
                    return;
                }

                RefreshAimPoint();
                return;
            }

            if (_aimInputArmed && CanBeginAim())
            {
                BeginAim();
            }
        }

        /// <summary>
        /// 供 PlayerCombatDriver 在动作开始时捕获本次射击瞄准点。
        /// 捕获后即使镜头退出瞄准，Projectile 仍会朝该固定位置发射。
        /// </summary>
        public bool TryCaptureShotAim(CombatActionDefinition actionDefinition, out Vector3 aimPoint)
        {
            aimPoint = AimPoint;
            return IsAiming
                && RequiresAimForBasicAttack
                && actionDefinition != null
                && actionDefinition == _weaponController.CurrentBasicAttackAction;
        }

        /// <summary>判断当前基础攻击是否满足形态输入规则。</summary>
        public bool CanStartBasicAttack()
        {
            return !RequiresAimForBasicAttack || IsAiming;
        }

        private bool CanBeginAim()
        {
            if (!RequiresAimForBasicAttack
                || _combatDriver == null
                || _combatDriver.IsExecutingAction
                || _stateMachine == null)
            {
                return false;
            }

            return _stateMachine.CurrentStateId == PlayerStateId.Idle
                || _stateMachine.CurrentStateId == PlayerStateId.Move;
        }

        private bool CanMaintainAim()
        {
            if (!RequiresAimForBasicAttack || _stateMachine == null)
            {
                return false;
            }

            return _stateMachine.CurrentStateId == PlayerStateId.Idle
                || _stateMachine.CurrentStateId == PlayerStateId.Move;
        }

        private void BeginAim()
        {
            ResolveViewReferences();
            if (aimCamera == null || viewController == null)
            {
                return;
            }

            _targeting?.ClearHardLock();
            IsAiming = true;
            RefreshAimPoint();
            viewController.SetAimView(true);
            _playerController?.SetFacingTarget(_aimFacingTarget);
            AimStateChanged?.Invoke(true);
        }

        private void EndAim(bool requireInputRelease)
        {
            if (requireInputRelease)
            {
                _aimInputArmed = false;
            }

            if (!IsAiming)
            {
                return;
            }

            IsAiming = false;
            _playerController?.ClearFacingTarget();
            viewController?.SetAimView(false);
            AimStateChanged?.Invoke(false);
        }

        private void RefreshAimPoint()
        {
            if (aimCamera == null)
            {
                ResolveViewReferences();
                if (aimCamera == null)
                {
                    return;
                }
            }

            Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            AimPoint = ResolveAimPoint(centerRay);

            if (_aimFacingTarget != null)
            {
                _aimFacingTarget.position = AimPoint;
            }
        }

        private Vector3 ResolveAimPoint(Ray ray)
        {
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                maxAimDistance,
                aimLayerMask,
                QueryTriggerInteraction.Ignore);
            float closestDistance = float.PositiveInfinity;
            Vector3 closestPoint = ray.GetPoint(maxAimDistance);

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _raycastHits[index];
                if (hit.collider == null
                    || hit.collider.transform == transform
                    || hit.collider.transform.IsChildOf(transform)
                    || hit.distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = hit.distance;
                closestPoint = hit.point;
            }

            return closestPoint;
        }

        private void ResolveViewReferences()
        {
            // UnityEngine.Object 可能处于“已销毁但托管引用仍存在”的假 null 状态，不能使用 ??= 重新解析。
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (aimCamera == null)
            {
                Camera[] sceneCameras = FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int index = 0; index < sceneCameras.Length; index++)
                {
                    Camera sceneCamera = sceneCameras[index];
                    if (sceneCamera != null && sceneCamera.enabled && sceneCamera.gameObject.activeInHierarchy)
                    {
                        aimCamera = sceneCamera;
                        break;
                    }
                }
            }

            if (viewController == null)
            {
                viewController = FindFirstObjectByType<PlayerViewController>(FindObjectsInactive.Include);
            }
        }

        private void CreateAimFacingTarget()
        {
            if (_aimFacingTarget != null)
            {
                return;
            }

            GameObject targetObject = new("PlayerAimFacingTarget_Runtime")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _aimFacingTarget = targetObject.transform;
            _aimFacingTarget.position = transform.position + transform.forward * maxAimDistance;
        }

        private void OnWeaponFormChanged(PlayerWeaponForm form)
        {
            if (form != PlayerWeaponForm.B)
            {
                EndAim(true);
            }
        }

        private void OnActionStarted(CombatActionDefinition actionDefinition)
        {
            if (IsAiming
                && RequiresAimForBasicAttack
                && actionDefinition == _weaponController.CurrentBasicAttackAction)
            {
                EndAim(true);
            }
        }
    }
}
