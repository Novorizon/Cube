using Game.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 简化版输入层：
/// 1. 负责读取 Unity New Input System 输入
/// 2. 区分点击 / 拖拽 / 快捷键 / 滚轮
/// 3. 通过接口通知外部业务
///
/// 不负责：
/// - 建造逻辑
/// - 选中逻辑
/// - 相机逻辑
/// </summary>
public sealed class SimpleGameInputManager : Singleton<SimpleGameInputManager>
{
    [Header("相机")]
    [SerializeField] private Camera inputCamera;

    [Header("参数")]
    [SerializeField] private float dragThreshold = 8f;
    [SerializeField] private float clickMaxDuration = 0.25f;
    [SerializeField] private bool ignoreClickWhenPointerOverUi = true;

    private readonly List<IGameInputReceiver> receivers = new();

    private IWorldPointResolver worldPointResolver;

    // New Input System 的 Action
    private InputAction pointAction;
    private InputAction leftClickAction;
    private InputAction rightClickAction;
    private InputAction scrollAction;

    private InputAction key1Action;
    private InputAction key2Action;
    private InputAction key3Action;
    private InputAction key4Action;
    private InputAction key5Action;
    private InputAction key6Action;

    private InputAction qAction;
    private InputAction eAction;
    private InputAction enterAction;
    private InputAction escAction;
    private InputAction deleteAction;

    // 鼠标当前位置
    private Vector2 currentPointerPosition;
    private Vector2 lastPointerPosition;

    // 左键按下时记录的信息，用于区分“点击”还是“拖拽”
    private bool leftPressed;
    private bool leftDragging;
    private float leftPressTime;
    private Vector2 leftPressScreenPosition;
    private Vector2 leftLastScreenPosition;

    public void SetWorldPointResolver(IWorldPointResolver resolver)
    {
        worldPointResolver = resolver;
    }

    public void RegisterReceiver(IGameInputReceiver receiver)
    {
        if (receiver == null)
        {
            return;
        }

        if (receivers.Contains(receiver))
        {
            return;
        }

        receivers.Add(receiver);
    }

    public void UnregisterReceiver(IGameInputReceiver receiver)
    {
        if (receiver == null)
        {
            return;
        }

        receivers.Remove(receiver);
    }

    public void Initialize()
    {
        CreateActions();
        BindActions();
    }

    private void OnEnable()
    {
        EnableActions();
    }

    private void OnDisable()
    {
        DisableActions();
    }

    private void OnDestroy()
    {
        DisposeActions();
    }

    private void Update()
    {
        UpdatePointerMove();
        UpdateLeftDrag();
    }

    /// <summary>
    /// 创建输入动作
    /// </summary>
    private void CreateActions()
    {
        pointAction = new InputAction("Point", InputActionType.Value, "<Pointer>/position");
        leftClickAction = new InputAction("LeftClick", InputActionType.Button, "<Pointer>/press");
        rightClickAction = new InputAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
        scrollAction = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll");

        key1Action = new InputAction("Key1", InputActionType.Button, "<Keyboard>/1");
        key2Action = new InputAction("Key2", InputActionType.Button, "<Keyboard>/2");
        key3Action = new InputAction("Key3", InputActionType.Button, "<Keyboard>/3");
        key4Action = new InputAction("Key4", InputActionType.Button, "<Keyboard>/4");
        key5Action = new InputAction("Key5", InputActionType.Button, "<Keyboard>/5");
        key6Action = new InputAction("Key6", InputActionType.Button, "<Keyboard>/6");

        qAction = new InputAction("RotateLeft", InputActionType.Button, "<Keyboard>/q");
        eAction = new InputAction("RotateRight", InputActionType.Button, "<Keyboard>/e");
        enterAction = new InputAction("Confirm", InputActionType.Button, "<Keyboard>/enter");
        escAction = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
        deleteAction = new InputAction("Delete", InputActionType.Button, "<Keyboard>/delete");
    }

