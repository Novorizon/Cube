using UnityEngine;

/// <summary>
/// 输入层不负责具体“点到世界哪里”。
/// 它只问外部：
/// “给你一个屏幕坐标，你能不能告诉我世界坐标？”
///
/// 以后你可以自己实现：
/// - 平面射线
/// - Physics.Raycast
/// - 地图格子拾取
/// </summary>
public interface IWorldPointResolver
{
    bool TryGetWorldPoint(Vector2 screenPosition, Camera camera, out Vector3 worldPosition);
}