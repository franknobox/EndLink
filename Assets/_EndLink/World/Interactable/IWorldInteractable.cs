using UnityEngine;

namespace EndLink.World
{
    /// <summary>
    /// 世界可交互对象接口。
    /// 门、电梯、开关、拾取物等对象都可以通过该接口暴露给交互者。
    /// </summary>
    public interface IWorldInteractable
    {
        /// <summary>
        /// 交互对象的根 Transform，用于 UI、调试和距离判断。
        /// </summary>
        Transform InteractionTransform { get; }

        /// <summary>
        /// 当前是否处于可交互状态。
        /// 例如门锁住、机关冷却中或对象已失效时应返回 false。
        /// </summary>
        bool IsInteractionEnabled { get; }

        /// <summary>
        /// 给 UI 使用的交互提示文本。
        /// 第一版只提供数据，不强制生成提示 UI。
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// 判断指定交互者当前是否可以交互。
        /// </summary>
        bool CanInteract(GameObject interactor);

        /// <summary>
        /// 尝试执行交互。
        /// 返回 true 表示本次交互被对象接受并执行。
        /// </summary>
        bool TryInteract(GameObject interactor);

        /// <summary>
        /// 返回从某个位置观察时最合适的交互点。
        /// 用于距离、提示和后续 UI 锚点计算。
        /// </summary>
        Vector3 GetInteractionPoint(Vector3 fromPosition);
    }
}
