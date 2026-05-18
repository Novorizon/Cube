using Game.Framework;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class TowerBuildManager : Singleton<TowerBuildManager>
    {
        private int selectedTowerConfigId;
        private TowerConfig selectedTowerConfig;
        private Transform towerRoot;
        private Transform previewRoot;
        private GameObject previewInstance;
        private TowerConfig previewConfig;
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
                return selectedTowerConfigId > 0 && selectedTowerConfig != null;
            }
        }

        public bool Initialize()
        {
            selectedTowerConfigId = 0;
            selectedTowerConfig = null;
            previewConfig = null;
            hasPreviewCoord = false;
            previewCanBuild = false;

            EnsureTowerRoot();
            EnsurePreviewRoot();

            return true;
        }

        public void SelectTower(int towerConfigId)
        {
            if (!DataManager.Instance.Tower.TryGet(towerConfigId, out TowerConfig config))
            {
                Debug.LogWarning($"Select tower failed. Missing tower config: {towerConfigId}");
                return;
            }

            if (string.IsNullOrEmpty(config.PrefabLocation))
            {
                Debug.LogWarning($"Select tower failed. Missing tower prefab location: {towerConfigId}");
                return;
            }

            selectedTowerConfigId = towerConfigId;
            selectedTowerConfig = config;

            CreatePreview(config);
        }

        public void CancelSelect()
        {
            selectedTowerConfigId = 0;
            selectedTowerConfig = null;
            previewConfig = null;
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
                CreatePreview(selectedTowerConfig);

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

            if (string.IsNullOrEmpty(config.PrefabLocation))
            {
                Debug.LogWarning($"Build tower failed. Missing tower prefab location: {towerConfigId}");
                return false;
            }

            if (!MapManager.Instance.CanPlaceTower(coord))
            {
                Debug.Log($"Build tower failed. Tile is not buildable: {coord}");
                return false;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(config.PrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Build tower failed. Load prefab failed. towerConfigId: {towerConfigId}, location: {config.PrefabLocation}");
                return false;
            }

            Vector3 position = GetTowerWorldPosition(coord);
            GameObject instance = GameObject.Instantiate(prefab, position, Quaternion.identity, towerRoot);
            instance.name = $"Tower_{towerConfigId}_{coord.x}_{coord.y}_{coord.z}";

            Tower tower = instance.GetComponent<Tower>();

            if (tower == null)
            {
                tower = instance.AddComponent<Tower>();
            }

            tower.Initialize(config.Id, coord);

            bool placed = MapManager.Instance.TryPlaceTower(coord, tower);

            if (!placed)
            {
                GameObject.Destroy(instance);
                return false;
            }

            TowerManager.Instance.Register(tower);
            ItemManager.Instance.TryConsume(config.CostItemId, config.CostCount);

            return true;
        }

        private void CreatePreview(TowerConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.PrefabLocation))
            {
                return;
            }

            if (previewInstance != null && previewConfig == config)
            {
                previewInstance.SetActive(false);
                return;
            }

            DestroyPreview();

            GameObject prefab = ResourceManager.Instance.LoadGameObject(config.PrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Create tower preview failed. Load prefab failed. towerConfigId: {config.Id}, location: {config.PrefabLocation}");
                return;
            }

            previewConfig = config;
            previewInstance = GameObject.Instantiate(prefab, previewRoot);
            previewInstance.name = $"Tower_{config.Id}_Preview";
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
