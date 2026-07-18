using UnityEngine;

namespace Game
{
    [ExecuteAlways]
    public sealed class BattleSafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private RectTransform target;

        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (lastSafeArea != Screen.safeArea || lastScreenSize != screenSize)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (target == null)
            {
                target = transform as RectTransform;
            }

            if (target == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            target.anchorMin = new Vector2(safeArea.xMin / Screen.width, safeArea.yMin / Screen.height);
            target.anchorMax = new Vector2(safeArea.xMax / Screen.width, safeArea.yMax / Screen.height);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
