using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class TowerBuildManager : Singleton<TowerBuildManager>
    {
        private const string TowerConfigPath = "Assets/Data/Cube/Configs/TowerConfig.asset";

        private TowerConfig towerConfig;
        private TowerType selectedTowerType = TowerType.None;
        private Transform towerRoot;

        public TowerType SelectedTowerType
        {
            get
            {
                return selectedTowerType;
            }
        }

        public bool HasSelectedTower
        {
            get
            {
                return selectedTowerType != TowerType.None;
            }
        }

        public bool Initialize()
        {
            towerConfig = ResourceManager.Instance.LoadAsset<TowerConfig>(TowerConfigPath);

            if (towerConfig == null)
            {
                Debug.LogError($"TowerBuildManager initialize failed. Missing config: {TowerConfigPath}");
                return false;
            }

            EnsureTowerRoot();
            return true;
        }

        public void SelectTower(TowerType towerType)
        {
            if (towerType == TowerType.None)
            {
                CancelSelect();
                return;
            }

            TowerConfigItem item = towerConfig.GetItem(towerType);

            if (item == null)
            {
                Debug.LogWarning($"Select tower failed. Missing tower config: {towerType}");
                return;
            }

            selectedTowerType = towerType;
            Debug.Log($"Selected tower: {towerType}");
        }

        public void CancelSelect()
        {
            selectedTowerType = TowerType.None;
        }

        public bool TryBuildSelectedTower(TileView tileView)
        {
            if (tileView == null)
            {
                return false;
            }

            if (!HasSelectedTower)
            {
                return false;
            }

            return TryBuildTower(tileView.Coord, selectedTowerType);
        }

        public bool TryBuildTower(Vector3Int coord, TowerType towerType)
        {
            if (towerType == TowerType.None)
            {
                return false;
            }

            if (towerConfig == null)
            {
                Debug.LogError("Build tower failed. TowerConfig is null.");
                return false;
            }

            TowerConfigItem item = towerConfig.GetItem(towerType);

            if (item == null)
            {
                Debug.LogWarning($"Build tower failed. Missing tower config: {towerType}");
                return false;
            }

            if (item.Prefab == null)
            {
                Debug.LogWarning($"Build tower failed. Missing tower prefab: {towerType}");
                return false;
            }

            if (!MapManager.Instance.CanPlaceTower(coord))
            {
                Debug.Log($"Build tower failed. Tile is not buildable: {coord}");
                return false;
            }

            Vector3 position = GetTowerWorldPosition(coord);
            GameObject instance = GameObject.Instantiate(item.Prefab, position, Quaternion.identity, towerRoot);
            instance.name = $"{towerType}_Tower_{coord.x}_{coord.y}_{coord.z}";

            Tower tower = instance.GetComponent<Tower>();

            if (tower == null)
            {
                tower = instance.AddComponent<Tower>();
            }

            tower.Initialize(item, coord);

            bool placed = MapManager.Instance.TryPlaceTower(coord, tower);

            if (!placed)
            {
                GameObject.Destroy(instance);
                return false;
            }

            return true;
        }

        private Vector3 GetTowerWorldPosition(Vector3Int coord)
        {
            Vector3 tilePosition = MapManager.Instance.GetTileWorldPosition(coord);
            float tileSize = MapManager.Instance.TileSize;

            return tilePosition + Vector3.up * tileSize;
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
    }
}
