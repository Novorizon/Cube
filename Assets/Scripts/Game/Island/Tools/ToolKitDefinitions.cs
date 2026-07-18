using System.Collections.Generic;

namespace Game
{
    public static class ToolKitDefinitions
    {
        private const string AxeIconLocation = "Assets/Arts/UI/Icons/Tool/axe.png";
        private const string PickaxeIconLocation = "Assets/Arts/UI/Icons/Tool/Pickaxe.png";
        private const string FishingRodIconLocation = "Assets/Arts/UI/Icons/Tool/FishingRod.png";

        private static readonly Dictionary<int, ToolDefinition> Tools = new Dictionary<int, ToolDefinition>
        {
            { ItemIds.BasicAxe, new ToolDefinition(ItemIds.BasicAxe, ToolType.Axe, 1, "Stone Axe") },
            { ItemIds.BasicPickaxe, new ToolDefinition(ItemIds.BasicPickaxe, ToolType.Pickaxe, 1, "Stone Pickaxe") },
            { ItemIds.BasicHoe, new ToolDefinition(ItemIds.BasicHoe, ToolType.Hoe, 1, "Stone Hoe") },
            { ItemIds.BasicWateringCan, new ToolDefinition(ItemIds.BasicWateringCan, ToolType.WateringCan, 1, "Wooden Watering Can") },
            { ItemIds.BasicFishingRod, new ToolDefinition(ItemIds.BasicFishingRod, ToolType.FishingRod, 1, "Wooden Fishing Rod") },
            { ItemIds.BasicHammer, new ToolDefinition(ItemIds.BasicHammer, ToolType.Hammer, 1, "Stone Hammer") },
        };

        private static readonly int[] DefaultSlots = { };

        public static IReadOnlyList<int> GetDefaultSlots()
        {
            return DefaultSlots;
        }

        public static int GetCapacity(int level)
        {
            if (level <= 1)
            {
                return 10;
            }

            if (level == 2)
            {
                return 12;
            }

            if (level == 3)
            {
                return 14;
            }

            return 16;
        }

        public static bool TryGetTool(int itemId, out ToolDefinition definition)
        {
            return Tools.TryGetValue(itemId, out definition);
        }

        public static bool TryGetToolIconLocation(int itemId, out string iconLocation)
        {
            iconLocation = null;
            if (!Tools.TryGetValue(itemId, out ToolDefinition definition))
            {
                return false;
            }

            switch (definition.ToolType)
            {
                case ToolType.Axe:
                    iconLocation = AxeIconLocation;
                    return true;
                case ToolType.Pickaxe:
                    iconLocation = PickaxeIconLocation;
                    return true;
                case ToolType.FishingRod:
                    iconLocation = FishingRodIconLocation;
                    return true;
                default:
                    return false;
            }
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
