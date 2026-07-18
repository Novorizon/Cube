using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldFarmSeedView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Text legacyNameText;
        [SerializeField] private Text legacyInfoText;
        [SerializeField] private Button button;

        private void Awake()
        {
            BindMissingReferences();
        }

        public void Configure(Image backgroundImage, Image iconImage, TMP_Text nameText, TMP_Text infoText, Button button)
        {
            this.backgroundImage = backgroundImage;
            this.iconImage = iconImage;
            this.nameText = nameText;
            this.infoText = infoText;
            this.button = button;
        }

        public void SetBackground(Color color)
        {
            BindMissingReferences();
            if (backgroundImage != null)
            {
                backgroundImage.color = color;
            }
        }

        public void SetBackgroundAlpha(float alpha)
        {
            BindMissingReferences();
            if (backgroundImage == null)
            {
                return;
            }

            Color color = backgroundImage.color;
            color.a = alpha;
            backgroundImage.color = color;
        }

        public void SetIcon(Sprite sprite, Color color)
        {
            BindMissingReferences();
            if (iconImage == null)
            {
                return;
            }

            iconImage.sprite = sprite;
            iconImage.preserveAspect = true;
            iconImage.gameObject.SetActive(sprite != null);
            iconImage.color = color;
        }

        public void SetName(string text, Color color)
        {
            BindMissingReferences();
            if (nameText != null)
            {
                nameText.text = text;
                nameText.color = color;
            }

            if (legacyNameText != null)
            {
                legacyNameText.text = text;
                legacyNameText.color = color;
            }
        }

        public void SetInfo(string text, Color color)
        {
            BindMissingReferences();
            if (infoText != null)
            {
                infoText.text = text;
                infoText.color = color;
            }

            if (legacyInfoText != null)
            {
                legacyInfoText.text = text;
                legacyInfoText.color = color;
            }
        }

        public void SetClick(Action clicked, bool interactable)
        {
            BindMissingReferences();
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
        }

        private void BindMissingReferences()
        {
            backgroundImage = backgroundImage != null ? backgroundImage : GetComponent<Image>();
            button = button != null ? button : GetComponent<Button>();
            iconImage = iconImage != null ? iconImage : FindDescendantByName(transform, "Icon")?.GetComponent<Image>();

            Transform nameRoot = FindDescendantByName(transform, "Name");
            Transform infoRoot = FindDescendantByName(transform, "Info");
            nameText = nameText != null ? nameText : nameRoot?.GetComponent<TMP_Text>();
            infoText = infoText != null ? infoText : infoRoot?.GetComponent<TMP_Text>();
            legacyNameText = legacyNameText != null ? legacyNameText : nameRoot?.GetComponent<Text>();
            legacyInfoText = legacyInfoText != null ? legacyInfoText : infoRoot?.GetComponent<Text>();
        }

        private static Transform FindDescendantByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform found = FindDescendantByName(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
