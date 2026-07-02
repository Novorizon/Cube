using System;
using Game.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game
{
    internal sealed class WorldBottomBarPanel
    {
        private readonly List<BagSlotView> hotSlotViews = new List<BagSlotView>();
        private GameObject bagOpenIcon;
        private GameObject bagCloseIcon;
        private ISubscription bagChangedSubscription;

        public TMP_Text CurrentModeText { get; private set; }
        public TMP_Text SelectedSummaryText { get; private set; }

        public bool Bind(
            Transform root,
            Action bagClicked,
            Action buildClicked,
            Action techClicked)
        {
            if (root == null)
            {
                Clear();
                return false;
            }

            Transform hotPanel = root.Find("HotPanel") ?? root;
            CurrentModeText = WorldPanelBindingUtility.FindText(hotPanel.Find("ModePanel"), "ModeHint") ??
                              WorldPanelBindingUtility.FindText(hotPanel, "ModeHint");
            SelectedSummaryText = WorldPanelBindingUtility.FindText(hotPanel.Find("SelectionPanel"), "SelectionSlot") ??
                                  WorldPanelBindingUtility.FindText(hotPanel, "SelectionSlot");

            Transform bag = hotPanel.Find("Bag") ?? hotPanel.Find("Entry_Bag");
            bagOpenIcon = bag != null && bag.Find("Open") != null ? bag.Find("Open").gameObject : null;
            bagCloseIcon = bag != null && bag.Find("Close") != null ? bag.Find("Close").gameObject : null;

            Transform build = hotPanel.Find("Build") ?? hotPanel.Find("Entry_Build");
            Transform tech = hotPanel.Find("Tech") ?? hotPanel.Find("TechEntry") ?? hotPanel.Find("Entry_Tech");

            WorldPanelBindingUtility.BindButton(
                bag,
                () => bagClicked?.Invoke(),
                "Bag entry");
            WorldPanelBindingUtility.BindButton(
                build,
                () => buildClicked?.Invoke(),
                "Build entry");
            WorldPanelBindingUtility.BindButton(
                tech,
                () => techClicked?.Invoke(),
                "Tech entry");

            BindHotSlots(hotPanel);
            bagChangedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, BagChangedMessage>(
                WorldMessageTopic.BagChanged,
                OnBagChanged);
            RefreshSlots();
            return true;
        }

        public void SetBagOpen(bool isOpen)
        {
            if (bagOpenIcon != null)
            {
                bagOpenIcon.SetActive(!isOpen);
            }

            if (bagCloseIcon != null)
            {
                bagCloseIcon.SetActive(isOpen);
            }
        }

        public void RefreshSlots()
        {
            for (int i = 0; i < hotSlotViews.Count; i++)
            {
                hotSlotViews[i]?.Refresh();
            }
        }

        public void Dispose()
        {
            bagChangedSubscription?.Dispose();
            bagChangedSubscription = null;

            for (int i = 0; i < hotSlotViews.Count; i++)
            {
                hotSlotViews[i]?.Dispose();
            }

            hotSlotViews.Clear();
            Clear();
        }

        private void BindHotSlots(Transform hotPanel)
        {
            for (int i = 0; i < hotSlotViews.Count; i++)
            {
                hotSlotViews[i]?.Dispose();
            }

            hotSlotViews.Clear();

            Transform hotBarGrid = hotPanel != null ? hotPanel.Find("HotBarGrid") : null;
            if (hotBarGrid == null)
            {
                return;
            }

            List<Transform> slots = CollectSlotTransforms(hotBarGrid);
            for (int i = 0; i < slots.Count && i < BagManager.QuickSlotCount; i++)
            {
                BagSlotView view = new BagSlotView(i, slots[i]);
                view.Bind(slotIndex => BagManager.Instance.TryUseSlot(slotIndex));
                hotSlotViews.Add(view);
            }
        }

        private void OnBagChanged(BagChangedMessage message)
        {
            RefreshSlots();
        }

        private void Clear()
        {
            CurrentModeText = null;
            SelectedSummaryText = null;
            bagOpenIcon = null;
            bagCloseIcon = null;
        }

        private static List<Transform> CollectSlotTransforms(Transform root)
        {
            List<Transform> result = new List<Transform>();
            for (int i = 0; root != null && i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (TryGetSlotNumber(child.name, out _))
                {
                    result.Add(child);
                }
            }

            result.Sort(CompareSlotTransform);
            return result;
        }

        private static int CompareSlotTransform(Transform left, Transform right)
        {
            TryGetSlotNumber(left != null ? left.name : string.Empty, out int leftNumber);
            TryGetSlotNumber(right != null ? right.name : string.Empty, out int rightNumber);
            return leftNumber.CompareTo(rightNumber);
        }

        private static bool TryGetSlotNumber(string name, out int number)
        {
            number = 0;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Slot_", StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(name.Substring("Slot_".Length), out number);
        }
    }
}
