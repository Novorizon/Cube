using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class ToolKitManager
    {
        public static ToolKitManager Instance { get; } = new ToolKitManager();

        private readonly List<int> slots = new List<int>();
        private bool loading;

        private ToolKitManager()
        {
        }

        public int Level { get; private set; } = 1;
        public int Capacity => ToolKitDefinitions.GetCapacity(Level);
        public int CurrentToolItemId { get; private set; }
        public IReadOnlyList<int> Slots => slots;

        public void Initialize()
        {
            loading = true;
            Level = 1;
            CurrentToolItemId = 0;
            slots.Clear();
            ApplyDefaultSlots();
            loading = false;
        }

        public bool CanPutTool(int itemId)
        {
            return itemId > 0 && ToolKitDefinitions.TryGetTool(itemId, out _);
        }

        public bool SetSlot(int slotIndex, int itemId)
        {
            EnsureSlotCount();
            if (slotIndex < 0 || slotIndex >= Capacity)
            {
                return false;
            }

            if (itemId > 0 && !CanPutTool(itemId))
            {
                Debug.LogWarning($"ToolKit set slot failed. Item is not a tool: {itemId}");
                return false;
            }

            if (itemId > 0)
            {
                ClearDuplicateTool(itemId, slotIndex);
            }

            slots[slotIndex] = itemId;
            MarkDirtyIfReady();
            return true;
        }

        public void ClearSlot(int slotIndex)
        {
            EnsureSlotCount();
            if (slotIndex < 0 || slotIndex >= Capacity)
            {
                return;
            }

            slots[slotIndex] = 0;
            MarkDirtyIfReady();
        }

        public bool TryFindTool(ToolType toolType, out int itemId)
        {
            itemId = 0;
            EnsureSlotCount();

            int bestLevel = -1;
            for (int i = 0; i < slots.Count; i++)
            {
                int candidateItemId = slots[i];
                if (!ToolKitDefinitions.TryGetTool(candidateItemId, out ToolDefinition definition) ||
                    definition == null ||
                    definition.ToolType != toolType)
                {
                    continue;
                }

                if (definition.Level > bestLevel)
                {
                    bestLevel = definition.Level;
                    itemId = candidateItemId;
                }
            }

            return itemId > 0;
        }

        public bool TryUseToolForAction(ToolKitActionType actionType, out int itemId)
        {
            itemId = 0;
            ToolType requiredTool = ActionToolResolver.GetRequiredTool(actionType);
            if (requiredTool == ToolType.None)
            {
                CurrentToolItemId = 0;
                return true;
            }

            if (!TryFindTool(requiredTool, out itemId))
            {
                Debug.Log($"ToolKit missing required tool. action: {actionType}, required: {requiredTool}");
                return false;
            }

            CurrentToolItemId = itemId;
            return true;
        }

        public bool TrySelectToolItem(int itemId)
        {
            if (!CanPutTool(itemId))
            {
                return false;
            }

            EnsureSlotCount();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == itemId)
                {
                    CurrentToolItemId = itemId;
                    return true;
                }
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == 0)
                {
                    slots[i] = itemId;
                    CurrentToolItemId = itemId;
                    MarkDirtyIfReady();
                    return true;
                }
            }

            return false;
        }

        public bool CanUpgrade()
        {
            return Level < 4;
        }

        public bool Upgrade()
        {
            if (!CanUpgrade())
            {
                return false;
            }

            Level++;
            EnsureSlotCount();
            MarkDirtyIfReady();
            return true;
        }

        public SaveToolKitData CreateSaveData()
        {
            EnsureSlotCount();
            return new SaveToolKitData
            {
                Level = Level,
                SlotItemIds = slots.ToArray(),
            };
        }

        public void LoadSaveData(SaveToolKitData data)
        {
            loading = true;
            CurrentToolItemId = 0;
            slots.Clear();

            if (data == null)
            {
                Level = 1;
                ApplyDefaultSlots();
                loading = false;
                return;
            }

            Level = Mathf.Clamp(data.Level, 1, 4);
            if (data.SlotItemIds != null)
            {
                for (int i = 0; i < data.SlotItemIds.Length && i < Capacity; i++)
                {
                    int itemId = data.SlotItemIds[i];
                    slots.Add(CanPutTool(itemId) ? itemId : 0);
                }
            }

            EnsureSlotCount();
            loading = false;
        }

        public string GetDisplayText()
        {
            EnsureSlotCount();
            List<string> parts = new List<string>();
            for (int i = 0; i < slots.Count; i++)
            {
                int itemId = slots[i];
                string name = itemId > 0 ? ToolKitDefinitions.GetToolName(itemId) : "Empty";
                if (itemId > 0 && itemId == CurrentToolItemId)
                {
                    name = $"[{name}]";
                }

                parts.Add(name);
            }

            return $"ToolKit Lv.{Level}  {string.Join(" | ", parts)}";
        }

        private void ApplyDefaultSlots()
        {
            IReadOnlyList<int> defaults = ToolKitDefinitions.GetDefaultSlots();
            for (int i = 0; i < defaults.Count && i < Capacity; i++)
            {
                slots.Add(defaults[i]);
            }

            EnsureSlotCount();
        }

        private void EnsureSlotCount()
        {
            int capacity = Capacity;
            while (slots.Count < capacity)
            {
                slots.Add(0);
            }

            while (slots.Count > capacity)
            {
                slots.RemoveAt(slots.Count - 1);
            }
        }

        private void ClearDuplicateTool(int itemId, int exceptSlot)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (i != exceptSlot && slots[i] == itemId)
                {
                    slots[i] = 0;
                }
            }
        }

        private void MarkDirtyIfReady()
        {
            if (!loading)
            {
                StorageManager.Instance.MarkDirty();
            }
        }
    }
}
