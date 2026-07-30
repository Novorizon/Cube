using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    [DisallowMultipleComponent]
    public sealed class GuideTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private string targetId;

        public event Action Clicked;

        public string TargetId => targetId;
        public RectTransform RectTransform => transform as RectTransform;

        public static GuideTarget Attach(GameObject targetObject, string id)
        {
            if (targetObject == null)
            {
                return null;
            }

            GuideTarget target = targetObject.GetComponent<GuideTarget>();
            if (target == null)
            {
                target = targetObject.AddComponent<GuideTarget>();
            }

            target.Bind(id);
            return target;
        }

        public void Bind(string id)
        {
            string normalized = id?.Trim() ?? string.Empty;
            if (string.Equals(targetId, normalized, StringComparison.Ordinal))
            {
                if (isActiveAndEnabled)
                {
                    GuideTargetRegistry.Register(targetId, this);
                }

                return;
            }

            if (isActiveAndEnabled)
            {
                GuideTargetRegistry.Unregister(targetId, this);
            }

            targetId = normalized;

            if (isActiveAndEnabled)
            {
                GuideTargetRegistry.Register(targetId, this);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }

        private void OnEnable()
        {
            GuideTargetRegistry.Register(targetId, this);
        }

        private void OnDisable()
        {
            GuideTargetRegistry.Unregister(targetId, this);
        }
    }
}
