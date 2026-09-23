using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// Makes a player visual prefab self-wiring when it is placed below a player root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerVisualAnimatorBridge : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float moveSpeedDamping = 0.08f;

        private readonly HashSet<int> _parameterHashes = new();
        private CharacterController _characterController;
        private PlayerStateMachine _stateMachine;
        private PlayerCombatDriver _combatDriver;
        private PlayerWeaponController _weaponController;
        private PlayerAnimatorDriver _parentAnimatorDriver;
        private PlayerStateId _lastStateId = PlayerStateId.None;

        private static readonly int TriggerAttackHash = Animator.StringToHash("TriggerAttack");
        private static readonly int TriggerSkillHash = Animator.StringToHash("TriggerSkill");
        private static readonly int TriggerDodgeHash = Animator.StringToHash("TriggerDodge");
        private static readonly int TriggerHitHash = Animator.StringToHash("TriggerHit");
        private static readonly int TriggerDeadHash = Animator.StringToHash("TriggerDead");

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeCombatEvents();
        }

        private void OnDisable()
        {
            if (_combatDriver != null)
            {
                _combatDriver.ActionStarted -= HandleActionStarted;
            }
        }

        private void Update()
        {
            if (animator == null || _parentAnimatorDriver != null)
            {
                return;
            }

            float speed = GetPlanarSpeed();
            SetFloat(CombatAnimatorParams.MoveSpeedHash, speed, moveSpeedDamping);
            SetBool(CombatAnimatorParams.IsMovingHash, speed > 0.05f);

            if (_characterController != null)
            {
                SetBool(CombatAnimatorParams.IsGroundedHash, _characterController.isGrounded);
            }

            if (_weaponController != null)
            {
                SetInteger(CombatAnimatorParams.WeaponFormHash, (int)_weaponController.CurrentForm);
            }

            if (_stateMachine == null)
            {
                return;
            }

            PlayerStateId stateId = _stateMachine.CurrentStateId;
            SetInteger(CombatAnimatorParams.StateIdHash, (int)stateId);
            SetBool(CombatAnimatorParams.IsDeadHash, stateId == PlayerStateId.Dead);

            if (stateId != _lastStateId)
            {
                TriggerStateEnter(stateId);
                _lastStateId = stateId;
            }
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            _characterController = GetComponentInParent<CharacterController>();
            _stateMachine = GetComponentInParent<PlayerStateMachine>();
            _combatDriver = GetComponentInParent<PlayerCombatDriver>();
            _weaponController = GetComponentInParent<PlayerWeaponController>();
            _parentAnimatorDriver = GetComponentInParent<PlayerAnimatorDriver>();

            RebuildParameterCache();
            EnsureAnimationEventReceiver();

            if (_parentAnimatorDriver != null && animator != null)
            {
                _parentAnimatorDriver.SetAnimator(animator);
            }
        }

        private void SubscribeCombatEvents()
        {
            if (_combatDriver == null || _parentAnimatorDriver != null)
            {
                return;
            }

            _combatDriver.ActionStarted -= HandleActionStarted;
            _combatDriver.ActionStarted += HandleActionStarted;
        }

        private void RebuildParameterCache()
        {
            _parameterHashes.Clear();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                _parameterHashes.Add(parameter.nameHash);
            }
        }

        private void EnsureAnimationEventReceiver()
        {
            if (animator == null)
            {
                return;
            }

            if (!animator.TryGetComponent(out CombatAnimationEventReceiver receiver))
            {
                receiver = animator.gameObject.AddComponent<CombatAnimationEventReceiver>();
            }

            receiver.RefreshListeners();
        }

        private float GetPlanarSpeed()
        {
            if (_characterController == null)
            {
                return 0f;
            }

            Vector3 velocity = _characterController.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        private void TriggerStateEnter(PlayerStateId stateId)
        {
            switch (stateId)
            {
                case PlayerStateId.Attack:
                    if (!HasParameter(CombatAnimatorParams.ActionTriggerHash)) SetTrigger(TriggerAttackHash);
                    break;
                case PlayerStateId.Skill:
                    if (!HasParameter(CombatAnimatorParams.ActionTriggerHash)) SetTrigger(TriggerSkillHash);
                    break;
                case PlayerStateId.Dodge:
                    SetFirstTrigger(CombatAnimatorParams.DodgeTriggerHash, TriggerDodgeHash);
                    break;
                case PlayerStateId.Hit:
                    SetFirstTrigger(CombatAnimatorParams.HitTriggerHash, TriggerHitHash);
                    break;
                case PlayerStateId.Dead:
                    SetFirstTrigger(CombatAnimatorParams.DeadTriggerHash, TriggerDeadHash);
                    break;
            }
        }

        private void HandleActionStarted(CombatActionDefinition actionDefinition)
        {
            if (actionDefinition == null || _parentAnimatorDriver != null)
            {
                return;
            }

            int actionId = string.IsNullOrWhiteSpace(actionDefinition.ActionId)
                ? 0
                : Animator.StringToHash(actionDefinition.ActionId);
            SetInteger(CombatAnimatorParams.ActionIdHash, actionId);
            SetInteger(CombatAnimatorParams.ActionTypeHash, (int)actionDefinition.ActionType);
            SetTrigger(CombatAnimatorParams.ActionTriggerHash);
        }

        private bool HasParameter(int hash) => _parameterHashes.Contains(hash);

        private void SetFloat(int hash, float value, float damping)
        {
            if (HasParameter(hash)) animator.SetFloat(hash, value, damping, Time.deltaTime);
        }

        private void SetBool(int hash, bool value)
        {
            if (HasParameter(hash)) animator.SetBool(hash, value);
        }

        private void SetInteger(int hash, int value)
        {
            if (HasParameter(hash)) animator.SetInteger(hash, value);
        }

        private void SetTrigger(int hash)
        {
            if (HasParameter(hash)) animator.SetTrigger(hash);
        }

        private void SetFirstTrigger(int primaryHash, int fallbackHash)
        {
            if (HasParameter(primaryHash)) animator.SetTrigger(primaryHash);
            else SetTrigger(fallbackHash);
        }
    }
}
