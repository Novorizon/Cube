using System;
using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    internal sealed class WorldBagPanel
    {
        private readonly List<BagSlotView> inventorySlotViews = new List<BagSlotView>();
        private readonly List<BagSlotView> hotSlotViews = new List<BagSlotView>();
        private ISubscription bagChangedSubscription;

        public GameObject Root { get; private set; }

        public bool Bind(Transform root, Action closeClicked)
        {
            Dispose();
            Root = root != null ? root.gameObject : null;
            if (root == null)
            {
                return false;
            }

            WorldPanelBindingUtility.BindButton(root.Find("Close"), () => closeClicked?.Invoke(), "Bag close");
            BindSlots(root);
            bagChangedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, BagChangedMessage>(
                WorldMessageTopic.BagChanged,
                OnBagChanged);
            RefreshSlots();
            return true;
        }

        public void RefreshSlots()
        {
            RefreshSlotViews(inventorySlotViews);
            RefreshSlotViews(hotSlotViews);
        }

        public void Dispose()
        {
            bagChangedSubscription?.Dispose();
            bagChangedSubscription = null;

            DisposeSlotViews(inventorySlotViews);
            DisposeSlotViews(hotSlotViews);
            inventorySlotViews.Clear();
            hotSlotViews.Clear();
            Root = null;
        }

        private void BindSlots(Transform root)
        {
            Transform inventoryRoot =
                FindChildByName(root, "Content") ??
                FindChildByName(root, "InventoryScrollView");
            Transform hotRoot = FindChildByName(root, "HotBarGrid");

            BindSlotGroup(inventoryRoot, BagManager.QuickSlotCount, inventorySlotViews);
            BindSlotGroup(hotRoot, 0, hotSlotViews);
        }

        private static void BindSlotGroup(Transform root, int slotIndexOffset, List<BagSlotView> views)
        {
            if (root == null)
            {
                return;
            }

            List<Transform> slots = CollectSlotTransforms(root);
            for (int i = 0; i < slots.Count; i++)
            {
                int slotIndex = slotIndexOffset + i;
                BagSlotView view = new BagSlotView(slotIndex, slots[i]);
                view.Bind(slotIndex => BagManager.Instance.TryUseSlot(slotIndex));
                views.Add(view);
            }
        }

        private void OnBagChanged(BagChangedMessage message)
        {
            RefreshSlots();
        }

        private static void RefreshSlotViews(List<BagSlotView> views)
        {
            for (int i = 0; i < views.Count; i++)
            {
                views[i]?.Refresh();
            }
        }

        private static void DisposeSlotViews(List<BagSlotView> views)
        {
            for (int i = 0; i < views.Count; i++)
            {
                views[i]?.Dispose();
            }
        }

        private static List<Transform> CollectSlotTransforms(Transform root)
        {
            List<Transform> result = new List<Transform>();
            CollectSlotTransforms(root, result);
            result.Sort(CompareSlotTransform);
            return result;
        }

        private static void CollectSlotTransforms(Transform root, List<Transform> result)
        {
            if (root == null)
            {
                return;
            }

            if (TryGetSlotNumber(root.name, out _))
            {
                result.Add(root);
                return;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                CollectSlotTransforms(root.GetChild(i), result);
            }
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

        private static Transform FindChildByName(Transform root, string childName)
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
                Transform child = FindChildByName(root.GetChild(i), childName);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
