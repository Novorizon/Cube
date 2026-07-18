using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    public sealed class TechUnlockPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/TechTree/TechUnlockPanel.prefab";
        private const int MaxCostSlotCount = 4;

        private static readonly HashSet<string> MissingIconWarnings = new HashSet<string>();

        private static readonly Color OutsideMaskColor = new Color(0f, 0f, 0f, 0.18f);

        [SerializeField] private Button unlockButton;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconLabel;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Transform itemSlotsRoot;

        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);
        private readonly List<WorldItemSlotView> costSlots = new List<WorldItemSlotView>();

        private TechNodeConfig config;
        private Action<int> unlocked;
        private GameObject outsideClickMask;

        public sealed class Args
        {
            public int TechId { get; set; }
            public Action<int> Unlocked { get; set; }
        }

        protected override void OnCreate()
        {
            BindStaticLayout();
            LocalizationManager.LanguageChanged += Refresh;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        protected override void OnOpen(object args)
        {
            BindStaticLayout();
            EnsureOutsideClickMask();
            ApplyArgs(args);
            RegisterDisposable(Messager.Instance.Subscribe<WorldMessageTopic, ItemChangedMessage>(
                WorldMessageTopic.ItemChanged,
                _ => Refresh()));
            RegisterDisposable(Messager.Instance.Subscribe<WorldMessageTopic, TechChangedMessage>(
                WorldMessageTopic.TechChanged,
                _ => Refresh()));
            Refresh();
        }

        protected override void OnClose()
        {
            DestroyOutsideClickMask();
        }

        private void BindStaticLayout()
        {
            Button rootButton = GetComponent<Button>();
            if (rootButton != null)
            {
                rootButton.onClick.RemoveAllListeners();
                rootButton.enabled = false;
            }

            unlockButton = unlockButton != null ? unlockButton : FindButton(transform, "Unlock", "Yes", "Confirm");
            icon = icon != null ? icon : FindImage(transform, "Icon");
            iconLabel = iconLabel != null ? iconLabel : FindTmpText(transform, "IconLabel");
            nameText = nameText != null ? nameText : FindTmpText(transform, "NameText");
            itemSlotsRoot = itemSlotsRoot != null ? itemSlotsRoot : FindChild(transform, "Items");

            SetChildActive("No", false);
            SetChildActive("OK", false);
            BindButton(unlockButton, ConfirmUnlock, nameof(unlockButton));
            BindCostSlots();
        }

        private void ApplyArgs(object args)
        {
            unlocked = null;
            config = null;

            int techId = 0;
            if (args is Args panelArgs)
            {
                techId = panelArgs.TechId;
                unlocked = panelArgs.Unlocked;
            }
            else if (args is int id)
            {
                techId = id;
            }
            else if (args is TechNodeConfig nodeConfig)
            {
                config = nodeConfig;
                techId = nodeConfig.Id;
            }

            if (config == null &&
                DataManager.Instance.TechNode != null &&
                DataManager.Instance.TechNode.TryGet(techId, out TechNodeConfig loadedConfig))
            {
                config = loadedConfig;
            }
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(TechUnlockPanel)}] {fieldName} is not assigned on prefab: {PrefabPath}");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void BindCostSlots()
        {
            costSlots.Clear();
            if (itemSlotsRoot == null)
            {
                return;
            }

            for (int i = 0; i < MaxCostSlotCount; i++)
            {
                Transform slotTransform = FindChild(itemSlotsRoot, $"ItemSlot{i}");
                if (slotTransform == null)
                {
                    continue;
                }

                WorldItemSlotView slotView = slotTransform.GetComponent<WorldItemSlotView>();
                if (slotView == null)
                {
                    slotView = slotTransform.gameObject.AddComponent<WorldItemSlotView>();
                }

                costSlots.Add(slotView);
            }
        }

        private void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            if (config == null)
            {
                SetText(nameText, LocalizationManager.Get("ui.tech.reason.missing_config"));
                RefreshIcon(null, string.Empty);
                RefreshCostSlots(Array.Empty<ItemStack>());
                RefreshUnlockButton(false);
                SetUnlockInteractable(false);
                return;
            }

            string techName = LocalizedConfigText.TechName(config.Id);
            SetText(nameText, techName);
            RefreshIcon(LoadIcon(config), techName);

            TechResearchState state = TechManager.Instance.GetResearchState(config, out _);
            bool researched = state == TechResearchState.Researched;
            bool canResearch = state == TechResearchState.CanResearch;
            if (itemSlotsRoot != null)
            {
                itemSlotsRoot.gameObject.SetActive(!researched);
            }
            RefreshUnlockButton(canResearch);

            IReadOnlyList<ItemStack> costs = !researched && config.CostGroupId > 0
                ? costResolver.GetCostGroup(config.CostGroupId)
                : Array.Empty<ItemStack>();
            RefreshCostSlots(costs);

            SetUnlockInteractable(canResearch);
        }

        private void RefreshCostSlots(IReadOnlyList<ItemStack> costs)
        {
            for (int i = 0; i < costSlots.Count; i++)
            {
                ItemStack cost = costs != null && i < costs.Count ? costs[i] : null;
                costSlots[i].SetCost(cost);
            }
        }

        private void RefreshIcon(Sprite sprite, string fallbackName)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(0.86f, 0.68f, 0.38f, 0.94f);
                icon.preserveAspect = true;
            }

            if (iconLabel != null)
            {
                iconLabel.gameObject.SetActive(sprite == null);
                iconLabel.text = GetIconLabel(fallbackName);
            }
        }

        private void RefreshUnlockButton(bool visible)
        {
            SetActive(unlockButton, visible);
        }

        private void SetUnlockInteractable(bool interactable)
        {
            if (unlockButton != null)
            {
                unlockButton.interactable = interactable;
            }
        }

        private void ConfirmUnlock()
        {
            if (config == null)
            {
                Toast.Warning(LocalizationManager.Get("ui.tech.reason.missing_config"));
                return;
            }

            if (!TechManager.Instance.TryResearch(config.Id, out string reason))
            {
                Toast.Warning(reason);
                Refresh();
                return;
            }

            Toast.Info(LocalizationManager.Format("ui.tech.toast.unlocked", LocalizedConfigText.TechName(config.Id)));
            unlocked?.Invoke(config.Id);
            WorldMainPanel.Instance?.RefreshNow();
            CloseSelf();
        }

        private void CloseSelf()
        {
            UIManager.Instance.Panels.Hide(PrefabPath);
        }

        private void EnsureOutsideClickMask()
        {
            if (outsideClickMask != null || transform.parent == null)
            {
                return;
            }

            outsideClickMask = new GameObject("TechUnlockPanel_ClickMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(OutsideClickMask));
            RectTransform rect = outsideClickMask.GetComponent<RectTransform>();
            rect.SetParent(transform.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = outsideClickMask.GetComponent<Image>();
            image.color = OutsideMaskColor;
            image.raycastTarget = true;

            OutsideClickMask mask = outsideClickMask.GetComponent<OutsideClickMask>();
            mask.Initialize(CloseSelf);

            outsideClickMask.transform.SetSiblingIndex(transform.GetSiblingIndex());
            transform.SetAsLastSibling();
        }

        private void DestroyOutsideClickMask()
        {
            if (outsideClickMask == null)
            {
                return;
            }

            Destroy(outsideClickMask);
            outsideClickMask = null;
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
                    Debug.LogWarning($"[TechUnlockPanel] Tech icon location must be a full asset path. location: {nodeConfig.IconLocation}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(nodeConfig.IconLocation);
            if (sprite == null && MissingIconWarnings.Add(nodeConfig.IconLocation))
            {
                Debug.LogWarning($"[TechUnlockPanel] Tech icon load failed. location: {nodeConfig.IconLocation}");
            }

            return sprite;
        }

        private static string GetIconLabel(string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(fallbackName))
            {
                return LocalizationManager.Get("ui.tech.icon_fallback");
            }

            return fallbackName.Length <= 2 ? fallbackName : fallbackName.Substring(0, 2);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static Button FindButton(Transform root, params string[] names)
        {
            if (names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Transform child = FindChild(root, names[i]);
                Button button = child != null ? child.GetComponent<Button>() : null;
                if (button != null)
                {
                    return button;
                }
            }

            return null;
        }

        private static Image FindImage(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static TMP_Text FindTmpText(Transform root, string name)
        {
            Transform child = FindChild(root, name);
            return child != null ? child.GetComponent<TMP_Text>() : null;
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

        private void SetChildActive(string childName, bool active)
        {
            Transform child = FindChild(transform, childName);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null)
            {
                component.gameObject.SetActive(active);
            }
        }

        private sealed class OutsideClickMask : MonoBehaviour, IPointerClickHandler
        {
            private Action clicked;

            public void Initialize(Action onClicked)
            {
                clicked = onClicked;
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData == null ||
                    eventData.button == PointerEventData.InputButton.Left ||
                    eventData.button == PointerEventData.InputButton.Right)
                {
                    clicked?.Invoke();
                }
            }
        }
    }
}
