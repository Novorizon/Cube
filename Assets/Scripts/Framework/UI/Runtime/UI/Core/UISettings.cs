using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [CreateAssetMenu(menuName = "UI/UI Settings", fileName = "UISettings")]
    public sealed class UISettings : ScriptableObject
    {
        [Header("Canvas Scaler")]
        public int referenceWidth = 1920;
        public int referenceHeight = 1080;

        public CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        [Range(0, 1)]
        public float matchWidthOrHeight = 0.5f;

        [Header("Canvas")]
        public RenderMode renderMode = RenderMode.ScreenSpaceOverlay;
        public float canvasPlaneDistance = 100f;
        public Camera explicitWorldCamera;

        [Tooltip("sortingOrder = (int)layer * sortingOrderStep")]
        public int sortingOrderStep = 100;

        [Header("Tooltip")]
        public string tooltipPrefabPath = "Assets/Arts/UI/Panels/Common/Tooltip.prefab";

        [Min(0f)]
        public float tooltipInitialDelay = 0.55f;

        [Min(0f)]
        public float tooltipReshowDelay = 0.08f;

        [Min(0f)]
        public float tooltipWarmDuration = 0.75f;

        public Vector2 tooltipOffset = new Vector2(16f, 8f);

        [Min(0f)]
        public float tooltipScreenPadding = 12f;
    }
}
