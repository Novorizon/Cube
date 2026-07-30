namespace Game
{
    /// <summary>
    /// Owns farm-specific prerequisite rules while returning the shared result format.
    /// Checks are side-effect free and are reused by entry UI and authoritative execution paths.
    /// </summary>
    public static class FarmRequirementChecker
    {
        public static RequirementResult CheckCanEnterCultivation(out int hoeItemId)
        {
            hoeItemId = 0;

            if (MapManager.Instance.CurrentMap == null)
            {
                return Failure(
                    "farm.map_not_ready",
                    "ui.farm.requirement.map_not_ready",
                    "地图尚未准备完成。",
                    "The map is not ready yet.");
            }

            if (!WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.House))
            {
                return Failure(
                    "farm.missing_house",
                    "ui.farm.requirement.missing_house",
                    "需要先建造并完成一座房屋。",
                    "Build and complete a house first.");
            }

            if (ToolKitManager.Instance.TryFindTool(ToolType.Hoe, out hoeItemId))
            {
                return RequirementResult.Success();
            }

            if (ToolKitManager.Instance.TryFindOwnedTool(ToolType.Hoe, out int ownedHoeItemId))
            {
                return RequirementResult.Failure(
                    "farm.hoe_not_in_toolkit",
                    "ui.farm.requirement.hoe_not_in_toolkit",
                    "请先将{0}放入工具箱。",
                    "Put {0} in the toolkit first.",
                    LocalizedConfigText.ItemName(ownedHoeItemId));
            }

            return Failure(
                "farm.missing_hoe",
                "ui.farm.requirement.missing_hoe",
                "需要先获得一把锄头。",
                "Obtain a hoe first.");
        }

        public static RequirementResult CheckCanPlant(Farm farm, int cropId)
        {
            if (farm == null)
            {
                return Failure(
                    "farm.no_selected_farm",
                    "ui.farm.requirement.no_selected_farm",
                    "请先选择一块农田。",
                    "Select a farm first.");
            }

            if (farm.CellCount <= 0)
            {
                return Failure(
                    "farm.empty_area",
                    "ui.farm.requirement.empty_area",
                    "这块农田没有可用地块。",
                    "This farm has no available plots.");
            }

            if (farm.HasCrop)
            {
                return Failure(
                    "farm.already_planted",
                    "ui.farm.requirement.already_planted",
                    "这块农田已经种植了作物。",
                    "This farm already has a crop.");
            }

            if (!FarmManager.Instance.Crops.TryGetValue(cropId, out WorldCropDefinition crop) || crop == null)
            {
                return Failure(
                    "farm.missing_crop_config",
                    "ui.farm.requirement.missing_crop_config",
                    "作物配置不存在或尚未启用。",
                    "The crop is missing or disabled.");
            }

            int requiredCount = GetRequiredSeedCount(farm, crop);
            int currentCount = crop.SeedItemId > 0
                ? ItemManager.Instance.GetCount(crop.SeedItemId)
                : 0;
            if (crop.SeedItemId > 0 && requiredCount > 0 && currentCount < requiredCount)
            {
                return RequirementResult.Failure(
                    "farm.not_enough_seed",
                    "ui.farm.requirement.not_enough_seed",
                    "{0}不足：需要{1}，当前有{2}。",
                    "Not enough {0}: need {1}, have {2}.",
                    LocalizedConfigText.ItemName(crop.SeedItemId),
                    requiredCount,
                    currentCount);
            }

            return RequirementResult.Success();
        }

        public static RequirementResult DragRequired()
        {
            return Failure(
                "farm.drag_required",
                "ui.farm.requirement.drag_required",
                "请按住鼠标并拖拽，划出农田区域。",
                "Hold and drag to mark out a farm area.");
        }

        public static RequirementResult InvalidDrag()
        {
            return Failure(
                "farm.invalid_drag",
                "ui.farm.requirement.invalid_drag",
                "拖拽起点或终点不在有效地图格上。",
                "The drag start or end is not on a valid tile.");
        }

        public static RequirementResult BuildingModeActive()
        {
            return Failure(
                "farm.building_mode_active",
                "ui.farm.requirement.building_mode_active",
                "请先退出建筑放置模式。",
                "Exit building placement mode first.");
        }

        public static RequirementResult ActionUnavailable()
        {
            return Failure(
                "farm.action_unavailable",
                "ui.farm.requirement.action_unavailable",
                "当前无法执行锄地动作，请检查角色动画和锄头资源。",
                "Cultivation cannot start. Check the character animation and hoe asset.");
        }

        public static RequirementResult NoBuildableCells()
        {
            return Failure(
                "farm.no_buildable_cells",
                "ui.farm.requirement.no_buildable_cells",
                "所选区域没有可开垦的地块。",
                "The selected area has no cultivatable tiles.");
        }

        public static RequirementResult GameplayUnavailable()
        {
            return Failure(
                "farm.gameplay_unavailable",
                "ui.farm.requirement.gameplay_unavailable",
                "经营地图控制器尚未准备完成。",
                "The world gameplay controller is not ready.");
        }

        private static int GetRequiredSeedCount(Farm farm, WorldCropDefinition crop)
        {
            if (farm == null || crop == null || crop.SeedItemId <= 0)
            {
                return 0;
            }

            int costPerCell = UnityEngine.Mathf.Max(1, crop.SeedCost);
            return costPerCell * farm.CellCount;
        }

        private static RequirementResult Failure(
            string code,
            string localizationKey,
            string fallbackZhCn,
            string fallbackEn)
        {
            return RequirementResult.Failure(
                code,
                localizationKey,
                fallbackZhCn,
                fallbackEn);
        }
    }
}
