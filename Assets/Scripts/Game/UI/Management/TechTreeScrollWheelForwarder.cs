using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    public sealed class TechTreeScrollWheelForwarder : MonoBehaviour, IScrollHandler
    {
        private ScrollRect scrollRect;
        private TechTreePanel panel;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.scrollSensitivity = 0f;
            }

            panel = GetComponentInParent<TechTreePanel>();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (panel == null)
            {
                panel = GetComponentInParent<TechTreePanel>();
            }

            panel?.ScrollVerticalByWheel(eventData);
        }
    }
}
