using UnityEngine;

/// <summary>
/// 示例：外部系统如何注册输入。
/// 这里只打印日志，不做业务。
/// </summary>
public sealed class InputReceiverExample : MonoBehaviour, IGameInputReceiver
{
    private void OnEnable()
    {
        SimpleGameInputManager.Instance.RegisterReceiver(this);
    }

    private void OnDisable()
    {
        //if (SimpleGameInputManager.HasInstance)
        {
            SimpleGameInputManager.Instance.UnregisterReceiver(this);
        }
    }

    public void OnLeftClick(Vector2 screenPosition, Vector3 worldPosition, bool hasWorldPoint)
    {
        Debug.Log($"LeftClick screen={screenPosition} world={worldPosition} has={hasWorldPoint}");
    }

    public void OnRightClick(Vector2 screenPosition, Vector3 worldPosition, bool hasWorldPoint)
    {
    }

    public void OnLeftDragBegin(Vector2 startScreenPosition, Vector3 startWorldPosition, bool hasWorldPoint)
    {
    }

    public void OnLeftDragging(Vector2 startScreenPosition, Vector2 currentScreenPosition, Vector2 delta)
    {
    }

    public void OnLeftDragEnd(Vector2 startScreenPosition, Vector2 endScreenPosition)
    {
    }

    public void OnScroll(Vector2 scrollDelta)
    {
    }

    public void OnBuildHotkey(int slotIndex)
    {
    }

    public void OnRotate(int direction)
    {
    }

    public void OnConfirm()
    {
    }

    public void OnCancel()
    {
    }

    public void OnDelete()
    {
    }

    public void OnPointerMove(Vector2 screenPosition, Vector3 worldPosition, bool hasWorldPoint)
    {
    }
}