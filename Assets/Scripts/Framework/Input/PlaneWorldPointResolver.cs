using UnityEngine;

/// <summary>
/// 最简单的世界坐标解析：
/// 把鼠标从屏幕发出的射线，打到一个水平平面上。
/// 适合：
/// - 地图先在 y=0 平面
/// - 先做原型
/// </summary>
public sealed class PlaneWorldPointResolver : MonoBehaviour, IWorldPointResolver
{
    [SerializeField] private float planeY = 0f;

    public bool TryGetWorldPoint(Vector2 screenPosition, Camera camera, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (camera == null)
        {
            return false;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        Ray ray = camera.ScreenPointToRay(screenPosition);

        if (!plane.Raycast(ray, out float enter))
        {
            return false;
        }

        worldPosition = ray.GetPoint(enter);
        return true;
    }
}