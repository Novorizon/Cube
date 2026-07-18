using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TechSlotView : MonoBehaviour
    {
        private static readonly HashSet<string> MissingIconWarnings = new HashSet<string>();

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text unlockText;

        private TechNodeConfig config;
        private Action<int> clicked;

        public void Bind(TechNodeConfig nodeConfig, Action<int> onClicked)
        {
            config = nodeConfig;
            clicked = onClicked;
            BindButton();
            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }
        }

        private void BindButton()
        {
            BindLayout();
            if (button == null)
            {
                Debug.LogError($"[TechSlotView] Missing Button reference on {name}.");
                return;
            }

            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }

        private void Refresh()
        {
            if (config == null)
            {
                return;
            }

            BindLayout();
            TechResearchState state = TechManager.Instance.GetResearchState(config, out _);
            bool researched = state == TechResearchState.Researched;
            bool canResearch = state == TechResearchState.CanResearch;

            SetBackground(researched, canResearch);
            SetText(nameText, LocalizedConfigText.TechName(config.Id));
            RefreshUnlock(researched);
            RefreshIcon();

            if (button != null)
            {
                button.interactable = true;
            }
        }

        private void OnClick()
        {
            if (config != null)
            {
                clicked?.Invoke(config.Id);
            }
        }

        private void SetBackground(bool researched, bool canResearch)
        {
            if (background == null)
            {
                return;
            }

            background.color = researched
                ? new Color(0.92f, 0.98f, 0.86f, 0.98f)
                : canResearch
                    ? new Color(1.00f, 0.92f, 0.70f, 0.98f)
                    : new Color(0.64f, 0.58f, 0.48f, 0.92f);
        }

        private void RefreshIcon()
        {
            Sprite sprite = LoadIcon(config);
            if (icon == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.color = sprite != null ? Color.white : new Color(0.86f, 0.68f, 0.38f, 0.94f);
            icon.preserveAspect = true;
        }

        private void RefreshUnlock(bool researched)
        {
            if (unlockText == null)
            {
                return;
            }

            GameObject unlockObject = unlockText.transform.parent != null && unlockText.transform.parent.name == "Unlock"
                ? unlockText.transform.parent.gameObject
                : unlockText.gameObject;
            unlockObject.SetActive(!researched);
        }

        private void BindLayout()
        {
            button = button != null ? button : GetComponent<Button>();
            background = background != null ? background : GetComponent<Image>();
            icon = icon != null ? icon : FindImage(transform, "Icon");
            nameText = nameText != null ? nameText : FindTmpText(transform, "NameText");
            unlockText = unlockText != null ? unlockText : FindTmpText(transform, "Unlock");
        }

        private static Sprite LoadIcon(TechNodeConfig nodeConfig)
        {
            if (nodeConfig == null || string.IsNullOrWhiteSpace(nodeConfig.IconLocation))
            {
                return null;
            }

            if (!nodeConfig.IconLocation.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (MissingIconWarnings.Add(nodeConfig.IconLocation))
                {
                    Debug.LogWarning($"[TechSlotView] Tech icon location must be a full asset path. location: {nodeConfig.IconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(nodeConfig.IconLocation);
            if (sprite == null && MissingIconWarnings.Add(nodeConfig.IconLocation))
            {
                Debug.LogWarning($"[TechSlotView] Tech icon load failed. location: {nodeConfig.IconLocation}");
            }

            return sprite;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static Image FindImage(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static TMP_Text FindTmpText(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            Transform direct = root.Find(name);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
