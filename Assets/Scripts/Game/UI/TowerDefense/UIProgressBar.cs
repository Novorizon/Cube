using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class UIProgressBar : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text valueText;

        public void SetValue(int current, int max)
        {
            float percent = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);
            SetPercent(percent);
            if (valueText != null)
            {
                valueText.text = $"{current}/{max}";
            }
        }

        public void SetPercent(float percent)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(percent);
            }
        }
    }
}
