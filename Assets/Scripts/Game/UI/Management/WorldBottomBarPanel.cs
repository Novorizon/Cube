using System;
using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldBottomBarPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text currentModeText;
        [SerializeField] private TMP_Text selectedSummaryText;
        [SerializeField] private RectTransform hotBarGridRect;
        [SerializeField] private WorldBagPanel bagPanel;
        [SerializeField] private GameObject bagOpenIcon;
        [SerializeField] private GameObject bagCloseIcon;
        [SerializeField] private Button bagButton;
        [SerializeField] private Button buildButton;
        [SerializeField] private Button toolKitButton;
        [SerializeField] private Button techButton;
        [SerializeField] private RectTransform[] hotSlotRects = Array.Empty<RectTransform>();

        private readonly List<BagSlotView> hotSlotViews = new List<BagSlotView>();
        private readonly BagDragController bagDragController = new BagDragController();
        private ISubscription bagChangedSubscription;

        public TMP_Text CurrentModeText => currentModeText;
        public TMP_Text SelectedSummaryText => selectedSummaryText;
        public RectTransform HotBarGridRect => hotBarGridRect;
        public RectTransform RootRect => transform as RectTransform;
        public bool IsBagOpen => bagPanel != null && bagPanel.IsOpen;

        public void Initialize(
            Action bagClicked,
            Action buildClicked,
            Action toolKitClicked,
            Action techClicked)
        {
            DisposeRuntimeBindings();
            if (bagPanel == null)
            {
                Debug.LogError($"[{nameof(WorldBottomBarPanel)}] BagPanel reference is missing.", this);
            }

            bagPanel?.Initialize(bagDragController, () => SetBagOpen(false));
            BindButton(bagButton, bagClicked);
            BindButton(buildButton, buildClicked);
            BindButton(toolKitButton, toolKitClicked);
            BindButton(techButton, techClicked);
            BindHotSlots();
            bagChangedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, BagChangedMessage>(
                WorldMessageTopic.BagChanged,
                OnBagChanged);
            SetBagOpen(false);
            RefreshSlots();
        }

        public void SetBagOpen(bool isOpen)
        {
            if (!isOpen)
            {
                bagDragController.Cancel();
            }

            bagPanel?.SetOpen(isOpen);
            SetOpenCloseIcons(bagOpenIcon, bagCloseIcon, isOpen);
        }

        public void SetTechOpen(bool isOpen)
        {
        }

        public void RefreshSlots()
        {
            for (int i = 0; i < hotSlotViews.Count; i++)
            {
                hotSlotViews[i]?.Refresh();
            }

            bagPanel?.RefreshSlots();
        }

        public void Dispose()
        {
            DisposeRuntimeBindings();
        }

        public bool TryGetHotSlotRect(int slotNumber, out RectTransform slotRect)
        {
            int index = slotNumber - 1;
            if (index >= 0 && index < hotSlotRects.Length)
            {
                slotRect = hotSlotRects[index];
                return slotRect != null;
            }

            slotRect = null;
            return false;
        }

        private void OnDestroy()
        {
            DisposeRuntimeBindings();
        }

        private void BindHotSlots()
        {
            for (int i = 0; i < hotSlotRects.Length && i < BagManager.QuickSlotCount; i++)
            {
                RectTransform slotRect = hotSlotRects[i];
                if (slotRect == null)
                {
                    continue;
                }

                BagSlotView view = new BagSlotView(i, slotRect, bagDragController);
                view.Bind(
                    slotIndex => BagManager.Instance.TryUseSlot(slotIndex),
                    (fromSlotIndex, toSlotIndex) => BagManager.Instance.TryMoveOrSwapSlot(fromSlotIndex, toSlotIndex));
                hotSlotViews.Add(view);
            }
        }

        private void OnBagChanged(BagChangedMessage message)
        {
            RefreshSlots();
        }

        private void DisposeRuntimeBindings()
        {
            bagDragController.Cancel();
            bagChangedSubscription?.Dispose();
            bagChangedSubscription = null;

            for (int i = 0; i < hotSlotViews.Count; i++)
            {
                hotSlotViews[i]?.Dispose();
            }

            hotSlotViews.Clear();
            bagPanel?.Dispose();
        }

        private static void BindButton(Button button, Action clicked)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => clicked?.Invoke());
        }

        private static void SetOpenCloseIcons(GameObject openIcon, GameObject closeIcon, bool isOpen)
        {
            if (openIcon != null)
            {
                openIcon.SetActive(!isOpen);
            }

            if (closeIcon != null)
            {
                closeIcon.SetActive(isOpen);
            }
        }
    }
}
