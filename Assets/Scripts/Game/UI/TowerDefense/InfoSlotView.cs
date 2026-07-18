using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class InfoSlotView : MonoBehaviour
    {
        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text valueText;

        [SerializeField]
        private TMP_Text addValueText;

        private string slotKey;

        public string SlotKey
        {
            get
            {
                return slotKey;
            }
        }

        public void Init(TdInfoSlotData data)
        {
            SetData(data);
        }

        public void SetData(TdInfoSlotData data)
        {
            slotKey = data.Key;

            if (iconImage != null)
            {
                iconImage.sprite = data.Icon;
                iconImage.enabled = data.Icon != null;
            }

            if (nameText != null)
            {
                nameText.text = string.IsNullOrEmpty(data.Name) ? string.Empty : data.Name;
            }

            if (valueText != null)
            {
                valueText.text = string.IsNullOrEmpty(data.Value) ? "--" : data.Value;
            }

            if (addValueText != null)
            {
                addValueText.text = string.IsNullOrEmpty(data.AddValue) ? string.Empty : data.AddValue;
            }

            gameObject.SetActive(data.Visible);
        }

        public void Clear()
        {
            slotKey = string.Empty;
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            if (valueText != null)
            {
                valueText.text = string.Empty;
            }

            if (addValueText != null)
            {
                addValueText.text = string.Empty;
            }

            gameObject.SetActive(false);
        }
    }
}
