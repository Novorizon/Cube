using Game.Framework;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class TowerBuildManager : Singleton<TowerBuildManager>
    {
        private int selectedTowerConfigId;
        private TowerConfig selectedTowerConfig;
        private TowerLevelConfig selectedTowerLevelConfig;
        private Transform towerRoot;
        private Transform previewRoot;
        private GameObject previewInstance;
        private TowerConfig previewConfig;
        private TowerLevelConfig previewLevelConfig;
        private Vector3Int previewCoord;
        private bool hasPreviewCoord;
        private bool previewCanBuild;

        private readonly Color canBuildPreviewColor = new Color(1f, 1f, 1f, 0.75f);
        private readonly Color cannotBuildPreviewColor = new Color(1f, 0.2f, 0.2f, 0.75f);

        public int SelectedTowerConfigId
        {
            get
            {
                return selectedTowerConfigId;
            }
        }

        public TowerType SelectedTowerType
        {
            get
            {
                if (selectedTowerConfig == null)
                {
                    return TowerType.Normal;
                }

                return (TowerType)selectedTowerConfig.TowerType;
            }
        }

        public bool HasSelectedTower
        {
            get
            {
                return selectedTowerConfigId > 0 && selectedTowerConfig != null && selectedTowerLevelConfig != null;
            }
        }

        public bool Initialize()
        {
            selectedTowerConfigId = 0;
            selectedTowerConfig = null;
            selectedTowerLevelConfig = null;
            previewConfig = null;
            previewLevelConfig = null;
            hasPreviewCoord = false;
            previewCanBuild = false;

            EnsureTowerRoot();
            EnsurePreviewRoot();

            return true;
        }

        public void Clear()
        {
            CancelSelect();
            DestroyPreview();

            if (towerRoot == null)
            {
                EnsureTowerRoot();
            }

            if (towerRoot == null)
            {
                return;
            }

            for (int i = towerRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = towerRoot.GetChild(i);
                if (child != null)
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
        }

        public void SelectTower(int towerConfigId)
        {
            if (!DataManager.Instance.Tower.TryGet(towerConfigId, out TowerConfig config))
            {
                Debug.LogWarning($"Select tower failed. Missing tower config: {towerConfigId}");
                return;
            }

            if (!DataManager.Instance.TryGetTowerLevel(towerConfigId, 1, out TowerLevelConfig levelConfig))
            {
                Debug.LogWarning($"Select tower failed. Missing tower level config: {towerConfigId}, level: 1");
                return;
            }

            if (string.IsNullOrEmpty(levelConfig.PrefabLocation))
            {
                Debug.LogWarning($"Select tower failed. Missing tower prefab location: {towerConfigId}, level: 1");
                return;
            }

            selectedTowerConfigId = towerConfigId;
            selectedTowerConfig = config;
            selectedTowerLevelConfig = levelConfig;

            CreatePreview(config, levelConfig);
        }

        public void CancelSelect()
        {
            selectedTowerConfigId = 0;
            selectedTowerConfig = null;
            selectedTowerLevelConfig = null;
            previewConfig = null;
            previewLevelConfig = null;
            hasPreviewCoord = false;
            previewCanBuild = false;

            HidePreview();
        }

        public void UpdatePreview(TileView tileView)
        {
            if (!HasSelectedTower)
            {
                HidePreview();
                return;
            }

            if (tileView == null)
            {
                hasPreviewCoord = false;
                previewCanBuild = false;
                HidePreview();
                return;
            }

            if (previewInstance == null)
            {
                CreatePreview(selectedTowerConfig, selectedTowerLevelConfig);

                if (previewInstance == null)
                {
                    HidePreview();
                    return;
                }
            }

            Vector3Int coord = tileView.Coord;
            hasPreviewCoord = true;
            previewCoord = coord;
            previewCanBuild = MapManager.Instance.CanPlaceTower(coord);

            Vector3 position = GetTowerWorldPosition(coord);
            previewInstance.transform.position = position;
            previewInstance.transform.rotation = Quaternion.identity;

            if (!previewInstance.activeSelf)
            {
                previewInstance.SetActive(true);
            }

            Color color = previewCanBuild ? canBuildPreviewColor : cannotBuildPreviewColor;
            ApplyPreviewColor(previewInstance, color);
        }

        public bool TryBuildPreviewTower()
        {
            if (!HasSelectedTower)
            {
                return false;
            }

            if (!hasPreviewCoord)
            {
                return false;
            }

            if (!previewCanBuild)
            {
                Debug.Log($"Build tower failed. Preview coord is not buildable: {previewCoord}");
                Toast.Warning("该地块不可建造");
                return false;
            }

            if (!TowerManager.Instance.HasGold(selectedTowerConfigId))
            {
                Toast.Warning("金币不足");
                return false;
            }

            return TryBuildTower(previewCoord, selectedTowerConfigId);
        }

        public bool TryBuildTower(Vector3Int coord, int towerConfigId)
        {
            if (!DataManager.Instance.Tower.TryGet(towerConfigId, out TowerConfig config))
            {
                Debug.LogWarning($"Build tower failed. Missing tower config: {towerConfigId}");
                return false;
            }

            if (!DataManager.Instance.TryGetTowerLevel(towerConfigId, 1, out TowerLevelConfig levelConfig))
            {
                Debug.LogWarning($"Build tower failed. Missing tower level config: {towerConfigId}, level: 1");
                return false;
            }

            if (string.IsNullOrEmpty(levelConfig.PrefabLocation))
            {
                Debug.LogWarning($"Build tower failed. Missing tower prefab location: {towerConfigId}, level: 1");
                return false;
            }

            if (!MapManager.Instance.CanPlaceTower(coord))
            {
                Debug.Log($"Build tower failed. Tile is not buildable: {coord}");
                return false;
            }

            int costItemId = GetCostItemId(levelConfig.CostItemId);
            if (!ItemManager.Instance.TryConsume(costItemId, levelConfig.BuildCost))
            {
                Toast.Warning("金币不足");
                return false;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(levelConfig.PrefabLocation);

            if (prefab == null)
            {
                ItemManager.Instance.AddItem(costItemId, levelConfig.BuildCost);
                Debug.LogWarning($"Build tower failed. Load prefab failed. towerConfigId: {towerConfigId}, location: {levelConfig.PrefabLocation}");
                return false;
            }

            Vector3 position = GetTowerWorldPosition(coord);
            GameObject instance = GameObject.Instantiate(prefab, position, Quaternion.identity, towerRoot);
            instance.name = $"Tower_{towerConfigId}_{levelConfig.Level}_{coord.x}_{coord.y}_{coord.z}";

            Tower tower = instance.GetComponent<Tower>();

            if (tower == null)
            {
                tower = instance.AddComponent<Tower>();
            }

            tower.Initialize(config.Id, levelConfig.Level, coord);

            bool placed = MapManager.Instance.TryPlaceTower(coord, tower);

            if (!placed)
            {
                ItemManager.Instance.AddItem(costItemId, levelConfig.BuildCost);
                GameObject.Destroy(instance);
                return false;
            }

            TowerRotateView rotateView = tower.gameObject.AddComponent<TowerRotateView>();
            rotateView.StartRotate();

            TowerManager.Instance.Register(tower);
            return true;
        }

        public bool TryUpgradeTower(Tower tower)
        {
            if (tower == null)
            {
                return false;
            }

            if (!DataManager.Instance.TryGetNextTowerLevel(tower, out TowerLevelConfig nextLevelConfig))
            {
                Toast.Info("已达最高等级");
                return false;
            }

            int costItemId = GetCostItemId(nextLevelConfig.UpgradeCostItemId);
            if (!ItemManager.Instance.TryConsume(costItemId, nextLevelConfig.UpgradeCost))
            {
                Toast.Warning("金币不足");
                return false;
            }

            if (ApplyTowerLevel(tower, nextLevelConfig))
            {
                Toast.Info($"升级成功 Lv {nextLevelConfig.Level}");
                return true;
            }

            ItemManager.Instance.AddItem(costItemId, nextLevelConfig.UpgradeCost);
            return false;
        }

        public bool TrySellTower(Tower tower, out int sellItemId, out int sellCount)
        {
            sellItemId = ItemIds.Gold;
            sellCount = 0;

            if (tower == null)
            {
                return false;
            }

            TowerLevelConfig currentLevelConfig = DataManager.Instance.GetTowerLevel(tower.ConfigId, tower.Level);
            if (currentLevelConfig == null)
            {
                Toast.Warning("出售失败：塔等级配置缺失");
                return false;
            }

            sellItemId = GetCostItemId(currentLevelConfig.CostItemId);
            sellCount = CalculateSellCount(tower);

            if (!MapManager.Instance.RemoveTower(tower.Coord))
            {
                Toast.Warning("出售失败：地块状态异常");
                return false;
            }

            TowerManager.Instance.Unregister(tower);
            GameObject.Destroy(tower.gameObject);

            if (sellCount > 0)
            {
                ItemManager.Instance.AddItem(sellItemId, sellCount);
            }

            return true;
        }

        public int CalculateSellCount(Tower tower)
        {
            if (tower == null)
            {
                return 0;
            }

            TowerLevelConfig currentLevelConfig = DataManager.Instance.GetTowerLevel(tower.ConfigId, tower.Level);
            if (currentLevelConfig == null)
            {
                return 0;
            }

            int totalCost = 0;
            for (int level = 1; level <= tower.Level; level++)
            {
                if (!DataManager.Instance.TryGetTowerLevel(tower.ConfigId, level, out TowerLevelConfig levelConfig))
                {
                    continue;
                }

                totalCost += level == 1 ? levelConfig.BuildCost : levelConfig.UpgradeCost;
            }

            return Mathf.RoundToInt(totalCost * currentLevelConfig.SellGoldRate);
        }

        private bool ApplyTowerLevel(Tower tower, TowerLevelConfig nextLevelConfig)
        {
            TowerLevelConfig currentLevelConfig = DataManager.Instance.GetTowerLevel(tower.ConfigId, tower.Level);
            if (currentLevelConfig == null)
            {
                return false;
            }

            if (currentLevelConfig.PrefabLocation == nextLevelConfig.PrefabLocation)
            {
                tower.SetLevel(nextLevelConfig.Level);
                return true;
            }

            return ReplaceTowerObject(tower, nextLevelConfig);
        }

        private bool ReplaceTowerObject(Tower oldTower, TowerLevelConfig nextLevelConfig)
        {
            GameObject prefab = ResourceManager.Instance.LoadGameObject(nextLevelConfig.PrefabLocation);
            if (prefab == null)
            {
                Debug.LogWarning($"Upgrade tower failed. Load prefab failed. towerConfigId: {nextLevelConfig.TowerId}, level: {nextLevelConfig.Level}, location: {nextLevelConfig.PrefabLocation}");
                Toast.Warning("升级失败：模型资源缺失");
                return false;
            }

            Vector3Int coord = oldTower.Coord;
            Vector3 position = oldTower.transform.position;
            Quaternion rotation = oldTower.transform.rotation;

            GameObject instance = GameObject.Instantiate(prefab, position, rotation, towerRoot);
            instance.name = $"Tower_{nextLevelConfig.TowerId}_{nextLevelConfig.Level}_{coord.x}_{coord.y}_{coord.z}";

            Tower newTower = instance.GetComponent<Tower>();
            if (newTower == null)
            {
                newTower = instance.AddComponent<Tower>();
            }

            newTower.Initialize(nextLevelConfig.TowerId, nextLevelConfig.Level, coord);

            // Replacement is only needed when a level swaps prefab; the tile occupancy moves atomically here.
            if (!MapManager.Instance.RemoveTower(coord))
            {
                GameObject.Destroy(instance);
                Toast.Warning("升级失败：地块状态异常");
                return false;
            }

            TowerManager.Instance.Unregister(oldTower);
            GameObject.Destroy(oldTower.gameObject);

            if (!MapManager.Instance.TryPlaceTower(coord, newTower))
            {
                TowerManager.Instance.Unregister(newTower);
                GameObject.Destroy(instance);
                Toast.Warning("升级失败：地块状态异常");
                return false;
            }

            TowerRotateView rotateView = newTower.gameObject.AddComponent<TowerRotateView>();
            rotateView.StartRotate();
            TowerManager.Instance.Register(newTower);
            return true;
        }

        private void CreatePreview(TowerConfig config, TowerLevelConfig levelConfig)
        {
            if (config == null || levelConfig == null || string.IsNullOrEmpty(levelConfig.PrefabLocation))
            {
                return;
            }

            if (previewInstance != null && previewConfig == config && previewLevelConfig == levelConfig)
            {
                previewInstance.SetActive(false);
                return;
            }

            DestroyPreview();

            GameObject prefab = ResourceManager.Instance.LoadGameObject(levelConfig.PrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Create tower preview failed. Load prefab failed. towerConfigId: {config.Id}, location: {levelConfig.PrefabLocation}");
                return;
            }

            previewConfig = config;
            previewLevelConfig = levelConfig;
            previewInstance = GameObject.Instantiate(prefab, previewRoot);
            previewInstance.name = $"Tower_{config.Id}_{levelConfig.Level}_Preview";
            previewInstance.SetActive(false);

            PreparePreviewObject(previewInstance);
        }

        private void DestroyPreview()
        {
            if (previewInstance == null)
            {
                return;
            }

            GameObject.Destroy(previewInstance);
            previewInstance = null;
            previewConfig = null;
            previewLevelConfig = null;
        }

        private void HidePreview()
        {
            if (previewInstance == null)
            {
                return;
            }

            previewInstance.SetActive(false);
        }

        private void PreparePreviewObject(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            SetLayerRecursively(instance.transform, LayerMask.NameToLayer("Ignore Raycast"));

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Rigidbody[] rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].isKinematic = true;
                rigidbodies[i].detectCollisions = false;
            }

            Tower[] towers = instance.GetComponentsInChildren<Tower>(true);

            for (int i = 0; i < towers.Length; i++)
            {
                towers[i].enabled = false;
            }
        }

        private void SetLayerRecursively(Transform target, int layer)
        {
            if (target == null)
            {
                return;
            }

            if (layer >= 0)
            {
                target.gameObject.layer = layer;
            }

            for (int i = 0; i < target.childCount; i++)
            {
                SetLayerRecursively(target.GetChild(i), layer);
            }
        }

        private void ApplyPreviewColor(GameObject instance, Color color)
        {
            if (instance == null)
            {
                return;
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);

                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);

                renderer.SetPropertyBlock(block);
            }
        }

        private Vector3 GetTowerWorldPosition(Vector3Int coord)
        {
            Vector3 tilePosition = MapManager.Instance.GetTileWorldPosition(coord);
            float tileSize = MapManager.Instance.TileSize;

            return tilePosition + Vector3.up * tileSize;
        }

        private int GetCostItemId(int itemId)
        {
            return itemId > 0 ? itemId : ItemIds.Gold;
        }

        private void EnsureTowerRoot()
        {
            GameObject rootObject = GameObject.Find("TowerRoot");

            if (rootObject == null)
            {
                rootObject = new GameObject("TowerRoot");
                rootObject.transform.position = Vector3.zero;
            }

            towerRoot = rootObject.transform;
        }

        private void EnsurePreviewRoot()
        {
            GameObject rootObject = GameObject.Find("TowerPreviewRoot");

            if (rootObject == null)
            {
                rootObject = new GameObject("TowerPreviewRoot");
                rootObject.transform.position = Vector3.zero;
            }

            previewRoot = rootObject.transform;
        }
    }
}
