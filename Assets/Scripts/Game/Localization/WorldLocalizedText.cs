using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldLocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private Text legacyText;
        [SerializeField] private string key;
        [SerializeField] private string fallback;

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        public void Configure(TMP_Text targetText, string key, string fallback)
        {
            this.targetText = targetText;
            legacyText = null;
            this.key = key;
            this.fallback = fallback;
            Refresh();
        }

        public void Configure(Text targetText, string key, string fallback)
        {
            legacyText = targetText;
            this.targetText = null;
            this.key = key;
            this.fallback = fallback;
            Refresh();
        }

        public void Refresh()
        {
            string value = LocalizationManager.GetOrFallback(key, fallback);
            if (targetText != null)
            {
                targetText.text = value;
            }

            if (legacyText != null)
            {
                legacyText.text = value;
            }
        }
    }
}
