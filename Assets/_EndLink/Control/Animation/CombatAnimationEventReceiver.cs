using System.Collections.Generic;
using UnityEngine;

namespace EndLink.Core
{
    /// <summary>
    /// 战斗动画事件接收器。
    /// 动画 Clip 上的事件应调用本组件的公开方法，再由它转发给同角色上的监听者。
    /// 这样动画事件不需要直接引用具体 Driver 或状态机。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatAnimationEventReceiver : MonoBehaviour
    {
        [Header("监听目标")]
        [Tooltip("监听者搜索根节点。为空时先搜索当前物体及子物体，再沿父级查找最近的角色 Driver。")]
        [SerializeField]
        private GameObject listenerRoot;

        [Tooltip("是否在监听根节点的子物体中搜索监听者。Animator 在模型子物体上时可开启。")]
        [SerializeField]
        private bool includeChildren = true;

        private readonly List<ICombatAnimationEventListener> _listeners = new();
        private readonly List<ICombatRootMotionReceiver> _rootMotionReceivers = new();
        private Animator _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            RefreshListeners();
        }

        private void OnEnable()
        {
            _animator ??= GetComponent<Animator>();
            RefreshListeners();
        }

        /// <summary>
        /// 刷新监听者列表。
        /// 动态添加 Driver、状态机或桥接组件后，可以手动调用一次。
        /// </summary>
        public void RefreshListeners()
        {
            _listeners.Clear();
            _rootMotionReceivers.Clear();

            if (listenerRoot != null)
            {
                CollectListeners(listenerRoot);
                return;
            }

            CollectListeners(gameObject);
            if (_listeners.Count > 0 && _rootMotionReceivers.Count > 0)
            {
                return;
            }

            // Animator 和事件接收器通常位于 Visuals 子物体，而 Driver 位于角色根物体。
            // 未显式指定监听根时沿父级查找，找到最近的一层监听者后停止，避免扫描其它角色。
            Transform current = transform.parent;
            while (current != null && (_listeners.Count == 0 || _rootMotionReceivers.Count == 0))
            {
                CollectListeners(current.gameObject, false);
                current = current.parent;
            }
        }

        private void CollectListeners(GameObject root, bool allowChildren = true)
        {
            if (root == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = allowChildren && includeChildren
                ? root.GetComponentsInChildren<MonoBehaviour>(true)
                : root.GetComponents<MonoBehaviour>();

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatAnimationEventListener listener
                    && !_listeners.Contains(listener))
                {
                    _listeners.Add(listener);
                }

                if (behaviours[i] is ICombatRootMotionReceiver rootMotionReceiver
                    && !_rootMotionReceivers.Contains(rootMotionReceiver))
                {
                    _rootMotionReceivers.Add(rootMotionReceiver);
                }
            }
        }

        /// <summary>
        /// Animator 根运动回调。
        /// 本组件与 Animator 位于同一物体，因此在这里读取动画 Delta，再交给最近的角色移动层处理。
        /// 没有接收者或当前动作未启用 Root Motion 时会主动丢弃 Delta，避免只移动 Visuals。
        /// </summary>
        private void OnAnimatorMove()
        {
            _animator ??= GetComponent<Animator>();
            if (_animator == null || !_animator.applyRootMotion)
            {
                return;
            }

            for (int i = 0; i < _rootMotionReceivers.Count; i++)
            {
                ICombatRootMotionReceiver receiver = _rootMotionReceivers[i];
                if (receiver == null || !receiver.CanReceiveCombatRootMotion)
                {
                    continue;
                }

                receiver.ApplyCombatRootMotion(_animator.deltaPosition, _animator.deltaRotation);
                return;
            }
        }

        /// <summary>动画事件：动作判定开始。</summary>
        public void OnActionHitboxStart()
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i]?.OnActionHitboxStart();
            }
        }

        /// <summary>动画事件：动作判定结束。</summary>
        public void OnActionHitboxEnd()
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i]?.OnActionHitboxEnd();
            }
        }

        /// <summary>动画事件：当前动作允许取消或派生。</summary>
        public void OnActionCanCancel()
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i]?.OnActionCanCancel();
            }
        }

        /// <summary>动画事件：当前动作结束。</summary>
        public void OnActionEnd()
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                _listeners[i]?.OnActionEnd();
            }
        }
    }
}
