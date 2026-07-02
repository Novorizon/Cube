using System;
using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    internal sealed class BagSlotView
    {
        private static readonly HashSet<string> MissingButtonWarnings = new HashSet<string>();
        private static readonly HashSet<string> MissingIconWarnings = new HashSet<string>();

        private readonly int slotIndex;
        private readonly Transform root;
        private readonly Button button;
        private readonly Image iconImage;
        private readonly Text labelText;
        private readonly TMP_Text labelTmpText;
        private readonly Text countText;
        private readonly TMP_Text countTmpText;
        private readonly GameObject lockObject;

        private Action<int> clicked;

        public BagSlotView(int slotIndex, Transform root)
        {
            this.slotIndex = slotIndex;
            this.root = root;
            button = root != null ? root.GetComponent<Button>() : null;
            iconImage = FindImage(root, "Icon");
            labelText = FindText(root, "Label");
            labelTmpText = FindTmpText(root, "Label");
            countText = FindText(root, "Count", "CountText", "Amount", "BadgeText");
            countTmpText = FindTmpText(root, "Count", "CountText", "Amount", "BadgeText");
            lockObject = FindChild(root, "Lock");
        }

        public bool IsValid => root != null;

        public void Bind(Action<int> onClicked)
        {
            clicked = onClicked;
            if (button == null)
            {
                string path = GetTransformPath(root);
                if (MissingButtonWarnings.Add(path))
                {
                    Debug.LogWarning($"[Bag] Slot has no static Button, click use is disabled: {path}");
                }

                return;
            }

            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }

        public void Refresh()
        {
            if (root == null)
            {
                return;
            }

            if (!BagManager.Instance.TryGetSlot(slotIndex, out BagSlot slot) || slot == null || slot.IsEmpty)
            {
                SetEmpty();
                return;
            }

            int count = WorldItemManager.Instance.GetCount(slot.ItemId);
            if (count <= 0)
            {
                SetEmpty();
                return;
            }

            string displayName = GetItemName(slot.ItemId);
            SetLabel(iconImage == null ? $"{displayName}\n{count}" : displayName);
            SetCount(count);

            if (iconImage != null)
            {
                Sprite icon = LoadIcon(slot.ItemId);
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (lockObject != null)
            {
                lockObject.SetActive(false);
            }

            if (button != null)
            {
                button.interactable = true;
            }
        }

        public void Dispose()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }

            clicked = null;
        }

        private void SetEmpty()
        {
            SetLabel(string.Empty);
            SetCount(0);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (lockObject != null)
            {
                lockObject.SetActive(false);
            }

            if (button != null)
            {
                button.interactable = false;
            }
        }

        private void SetLabel(string value)
        {
            if (labelText != null)
            {
                labelText.text = value;
            }

            if (labelTmpText != null)
            {
                labelTmpText.text = value;
            }
        }

        private void SetCount(int count)
        {
            string value = count > 0 ? count.ToString() : string.Empty;
            if (countText != null)
            {
                countText.text = value;
            }

            if (countTmpText != null)
            {
                countTmpText.text = value;
            }
        }

        private void OnClick()
        {
            clicked?.Invoke(slotIndex);
        }

        private static string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }

        private static Sprite LoadIcon(int itemId)
        {
            if (DataManager.Instance.Item == null ||
                !DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) ||
                config == null ||
                string.IsNullOrWhiteSpace(config.IconLocation))
            {
                return null;
            }

            if (!config.IconLocation.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (MissingIconWarnings.Add(config.IconLocation))
                {
                    Debug.LogWarning($"Item icon location must be a full asset path. location: {config.IconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(config.IconLocation);
            if (sprite == null && MissingIconWarnings.Add(config.IconLocation))
            {
                Debug.LogWarning($"Item icon load failed. location: {config.IconLocation}");
            }

            return sprite;
        }

        private static Image FindImage(Transform root, params string[] names)
        {
            Transform child = FindChildTransform(root, names);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Text FindText(Transform root, params string[] names)
        {
            Transform child = FindChildTransform(root, names);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static TMP_Text FindTmpText(Transform root, params string[] names)
        {
            Transform child = FindChildTransform(root, names);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static GameObject FindChild(Transform root, params string[] names)
        {
            Transform child = FindChildTransform(root, names);
            return child != null ? child.gameObject : null;
        }

        private static Transform FindChildTransform(Transform root, params string[] names)
        {
            if (root == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Transform child = root.Find(names[i]);
                if (child != null)
                {
                    return child;
                }
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildTransform(root.GetChild(i), names);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = $"{parent.name}/{path}";
                parent = parent.parent;
            }

            return path;
        }
    }
}
