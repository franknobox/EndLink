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
        [Tooltip("监听者搜索根节点。为空时使用当前物体。通常填角色根物体。")]
        [SerializeField]
        private GameObject listenerRoot;

        [Tooltip("是否在监听根节点的子物体中搜索监听者。Animator 在模型子物体上时可开启。")]
        [SerializeField]
        private bool includeChildren = true;

        private readonly List<ICombatAnimationEventListener> _listeners = new();

        private void Awake()
        {
            RefreshListeners();
        }

        /// <summary>
        /// 刷新监听者列表。
        /// 动态添加 Driver、状态机或桥接组件后，可以手动调用一次。
        /// </summary>
        public void RefreshListeners()
        {
            _listeners.Clear();

            GameObject root = listenerRoot != null ? listenerRoot : gameObject;
            if (root == null)
            {
                return;
            }

            if (includeChildren)
            {
                root.GetComponentsInChildren(true, _listeners);
            }
            else
            {
                root.GetComponents(_listeners);
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
