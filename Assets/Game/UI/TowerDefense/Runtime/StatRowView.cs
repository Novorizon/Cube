using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class StatRowView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text extraText;

        public void SetValue(Sprite icon, string label, string value, string extra = null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(icon != null);
            }
            if (labelText != null)
            {
                labelText.text = label;
            }
            if (valueText != null)
            {
                valueText.text = value;
            }
            if (extraText != null)
            {
                extraText.text = string.IsNullOrEmpty(extra) ? string.Empty : extra;
                extraText.gameObject.SetActive(!string.IsNullOrEmpty(extra));
            }
        }
    }
}
