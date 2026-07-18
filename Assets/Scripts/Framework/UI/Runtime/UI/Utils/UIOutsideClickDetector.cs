using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    internal struct UIOutsidePointerEvent
    {
        public UIOutsidePointerEvent(Vector2 screenPosition, UICloseReason pointerReason, bool touchLike)
        {
            ScreenPosition = screenPosition;
            PointerReason = pointerReason;
            TouchLike = touchLike;
        }

        public Vector2 ScreenPosition { get; }
        public UICloseReason PointerReason { get; }
        public bool TouchLike { get; }
    }

    [DefaultExecutionOrder(-32000)]
    internal sealed class UIOutsideClickDetector : MonoBehaviour
    {
        readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        UIManager manager;
        PointerEventData pointerEventData;

        public void Initialize(UIManager uiManager)
        {
            manager = uiManager;
        }

        void Update()
        {
            if (manager == null || manager.Panels == null || ShouldSkip())
            {
                return;
            }

            if (!TryGetPointerDown(out UIOutsidePointerEvent pointerEvent))
            {
                return;
            }

            Raycast(pointerEvent.ScreenPosition);
            if (manager.Panels.HandleOutsidePointer(pointerEvent, raycastResults))
            {
                manager.MarkPointerConsumedForCurrentFrame();
            }
        }

        bool ShouldSkip()
        {
            return (manager.Popups != null && manager.Popups.Count > 0) ||
                   (manager.Overlays != null && manager.Overlays.HasBlockingOverlay);
        }

        bool TryGetPointerDown(out UIOutsidePointerEvent pointerEvent)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    pointerEvent = new UIOutsidePointerEvent(touch.position, UICloseReason.LeftOutside, true);
                    return true;
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                pointerEvent = new UIOutsidePointerEvent(Input.mousePosition, UICloseReason.RightOutside, false);
                return true;
            }

            if (Input.GetMouseButtonDown(0))
            {
                pointerEvent = new UIOutsidePointerEvent(Input.mousePosition, UICloseReason.LeftOutside, false);
                return true;
            }

            pointerEvent = default;
            return false;
        }

        void Raycast(Vector2 screenPosition)
        {
            raycastResults.Clear();

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (pointerEventData == null || pointerEventData.pointerPressRaycast.module == null)
            {
                pointerEventData = new PointerEventData(eventSystem);
            }

            pointerEventData.Reset();
            pointerEventData.position = screenPosition;
            eventSystem.RaycastAll(pointerEventData, raycastResults);
        }
    }
}
