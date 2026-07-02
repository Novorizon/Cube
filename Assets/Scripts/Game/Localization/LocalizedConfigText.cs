namespace Game
{
    public static class LocalizedConfigText
    {
        public static string ItemName(int itemId)
        {
            string fallback = itemId.ToString();
            if (DataManager.Instance.Item != null &&
                DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback(GetItemNameKey(itemId), fallback);
        }

        public static string ItemDescription(int itemId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.Item != null &&
                DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Description))
            {
                fallback = config.Description;
            }

            return LocalizationManager.GetOrFallback(GetItemDescriptionKey(itemId), fallback);
        }

        public static string BuildingName(int buildingId)
        {
            string fallback = buildingId.ToString();
            if (DataManager.Instance.WorldBuilding != null &&
                DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"building.{buildingId}.name", fallback);
        }

        public static string BuildingDescription(int buildingId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.WorldBuilding != null &&
                DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Desc))
            {
                fallback = config.Desc;
            }

            return LocalizationManager.GetOrFallback($"building.{buildingId}.desc", fallback);
        }

        public static string TechName(int techId)
        {
            string fallback = techId.ToString();
            if (DataManager.Instance.TechNode != null &&
                DataManager.Instance.TechNode.TryGet(techId, out TechNodeConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"tech.{techId}.name", fallback);
        }

        public static string TechDescription(int techId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.TechNode != null &&
                DataManager.Instance.TechNode.TryGet(techId, out TechNodeConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Desc))
            {
                fallback = config.Desc;
            }

            return LocalizationManager.GetOrFallback($"tech.{techId}.desc", fallback);
        }

        public static string CropName(int cropId)
        {
            string fallback = cropId.ToString();
            if (DataManager.Instance.WorldCrop != null &&
                DataManager.Instance.WorldCrop.TryGet(cropId, out WorldCropConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"crop.{cropId}.name", fallback);
        }

        public static string TowerName(int towerId)
        {
            string fallback = towerId.ToString();
            if (DataManager.Instance.Tower != null &&
                DataManager.Instance.Tower.TryGet(towerId, out TowerConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"tower.{towerId}.name", fallback);
        }

        public static string TowerDescription(int towerId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.Tower != null &&
                DataManager.Instance.Tower.TryGet(towerId, out TowerConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Description))
            {
                fallback = config.Description;
            }

            return LocalizationManager.GetOrFallback($"tower.{towerId}.desc", fallback);
        }

        public static string SkillName(int skillId)
        {
            string fallback = skillId.ToString();
            if (DataManager.Instance.Skill != null &&
                DataManager.Instance.Skill.TryGet(skillId, out SkillConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"skill.{skillId}.name", fallback);
        }

        public static string SkillDescription(int skillId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.Skill != null &&
                DataManager.Instance.Skill.TryGet(skillId, out SkillConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Description))
            {
                fallback = config.Description;
            }

            return LocalizationManager.GetOrFallback($"skill.{skillId}.desc", fallback);
        }

        public static string NpcName(int npcId)
        {
            string fallback = npcId.ToString();
            if (DataManager.Instance.Npc != null &&
                DataManager.Instance.Npc.TryGet(npcId, out NpcConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"npc.{npcId}.name", fallback);
        }

        public static string NpcDescription(int npcId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.Npc != null &&
                DataManager.Instance.Npc.TryGet(npcId, out NpcConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Description))
            {
                fallback = config.Description;
            }

            return LocalizationManager.GetOrFallback($"npc.{npcId}.desc", fallback);
        }

        public static string BaseName(int baseId)
        {
            string fallback = baseId.ToString();
            if (DataManager.Instance.Base != null &&
                DataManager.Instance.Base.TryGet(baseId, out BaseConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"base.{baseId}.name", fallback);
        }

        public static string BaseDescription(int baseId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.Base != null &&
                DataManager.Instance.Base.TryGet(baseId, out BaseConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Description))
            {
                fallback = config.Description;
            }

            return LocalizationManager.GetOrFallback($"base.{baseId}.desc", fallback);
        }

        public static string MapName(int mapId)
        {
            string fallback = mapId.ToString();
            if (DataManager.Instance.Map != null &&
                DataManager.Instance.Map.TryGet(mapId, out MapConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"map.{mapId}.name", fallback);
        }

        public static string MapDescription(int mapId)
        {
            string fallback = string.Empty;
            if (DataManager.Instance.Map != null &&
                DataManager.Instance.Map.TryGet(mapId, out MapConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Description))
            {
                fallback = config.Description;
            }

            return LocalizationManager.GetOrFallback($"map.{mapId}.desc", fallback);
        }

        public static string WorldResourceName(int resourceId)
        {
            string fallback = resourceId.ToString();
            if (DataManager.Instance.WorldResource != null &&
                DataManager.Instance.WorldResource.TryGet(resourceId, out WorldResourceConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"world_resource.{resourceId}.name", fallback);
        }

        public static string WorldGatherName(int gatherId)
        {
            string fallback = gatherId.ToString();
            if (DataManager.Instance.WorldGather != null &&
                DataManager.Instance.WorldGather.TryGet(gatherId, out WorldGatherConfig config) &&
                config != null &&
                !string.IsNullOrWhiteSpace(config.Name))
            {
                fallback = config.Name;
            }

            return LocalizationManager.GetOrFallback($"world_gather.{gatherId}.name", fallback);
        }

        public static string RecipeName(int recipeId, string fallback)
        {
            return LocalizationManager.GetOrFallback($"recipe.{recipeId}.name", fallback);
        }

        private static string GetItemNameKey(int itemId)
        {
            switch (itemId)
            {
                case ItemIds.Wood:
                    return "item.wood";
                case ItemIds.Stone:
                    return "item.stone";
                case ItemIds.Gold:
                    return "item.gold";
                case ItemIds.CopperOre:
                    return "item.copper_ore";
                case ItemIds.IronOre:
                    return "item.iron_ore";
                case ItemIds.Wheat:
                    return "item.wheat";
                case ItemIds.Tomato:
                    return "item.tomato";
                case ItemIds.Herb:
                    return "item.herb";
                case ItemIds.Flower:
                    return "item.flower";
                case ItemIds.WheatSeed:
                    return "item.wheat_seed";
                case ItemIds.TomatoSeed:
                    return "item.tomato_seed";
                case ItemIds.HerbSeed:
                    return "item.herb_seed";
                case ItemIds.FlowerSeed:
                    return "item.flower_seed";
                case ItemIds.Food:
                    return "item.food";
                case ItemIds.Plank:
                    return "item.plank";
                case ItemIds.CopperIngot:
                    return "item.copper_ingot";
                case ItemIds.IronIngot:
                    return "item.iron_ingot";
                case ItemIds.BombTree:
                    return "item.bomb_tree";
                case ItemIds.BombBridge:
                    return "item.bomb_bridge";
                case ItemIds.Blueprint:
                    return "item.blueprint";
                case ItemIds.SlimeCore:
                    return "item.slime_core";
                case ItemIds.BasicAxe:
                    return "item.basic_axe";
                case ItemIds.BasicPickaxe:
                    return "item.basic_pickaxe";
                case ItemIds.BasicHoe:
                    return "item.basic_hoe";
                case ItemIds.BasicWateringCan:
                    return "item.basic_watering_can";
                case ItemIds.BasicFishingRod:
                    return "item.basic_fishing_rod";
                case ItemIds.BasicHammer:
                    return "item.basic_hammer";
                default:
                    return $"item.{itemId}.name";
            }
        }

        private static string GetItemDescriptionKey(int itemId)
        {
            return GetItemNameKey(itemId) + ".desc";
        }
    }
}
