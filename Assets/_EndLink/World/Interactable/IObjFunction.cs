namespace EndLink.World
{
    /// <summary>
    /// 世界物体功能的统一执行接口。
    /// 门、电梯、检查点等组件只实现自身功能，不负责判断是哪种武器形态命中了交互子物体。
    /// </summary>
    public interface IObjFunction
    {
        /// <summary>
        /// 尝试执行一次物体功能。
        /// 返回 true 表示功能接受并执行了本次请求，false 表示当前状态暂时不能执行。
        /// </summary>
        bool TryExecute(ObjInteractionContext context);
    }
}
