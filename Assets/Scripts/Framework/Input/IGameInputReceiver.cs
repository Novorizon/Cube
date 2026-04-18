using UnityEngine;

/// <summary>
/// 业务层实现这个接口，就可以收到输入事件。
/// 注意：这里只是“通知”，不处理具体业务。
/// 比如：
/// - 选中系统
/// - 建造系统
/// - 相机系统
/// 都可以实现这个接口。
/// </summary>
public interface IGameInputReceiver
{
    // 鼠标左键点击
    void OnLeftClick(Vector2 screenPosition, Vector3 worldPosition, bool hasWorldPoint);

    // 鼠标右键点击
    void OnRightClick(Vector2 screenPosition, Vector3 worldPosition, bool hasWorldPoint);

    // 左键开始拖拽
    void OnLeftDragBegin(Vector2 startScreenPosition, Vector3 startWorldPosition, bool hasWorldPoint);

    // 左键拖拽中
    void OnLeftDragging(Vector2 startScreenPosition, Vector2 currentScreenPosition, Vector2 delta);

    // 左键拖拽结束
    void OnLeftDragEnd(Vector2 startScreenPosition, Vector2 endScreenPosition);

    // 鼠标滚轮
    void OnScroll(Vector2 scrollDelta);

    // 建造快捷键 1~6
    void OnBuildHotkey(int slotIndex);

    // 旋转
    void OnRotate(int direction);

    // 确认
    void OnConfirm();

    // 取消
    void OnCancel();

    // 删除
    void OnDelete();

    // 鼠标移动
    void OnPointerMove(Vector2 screenPosition, Vector3 worldPosition, bool hasWorldPoint);
}