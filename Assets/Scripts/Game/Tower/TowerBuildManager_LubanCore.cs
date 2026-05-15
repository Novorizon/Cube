using Game;
using UnityEngine;

/// <summary>
/// 这是 TowerBuildManager 中和 Luban TowerConfig / ItemManager 接入相关的核心方法示例。
/// 如果你已有 TowerBuildManager，不需要整个替换，只需要把建塔消耗和配置读取逻辑改成这里的方式。
/// </summary>
public partial class TowerBuildManager
{
    private int selectedTowerConfigId;

    public void SelectTower(int towerConfigId)
    {
        selectedTowerConfigId = towerConfigId;

        TowerConfig config = DataManager.Instance.Tower.Get(towerConfigId);

        if (config != null)
        {
            Debug.Log($"Selected tower. id: {towerConfigId}, name: {config.Name}");
        }
    }

    private bool CanBuildSelectedTower(Vector3Int coord)
    {
        if (selectedTowerConfigId <= 0)
        {
            Debug.Log("No tower selected.");
            return false;
        }

        TowerConfig config = DataManager.Instance.Tower.Get(selectedTowerConfigId);

        if (config == null)
        {
            return false;
        }

        if (!ItemManager.Instance.HasItem(config.CostItemId, config.CostCount))
        {
            Debug.Log($"Not enough item to build tower. itemId: {config.CostItemId}, need: {config.CostCount}, current: {ItemManager.Instance.GetCount(config.CostItemId)}");
            return false;
        }

        if (!MapManager.Instance.CanPlaceTower(coord))
        {
            Debug.Log($"Can not place tower on coord: {coord}");
            return false;
        }

        return true;
    }

    private bool ConsumeBuildCost(TowerConfig config)
    {
        if (config == null)
        {
            return false;
        }

        return ItemManager.Instance.TryConsume(config.CostItemId, config.CostCount);
    }

    private void InitializeBuiltTower(Tower tower, int towerConfigId, Vector3Int coord)
    {
        tower.Initialize(towerConfigId, coord);
        TowerManager.Instance.Register(tower);
    }
}
