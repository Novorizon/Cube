namespace Game
{
    /// <summary>
    /// 兼容旧 prefab / 旧代码的包装类。
    /// 最终命名和实际逻辑使用 TargetInfoPanel。
    /// 后续可以把 prefab 上的组件替换为 TargetInfoPanel 后删除这个类。
    /// </summary>
    public sealed class InfoPanel : TargetInfoPanel
    {
    }
}
