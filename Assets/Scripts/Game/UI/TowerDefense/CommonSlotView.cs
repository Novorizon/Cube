using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class CommonSlotView : MonoBehaviour
    {
        [SerializeField]
        private Transform contentRoot;

        [SerializeField]
        private Button button;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text countText;

        [SerializeField]
        private GameObject countBadge;

        [SerializeField]
        private GameObject disabledMask;

        private int id;
        private Action<int> clicked;
        private GameObject contentInstance;

        public void Init(int slotId, string displayName, int count, Sprite icon, GameObject contentPrefab, Action<int> onClicked)
        {
            id = slotId;
            clicked = onClicked;

            SetContent(contentPrefab, icon);

            if (nameText != null)
            {
                nameText.text = displayName;
            }

            SetCount(count);

            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }
        }

        public void SetCount(int count)
        {
            if (countText != null)
            {
                countText.text = count.ToString();
            }

            if (countBadge != null)
            {
                countBadge.SetActive(true);
            }

            bool available = count > 0;

            if (button != null)
            {
                button.interactable = available;
            }

            if (disabledMask != null)
            {
                disabledMask.SetActive(!available);
            }
        }

        public void ClearContent()
        {
            id = 0;
            clicked = null;

            if (contentInstance != null)
            {
                Destroy(contentInstance);
                contentInstance = null;
            }

            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            if (countText != null)
            {
                countText.text = string.Empty;
            }

            if (countBadge != null)
            {
                countBadge.SetActive(false);
            }

            if (disabledMask != null)
            {
                disabledMask.SetActive(false);
            }

            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveListener(OnClick);
            }
        }

        private void SetContent(GameObject contentPrefab, Sprite icon)
        {
            if (contentInstance != null)
            {
                Destroy(contentInstance);
                contentInstance = null;
            }

            if (contentPrefab == null)
            {
                return;
            }

            Transform parent = contentRoot != null ? contentRoot : transform;
            contentInstance = Instantiate(contentPrefab, parent, false);
            contentInstance.transform.SetSiblingIndex(Mathf.Min(1, contentInstance.transform.parent.childCount - 1));

            Image iconImage = FindIconImage(contentInstance.transform);
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
        }

        private static Image FindIconImage(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == "Icon")
            {
                Image image = root.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Image image = FindIconImage(root.GetChild(i));
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }

            clicked = null;
        }

        private void OnClick()
        {
            clicked?.Invoke(id);
        }
    }
}
