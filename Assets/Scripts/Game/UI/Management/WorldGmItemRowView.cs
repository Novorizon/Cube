using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldGmItemRowView : MonoBehaviour
    {
        [SerializeField] private int itemId;
        [SerializeField] private string displayName;
        [SerializeField] private string displayKey;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button add10Button;
        [SerializeField] private Button add100Button;
        [SerializeField] private Button add1000Button;

        public int ItemId => itemId;
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayKey))
                {
                    return LocalizationManager.Get(displayKey);
                }

                return string.IsNullOrEmpty(displayName) ? itemId.ToString() : displayName;
            }
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshText;
            RefreshText();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshText;
        }

        public void Initialize(Action<WorldGmItemRowView, int> addAction)
        {
            Bind(add10Button, addAction, 10);
            Bind(add100Button, addAction, 100);
            Bind(add1000Button, addAction, 1000);
            Refresh();
        }

        public void Refresh()
        {
            RefreshText();
            if (countText != null)
            {
                countText.text = ItemManager.Instance.GetCount(itemId).ToString();
            }
        }

        public void Configure(int itemId, string displayName, string displayKey, TMP_Text nameText, TMP_Text countText, Button add10Button, Button add100Button, Button add1000Button)
        {
            this.itemId = itemId;
            this.displayName = displayName;
            this.displayKey = displayKey;
            this.nameText = nameText;
            this.countText = countText;
            this.add10Button = add10Button;
            this.add100Button = add100Button;
            this.add1000Button = add1000Button;
        }

        private void RefreshText()
        {
            if (nameText != null)
            {
                nameText.text = DisplayName;
            }
        }

        private void Bind(Button button, Action<WorldGmItemRowView, int> addAction, int amount)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => addAction?.Invoke(this, amount));
        }
    }
}