    /// <summary>
    /// 绑定输入回调
    /// </summary>
    private void BindActions()
    {
        pointAction.performed += ctx =>
        {
            currentPointerPosition = ctx.ReadValue<Vector2>();
        };

        leftClickAction.started += OnLeftStarted;
        leftClickAction.canceled += OnLeftCanceled;

        rightClickAction.performed += OnRightPerformed;
        scrollAction.performed += OnScrollPerformed;

        key1Action.performed += ctx => NotifyBuildHotkey(1);
        key2Action.performed += ctx => NotifyBuildHotkey(2);
        key3Action.performed += ctx => NotifyBuildHotkey(3);
        key4Action.performed += ctx => NotifyBuildHotkey(4);
        key5Action.performed += ctx => NotifyBuildHotkey(5);
        key6Action.performed += ctx => NotifyBuildHotkey(6);

        qAction.performed += ctx => NotifyRotate(-1);
        eAction.performed += ctx => NotifyRotate(1);

        enterAction.performed += ctx => NotifyConfirm();
        escAction.performed += ctx => NotifyCancel();
        deleteAction.performed += ctx => NotifyDelete();
    }

    private void EnableActions()
    {
        pointAction.Enable();
        leftClickAction.Enable();
        rightClickAction.Enable();
        scrollAction.Enable();

        key1Action.Enable();
        key2Action.Enable();
        key3Action.Enable();
        key4Action.Enable();
        key5Action.Enable();
        key6Action.Enable();

        qAction.Enable();
        eAction.Enable();
        enterAction.Enable();
        escAction.Enable();
        deleteAction.Enable();
    }

    private void DisableActions()
    {
        pointAction.Disable();
        leftClickAction.Disable();
        rightClickAction.Disable();
        scrollAction.Disable();

        key1Action.Disable();
        key2Action.Disable();
        key3Action.Disable();
        key4Action.Disable();
        key5Action.Disable();
        key6Action.Disable();

        qAction.Disable();
        eAction.Disable();
        enterAction.Disable();
        escAction.Disable();
        deleteAction.Disable();
    }

    private void DisposeActions()
    {
        pointAction.Dispose();
        leftClickAction.Dispose();
        rightClickAction.Dispose();
        scrollAction.Dispose();

        key1Action.Dispose();
        key2Action.Dispose();
        key3Action.Dispose();
        key4Action.Dispose();
        key5Action.Dispose();
        key6Action.Dispose();

        qAction.Dispose();
        eAction.Dispose();
        enterAction.Dispose();
        escAction.Dispose();
        deleteAction.Dispose();
    }

    /// <summary>
    /// 鼠标左键按下
    /// 这里只记录按下时的数据，不立刻判断是点击还是拖拽
    /// </summary>
    private void OnLeftStarted(InputAction.CallbackContext context)
    {
        currentPointerPosition = pointAction.ReadValue<Vector2>();

        leftPressed = true;
        leftDragging = false;
        leftPressTime = Time.unscaledTime;
        leftPressScreenPosition = currentPointerPosition;
        leftLastScreenPosition = currentPointerPosition;
    }

    /// <summary>
    /// 鼠标左键抬起
    /// 如果之前没有进入拖拽状态，就判定为点击
    /// 如果已经进入拖拽状态，就结束拖拽
    /// </summary>
    private void OnLeftCanceled(InputAction.CallbackContext context)
    {
        if (!leftPressed)
        {
            return;
        }

        Vector2 current = pointAction.ReadValue<Vector2>();

        if (leftDragging)
        {
            for (int i = 0; i < receivers.Count; i++)
            {
                receivers[i].OnLeftDragEnd(leftPressScreenPosition, current);
            }
        }
        else
        {
            float heldTime = Time.unscaledTime - leftPressTime;
            float distance = Vector2.Distance(leftPressScreenPosition, current);

            if (heldTime <= clickMaxDuration && distance <= dragThreshold)
            {
                if (!(ignoreClickWhenPointerOverUi && IsPointerOverUi()))
                {
                    TryGetWorldPoint(current, out Vector3 worldPoint, out bool hasWorldPoint);

                    for (int i = 0; i < receivers.Count; i++)
                    {
                        receivers[i].OnLeftClick(current, worldPoint, hasWorldPoint);
                    }
                }
            }
        }

        leftPressed = false;
        leftDragging = false;
    }

