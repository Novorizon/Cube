using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public sealed class UIBlocker : MonoBehaviour, IPointerClickHandler, ICanvasRaycastFilter
    {
        private readonly List<RectTransform> passthroughRects = new List<RectTransform>();

        public event Action<PointerEventData> Clicked;

        public static UIBlocker Create(Transform parent, string name = "ModalBlocker", float alpha = 0.4f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = go.AddComponent<Image>();
            img.raycastTarget = true;
            img.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));

            return go.AddComponent<UIBlocker>();
        }

        public void SetPassthroughRects(IEnumerable<RectTransform> rects)
        {
            passthroughRects.Clear();
            if (rects == null)
            {
                return;
            }

            foreach (RectTransform rect in rects)
            {
                if (rect != null)
                {
                    passthroughRects.Add(rect);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke(eventData);
        }

        public bool IsRaycastLocationValid(Vector2 screenPosition, Camera eventCamera)
        {
            for (int i = 0; i < passthroughRects.Count; i++)
            {
                RectTransform rect = passthroughRects[i];
                if (rect == null || !rect.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
