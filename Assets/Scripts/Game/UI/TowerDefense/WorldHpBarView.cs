using TMPro;
using UnityEngine;

namespace Game
{
    public sealed class WorldHpBarView : MonoBehaviour
    {
        [SerializeField] private UIProgressBar hpBar;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private RectTransform rectTransform;

        private Transform target;
        private Vector3 offset;
        private Camera worldCamera;

        public void Bind(Transform targetTransform, string displayName, Vector3 worldOffset, Camera camera)
        {
            target = targetTransform;
            offset = worldOffset;
            worldCamera = camera == null ? Camera.main : camera;
            if (nameText != null)
            {
                nameText.text = displayName;
            }
        }

        public void SetLife(int current, int max)
        {
            if (hpBar != null)
            {
                hpBar.SetValue(current, max);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }
            Camera camera = worldCamera == null ? Camera.main : worldCamera;
            if (camera == null || rectTransform == null)
            {
                return;
            }
            Vector3 screenPosition = camera.WorldToScreenPoint(target.position + offset);
            rectTransform.position = screenPosition;
        }
    }
}
