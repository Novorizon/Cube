using UnityEngine.EventSystems;

namespace UI
{
    internal static class UICloseTriggerUtility
    {
        public static UICloseTriggers ToTrigger(UICloseReason reason)
        {
            switch (reason)
            {
                case UICloseReason.CloseButton:
                    return UICloseTriggers.CloseButton;
                case UICloseReason.LeftOutside:
                    return UICloseTriggers.LeftOutside;
                case UICloseReason.RightOutside:
                    return UICloseTriggers.RightOutside;
                case UICloseReason.Back:
                    return UICloseTriggers.Back;
                default:
                    return UICloseTriggers.None;
            }
        }

        public static UICloseReason ToOutsideReason(PointerEventData eventData, UIView view)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                return UICloseReason.RightOutside;
            }

            return ToOutsideReason(UICloseReason.LeftOutside, IsTouchLikePointer(eventData), view);
        }

        public static UICloseReason ToOutsideReason(UICloseReason pointerReason, bool touchLike, UIView view)
        {
            if (pointerReason == UICloseReason.RightOutside)
            {
                return UICloseReason.RightOutside;
            }

            if (touchLike &&
                view != null &&
                (view.CloseTriggers & UICloseTriggers.RightOutside) != 0)
            {
                return UICloseReason.RightOutside;
            }

            return UICloseReason.LeftOutside;
        }

        private static bool IsTouchLikePointer(PointerEventData eventData)
        {
            // StandaloneInputModule uses negative pointer ids for mouse buttons and non-negative ids for touches.
            return eventData != null &&
                   eventData.button == PointerEventData.InputButton.Left &&
                   eventData.pointerId >= 0;
        }
    }
}
