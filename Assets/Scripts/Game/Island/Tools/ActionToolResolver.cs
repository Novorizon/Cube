namespace Game
{
    public static class ActionToolResolver
    {
        public static ToolType GetRequiredTool(ToolKitActionType actionType)
        {
            switch (actionType)
            {
                case ToolKitActionType.GatherTree:
                    return ToolType.Axe;

                case ToolKitActionType.GatherStone:
                case ToolKitActionType.GatherOre:
                case ToolKitActionType.BuildMine:
                    return ToolType.Pickaxe;

                case ToolKitActionType.CultivateFarm:
                    return ToolType.Hoe;

                case ToolKitActionType.WaterCrop:
                    return ToolType.WateringCan;

                case ToolKitActionType.Fish:
                    return ToolType.FishingRod;

                case ToolKitActionType.Remove:
                    return ToolType.Hammer;

                default:
                    return ToolType.None;
            }
        }

        public static ToolKitActionType GetGatherAction(WorldResourceCategory category)
        {
            switch (category)
            {
                case WorldResourceCategory.Tree:
                    return ToolKitActionType.GatherTree;

                case WorldResourceCategory.Stone:
                    return ToolKitActionType.GatherStone;

                case WorldResourceCategory.Ore:
                    return ToolKitActionType.GatherOre;

                default:
                    return ToolKitActionType.None;
            }
        }
    }
}