    /// <summary>
    /// 右键点击
    /// </summary>
    private void OnRightPerformed(InputAction.CallbackContext context)
    {
        Vector2 screenPosition = pointAction.ReadValue<Vector2>();

        if (ignoreClickWhenPointerOverUi && IsPointerOverUi())
        {
            return;
        }

        TryGetWorldPoint(screenPosition, out Vector3 worldPoint, out bool hasWorldPoint);

        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnRightClick(screenPosition, worldPoint, hasWorldPoint);
        }
    }

    /// <summary>
    /// 鼠标滚轮
    /// </summary>
    private void OnScrollPerformed(InputAction.CallbackContext context)
    {
        Vector2 scroll = context.ReadValue<Vector2>();

        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnScroll(scroll);
        }
    }

    /// <summary>
    /// 每帧更新鼠标移动事件
    /// </summary>
    private void UpdatePointerMove()
    {
        Vector2 current = pointAction.ReadValue<Vector2>();

        if (current == lastPointerPosition)
        {
            return;
        }

        TryGetWorldPoint(current, out Vector3 worldPoint, out bool hasWorldPoint);

        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnPointerMove(current, worldPoint, hasWorldPoint);
        }

        lastPointerPosition = current;
        currentPointerPosition = current;
    }

    /// <summary>
    /// 每帧判断左键是不是进入拖拽状态
    /// </summary>
    private void UpdateLeftDrag()
    {
        if (!leftPressed)
        {
            return;
        }

        Vector2 current = pointAction.ReadValue<Vector2>();
        Vector2 totalDelta = current - leftPressScreenPosition;
        Vector2 frameDelta = current - leftLastScreenPosition;

        if (!leftDragging)
        {
            if (totalDelta.magnitude >= dragThreshold)
            {
                leftDragging = true;

                TryGetWorldPoint(leftPressScreenPosition, out Vector3 worldPoint, out bool hasWorldPoint);

                for (int i = 0; i < receivers.Count; i++)
                {
                    receivers[i].OnLeftDragBegin(leftPressScreenPosition, worldPoint, hasWorldPoint);
                }
            }
        }
        else
        {
            if (frameDelta != Vector2.zero)
            {
                for (int i = 0; i < receivers.Count; i++)
                {
                    receivers[i].OnLeftDragging(leftPressScreenPosition, current, frameDelta);
                }
            }
        }

        leftLastScreenPosition = current;
    }

    private void NotifyBuildHotkey(int slotIndex)
    {
        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnBuildHotkey(slotIndex);
        }
    }

    private void NotifyRotate(int direction)
    {
        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnRotate(direction);
        }
    }

    private void NotifyConfirm()
    {
        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnConfirm();
        }
    }

    private void NotifyCancel()
    {
        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnCancel();
        }
    }

    private void NotifyDelete()
    {
        for (int i = 0; i < receivers.Count; i++)
        {
            receivers[i].OnDelete();
        }
    }

    private bool TryGetWorldPoint(Vector2 screenPosition, out Vector3 worldPoint, out bool hasWorldPoint)
    {
        worldPoint = default;
        hasWorldPoint = false;

        Camera camera = inputCamera != null ? inputCamera : Camera.main;
        if (camera == null)
        {
            return false;
        }

        if (worldPointResolver == null)
        {
            return false;
        }

        if (!worldPointResolver.TryGetWorldPoint(screenPosition, camera, out worldPoint))
        {
            return false;
        }

        hasWorldPoint = true;
        return true;
    }

    private bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}