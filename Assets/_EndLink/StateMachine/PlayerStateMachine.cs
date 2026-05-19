using System.Collections.Generic;
using EndLink.Combat;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 玩家有限状态机。
    /// 负责持有状态实例、切换当前状态，并把每帧执行权交给当前状态。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombatDriver))]
    public sealed class PlayerStateMachine : MonoBehaviour
    {
        [Header("初始状态")]
        [SerializeField]
        private PlayerStateId initialState = PlayerStateId.Idle;

        [Header("攻击状态")]
        [Tooltip("攻击状态的基础持续时间。胶囊白模阶段先用时间控制，接动画后可改为动画事件驱动。")]
        [SerializeField, Min(0.01f)]
        private float attackDuration = 0.45f;

        [Tooltip("攻击期间移动输入倍率。0 表示站桩攻击，0.3 表示允许轻微滑步，1 表示完全保留移动。")]
        [SerializeField, Range(0f, 1f)]
        private float attackMoveInputScale = 0f;

        private readonly Dictionary<PlayerStateId, IPlayerState> _states = new();
        private IPlayerState _currentState;

        /// <summary>
        /// 当前状态标识，便于调试面板或 Inspector 观察。
        /// </summary>
        public PlayerStateId CurrentStateId => _currentState?.StateId ?? PlayerStateId.None;

        /// <summary>
        /// 攻击状态持续时间。
        /// </summary>
        public float AttackDuration => attackDuration;

        /// <summary>
        /// 攻击状态移动输入倍率。
        /// </summary>
        public float AttackMoveInputScale => attackMoveInputScale;

        private void Awake()
        {
            PlayerInputReader inputReader = GetComponent<PlayerInputReader>();
            PlayerController controller = GetComponent<PlayerController>();
            PlayerCombatDriver combatDriver = GetComponent<PlayerCombatDriver>();

            if (combatDriver == null)
            {
                Debug.LogError("PlayerStateMachine 需要同一物体上挂载 PlayerCombatDriver。", this);
                enabled = false;
                return;
            }

            PlayerStateContext context = new PlayerStateContext(
                this,
                transform,
                inputReader,
                controller,
                combatDriver);

            RegisterState(new PlayerIdleState(context));
            RegisterState(new PlayerMoveState(context));
            RegisterState(new PlayerAttackState(context));
        }

        private void Start()
        {
            ChangeState(initialState);
        }

        private void Update()
        {
            _currentState?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 切换到指定状态。
        /// 如果目标状态就是当前状态，则不重复调用 Exit/Enter。
        /// </summary>
        public void ChangeState(PlayerStateId nextStateId)
        {
            if (nextStateId == PlayerStateId.None || CurrentStateId == nextStateId)
            {
                return;
            }

            if (!_states.TryGetValue(nextStateId, out IPlayerState nextState))
            {
                Debug.LogError($"未注册玩家状态：{nextStateId}", this);
                return;
            }

            _currentState?.Exit();
            _currentState = nextState;
            _currentState.Enter();
        }

        private void RegisterState(IPlayerState state)
        {
            _states[state.StateId] = state;
        }
    }
}
