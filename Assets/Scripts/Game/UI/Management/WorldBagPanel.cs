using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// Controls the bag drawer embedded in WorldBottomBarPanel.
    /// It is intentionally not registered as an independent UIPanel.
    /// </summary>
    public sealed class WorldBagPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform bagSlotRoot;
        [SerializeField] private GameObject legacyHotBarRoot;

        private readonly List<BagSlotView> bagSlotViews = new List<BagSlotView>();
        private BagDragController dragController;
        private Action closeClicked;

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        internal void Initialize(BagDragController controller, Action onCloseClicked)
        {
            Dispose();
            dragController = controller;
            closeClicked = onCloseClicked;
            Transform root = transform;
            Root = root != null ? root.gameObject : null;
            if (root == null)
            {
                return;
            }

            BindCloseButton();
            BindSlots();
            RefreshSlots();
        }

        public void SetOpen(bool isOpen)
        {
            if (!isOpen)
            {
                dragController?.Cancel();
            }

            if (Root != null)
            {
                Root.SetActive(isOpen);
            }

            if (isOpen)
            {
                RefreshSlots();
            }
        }

        private void CloseSelf()
        {
            closeClicked?.Invoke();
        }

        public void RefreshSlots()
        {
            RefreshSlotViews(bagSlotViews);
        }

        public void Dispose()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseSelf);
            }

            DisposeSlotViews(bagSlotViews);
            bagSlotViews.Clear();
            dragController?.Cancel();
            dragController = null;
            closeClicked = null;
            Root = null;
        }

        private void BindCloseButton()
        {
            if (closeButton == null)
            {
                Debug.LogError($"[{nameof(WorldBagPanel)}] Close button reference is missing.", this);
                return;
            }

            closeButton.onClick.RemoveListener(CloseSelf);
            closeButton.onClick.AddListener(CloseSelf);
        }

        private void BindSlots()
        {
            if (legacyHotBarRoot != null)
            {
                legacyHotBarRoot.SetActive(false);
            }

            if (bagSlotRoot == null)
            {
                Debug.LogError($"[{nameof(WorldBagPanel)}] Bag slot root reference is missing.", this);
                return;
            }

            BindSlotGroup(bagSlotRoot, BagManager.QuickSlotCount, bagSlotViews, dragController);
        }

        private static void BindSlotGroup(
            Transform root,
            int slotIndexOffset,
            List<BagSlotView> views,
            BagDragController dragController)
        {
            if (root == null)
            {
                return;
            }

            List<Transform> slots = CollectSlotTransforms(root);
            for (int i = 0; i < slots.Count; i++)
            {
                int slotIndex = slotIndexOffset + i;
                BagSlotView view = new BagSlotView(slotIndex, slots[i], dragController);
                view.Bind(
                    slotIndex => BagManager.Instance.TryUseSlot(slotIndex),
                    (fromSlotIndex, toSlotIndex) => BagManager.Instance.TryMoveOrSwapSlot(fromSlotIndex, toSlotIndex));
                views.Add(view);
            }
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

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
