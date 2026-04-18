using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [CreateAssetMenu(menuName = "UI/UI Settings", fileName = "UISettings")]
    public sealed class UISettings : ScriptableObject
    {
        [Header("Canvas Scaler")]
        public int referenceWidth = 1080;
        public int referenceHeight = 1920;

        public CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        [Range(0, 1)]
        public float matchWidthOrHeight = 0.5f;

        [Header("Canvas")]
        public float canvasPlaneDistance = 100f;

        [Tooltip("sortingOrder = (int)layer * sortingOrderStep")]
        public int sortingOrderStep = 100;
    }
}
