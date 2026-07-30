using System;
using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldToolSlotView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly HashSet<string> MissingIconWarnings = new HashSet<string>();

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject selectedObject;
        [SerializeField] private GameObject dropHighlight;

        private Action<int> clicked;
        private Action<int, PointerEventData> dragStarted;
        private Action<PointerEventData> dragged;
        private Action<PointerEventData> dragEnded;
        private Action<int, PointerEventData> dropped;
        private Action<int, bool> hovered;
        private int slotIndex;
        private int itemId;
        private bool selected;
        private bool highlighted;
        private bool pointerInside;
        private bool tooltipSuppressed;
        private bool tooltipDismissedUntilExit;

        public int SlotIndex => slotIndex;
        public int ItemId => itemId;
        public bool HasTool => itemId > 0;
        public Sprite IconSprite => icon != null ? icon.sprite : null;

        public void Bind(
            int index,
            int toolItemId,
            bool isSelected,
            Action<int> onClicked,
            Action<int, PointerEventData> onDragStarted,
            Action<PointerEventData> onDragged,
            Action<PointerEventData> onDragEnded,
            Action<int, PointerEventData> onDropped,
            Action<int, bool> onHovered)
        {
            BindLayout();
            slotIndex = index;
            itemId = toolItemId;
            selected = isSelected;
            clicked = onClicked;
            dragStarted = onDragStarted;
            dragged = onDragged;
            dragEnded = onDragEnded;
            dropped = onDropped;
            hovered = onHovered;
            highlighted = false;
            Refresh();
            RefreshTooltip();
        }

        public void SetDropHighlighted(bool value)
        {
            highlighted = value;
            RefreshFrame();
        }

        public void SetTooltipSuppressed(bool value)
        {
            tooltipSuppressed = value;
            if (!value && pointerInside)
            {
                tooltipDismissedUntilExit = true;
            }

            HideTooltip();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (HasTool)
            {
                tooltipDismissedUntilExit = true;
                HideTooltip();
                clicked?.Invoke(slotIndex);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (HasTool)
            {
                tooltipDismissedUntilExit = true;
                HideTooltip();
                dragStarted?.Invoke(slotIndex, eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (HasTool)
            {
                dragged?.Invoke(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (HasTool)
            {
                dragEnded?.Invoke(eventData);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            dropped?.Invoke(slotIndex, eventData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            tooltipDismissedUntilExit = false;
            hovered?.Invoke(slotIndex, true);
            RefreshTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            tooltipDismissedUntilExit = false;
            hovered?.Invoke(slotIndex, false);
            HideTooltip();
        }

        private void OnDisable()
        {
            pointerInside = false;
            tooltipDismissedUntilExit = false;
            HideTooltip();
        }

        private void Refresh()
        {
            Sprite sprite = LoadItemIcon(itemId);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.color = HasTool ? Color.white : new Color(1f, 1f, 1f, 0f);
                icon.gameObject.SetActive(HasTool);
            }

            if (nameText != null)
            {
                nameText.text = HasTool ? ToolKitDefinitions.GetToolName(itemId) : string.Empty;
                nameText.gameObject.SetActive(HasTool && sprite == null);
            }

            if (selectedObject != null)
            {
                selectedObject.SetActive(selected);
            }

            if (dropHighlight != null)
            {
                dropHighlight.SetActive(highlighted);
            }

            if (button != null)
            {
                button.interactable = true;
            }

            RefreshFrame();
        }

        private void RefreshFrame()
        {
            if (background == null)
            {
                return;
            }

            background.enabled = true;
            if (highlighted)
            {
                background.color = new Color(1f, 0.96f, 0.66f, 1f);
            }
            else if (selected)
            {
                background.color = new Color(0.98f, 0.86f, 0.42f, 1f);
            }
            else if (HasTool)
            {
                background.color = new Color(0.96f, 0.90f, 0.76f, 0.98f);
            }
            else
            {
                background.color = new Color(0.82f, 0.78f, 0.66f, 0.72f);
            }
        }

        private void RefreshTooltip()
        {
            TooltipManager tooltips = UIManager.Current?.Tooltips;
            if (tooltips == null)
            {
                return;
            }

            if (tooltipSuppressed || tooltipDismissedUntilExit || !pointerInside || !HasTool)
            {
                tooltips.Hide(this);
                return;
            }

            tooltips.Show(
                this,
                transform as RectTransform,
                CreateTooltipData);
        }

        private TooltipData CreateTooltipData()
        {
            return new TooltipData
            {
                Title = ToolKitDefinitions.GetToolName(itemId),
                Description = LocalizedConfigText.ItemDescription(itemId),
                Icon = IconSprite,
            };
        }

        private void HideTooltip()
        {
            UIManager.Current?.Tooltips?.Hide(this);
        }

        private void BindLayout()
        {
            button = button != null ? button : GetComponent<Button>();
            background = background != null ? background : FindImage(transform, "Image") ?? GetComponent<Image>();
            icon = icon != null ? icon : FindImage(transform, "Icon");
            nameText = nameText != null ? nameText : FindText(transform, "NameText");
            selectedObject = selectedObject != null ? selectedObject : FindChildGameObject(transform, "Selected");
            dropHighlight = dropHighlight != null ? dropHighlight : FindChildGameObject(transform, "DropHighlight");
        }

        private static Sprite LoadItemIcon(int toolItemId)
        {
            if (toolItemId <= 0)
            {
                return null;
            }

            if (ToolKitDefinitions.TryGetToolIconLocation(toolItemId, out string toolIconLocation))
            {
                return LoadSprite(toolIconLocation);
            }

            if (ToolKitDefinitions.TryGetTool(toolItemId, out _))
            {
                return null;
            }

            if (DataManager.Instance.Item == null ||
                !DataManager.Instance.Item.TryGet(toolItemId, out ItemConfig config) ||
                config == null ||
                string.IsNullOrWhiteSpace(config.IconLocation))
            {
                return null;
            }

            return LoadSprite(config.IconLocation);
        }

        private static Sprite LoadSprite(string iconLocation)
        {
            if (string.IsNullOrWhiteSpace(iconLocation))
            {
                return null;
            }

            if (!iconLocation.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (MissingIconWarnings.Add(iconLocation))
                {
                    Debug.LogWarning($"[WorldToolSlotView] Tool icon location must be a full asset path. location: {iconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(iconLocation);
            if (sprite == null && MissingIconWarnings.Add(iconLocation))
            {
                Debug.LogWarning($"[WorldToolSlotView] Tool icon load failed. location: {iconLocation}");
            }

            return sprite;
        }

        private static Image FindImage(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static TMP_Text FindText(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static GameObject FindChildGameObject(Transform root, string childName)
        {
            Transform child = FindChild(root, childName);
            return child != null ? child.gameObject : null;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            Transform direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
