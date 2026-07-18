using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldItemSlotView : MonoBehaviour
    {
        private static readonly HashSet<string> MissingIconWarnings = new HashSet<string>();
        private static readonly Color EnoughColor = Color.white;
        private static readonly Color MissingColor = new Color(0.90f, 0.12f, 0.08f, 1f);

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text countText;

        private void Awake()
        {
            BindLayout();
        }

        public void SetCost(ItemStack cost)
        {
            BindLayout();
            if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
            {
                SetEmpty();
                return;
            }

            gameObject.SetActive(true);

            int current = ItemManager.Instance.GetCount(cost.ItemId);
            bool enough = current >= cost.Count;
            SetText(enough ? cost.Count.ToString() : $"{current}/{cost.Count}", enough ? EnoughColor : MissingColor);

            if (icon != null)
            {
                Sprite sprite = LoadIcon(cost.ItemId);
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }
        }

        public void SetEmpty()
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            SetText(string.Empty, EnoughColor);
            gameObject.SetActive(false);
        }

        private void BindLayout()
        {
            icon = icon != null ? icon : FindComponentInChild<Image>(transform, "Image");
            countText = countText != null ? countText : FindComponentInChild<TMP_Text>(transform, "Text (TMP)");
        }

        private void SetText(string value, Color color)
        {
            if (countText == null)
            {
                return;
            }

            countText.text = value ?? string.Empty;
            countText.color = color;
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
                    Debug.LogWarning($"[WorldItemSlotView] Item icon location must be a full asset path. location: {config.IconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(config.IconLocation);
            if (sprite == null && MissingIconWarnings.Add(config.IconLocation))
            {
                Debug.LogWarning($"[WorldItemSlotView] Item icon load failed. location: {config.IconLocation}");
            }

            return sprite;
        }

        private static T FindComponentInChild<T>(Transform root, string name) where T : Component
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<T>() : null;
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
