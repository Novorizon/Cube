using System.Collections.Generic;

namespace Game
{
    public static class ToolKitDefinitions
    {
        private static readonly Dictionary<int, ToolDefinition> Tools = new Dictionary<int, ToolDefinition>
        {
            { ItemIds.BasicAxe, new ToolDefinition(ItemIds.BasicAxe, ToolType.Axe, 1, "Basic Axe") },
            { ItemIds.BasicPickaxe, new ToolDefinition(ItemIds.BasicPickaxe, ToolType.Pickaxe, 1, "Basic Pickaxe") },
            { ItemIds.BasicHoe, new ToolDefinition(ItemIds.BasicHoe, ToolType.Hoe, 1, "Basic Hoe") },
            { ItemIds.BasicWateringCan, new ToolDefinition(ItemIds.BasicWateringCan, ToolType.WateringCan, 1, "Basic Watering Can") },
            { ItemIds.BasicFishingRod, new ToolDefinition(ItemIds.BasicFishingRod, ToolType.FishingRod, 1, "Basic Fishing Rod") },
            { ItemIds.BasicHammer, new ToolDefinition(ItemIds.BasicHammer, ToolType.Hammer, 1, "Basic Hammer") },
        };

        private static readonly int[] DefaultSlots =
        {
            ItemIds.BasicAxe,
            ItemIds.BasicPickaxe,
            ItemIds.BasicHoe,
        };

        public static IReadOnlyList<int> GetDefaultSlots()
        {
            return DefaultSlots;
        }

        public static int GetCapacity(int level)
        {
            if (level <= 1)
            {
                return 3;
            }

            if (level == 2)
            {
                return 4;
            }

            if (level == 3)
            {
                return 5;
            }

            return 6;
        }

        public static bool TryGetTool(int itemId, out ToolDefinition definition)
        {
            return Tools.TryGetValue(itemId, out definition);
        }

        public static string GetToolName(int itemId)
        {
            if (itemId > 0)
            {
                return LocalizedConfigText.ItemName(itemId);
            }

            return LocalizationManager.Get("ui.common.empty");
        }
    }
}
