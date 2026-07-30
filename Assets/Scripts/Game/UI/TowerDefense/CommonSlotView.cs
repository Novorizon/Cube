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
        private BattleSlotContentView contentView;

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

        [SerializeField]
        private Image cooldownMask;

        [SerializeField]
        private TMP_Text cooldownText;

        private int id;
        private Action<int> clicked;
        private GameObject contentInstance;
        private bool hasCount;
        private bool coolingDown;

        public void Init(int slotId, string displayName, int count, Sprite icon, Action<int> onClicked)
        {
            Init(slotId, displayName, count, icon, null, onClicked);
        }

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

            hasCount = count > 0;
            RefreshInteractable();
        }

        public void SetCooldown(float remainingSeconds, float durationSeconds)
        {
            coolingDown = remainingSeconds > 0f && durationSeconds > 0f;
            if (cooldownMask != null)
            {
                cooldownMask.gameObject.SetActive(coolingDown);
                cooldownMask.fillAmount = coolingDown ? Mathf.Clamp01(remainingSeconds / durationSeconds) : 0f;
            }

            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(coolingDown);
                cooldownText.text = coolingDown ? Mathf.CeilToInt(remainingSeconds).ToString() : string.Empty;
            }

            RefreshInteractable();
        }

        public void ClearContent()
        {
            id = 0;
            clicked = null;

            if (contentView != null)
            {
                contentView.SetIcon(null);
            }

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

            if (cooldownMask != null)
            {
                cooldownMask.gameObject.SetActive(false);
                cooldownMask.fillAmount = 0f;
            }

            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(false);
                cooldownText.text = string.Empty;
            }

            hasCount = false;
            coolingDown = false;

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

            if (contentView != null)
            {
                contentView.gameObject.SetActive(true);
                contentView.SetIcon(icon);
                return;
            }

            if (contentPrefab == null)
            {
                return;
            }

            Transform parent = contentRoot != null ? contentRoot : transform;
            contentInstance = Instantiate(contentPrefab, parent, false);
            contentInstance.transform.SetSiblingIndex(Mathf.Min(1, contentInstance.transform.parent.childCount - 1));

            BattleSlotContentView dynamicContentView = contentInstance.GetComponent<BattleSlotContentView>();
            if (dynamicContentView != null)
            {
                dynamicContentView.SetIcon(icon);
            }
            else
            {
                Debug.LogError($"Slot content prefab is missing {nameof(BattleSlotContentView)}.", contentPrefab);
            }
        }

        private void RefreshInteractable()
        {
            bool interactable = hasCount && !coolingDown;
            if (button != null)
            {
                button.interactable = interactable;
            }

            if (disabledMask != null)
            {
                disabledMask.SetActive(!hasCount);
            }
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
