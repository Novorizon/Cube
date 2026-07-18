using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class BuildingPreview
    {
        private readonly PlacementMaterials materials;
        private GameObject instance;
        private int buildingId;
        private bool missingPrefabLogged;

        public BuildingPreview(PlacementMaterials materials)
        {
            this.materials = materials;
        }

        public void Show(int selectedBuildingId, Vector3Int coord)
        {
            Ensure(selectedBuildingId);
            if (instance == null ||
                !DataManager.Instance.WorldBuilding.TryGet(selectedBuildingId, out WorldBuildingConfig config) ||
                config == null)
            {
                Hide();
                return;
            }

            bool canPlace = CanPlace(selectedBuildingId, coord, config);
            int sizeX = WorldBuildingFootprint.GetSizeX(config);
            int sizeZ = WorldBuildingFootprint.GetSizeZ(config);
            instance.transform.position = WorldBuildingFootprint.GetCenterWorldPosition(
                coord,
                sizeX,
                sizeZ,
                MapManager.Instance.TileSize) + Vector3.up * MapManager.Instance.TileSize;

            Material material = canPlace ? materials.Valid : materials.Invalid;
            if (material == null)
            {
                Hide();
                return;
            }

            SetVisible(true);
            PlacementVisualUtility.ApplyMaterial(instance, material);
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void Clear()
        {
            if (instance != null)
            {
                Object.Destroy(instance);
            }

            instance = null;
            buildingId = 0;
            missingPrefabLogged = false;
        }

        private void Ensure(int selectedBuildingId)
        {
            if (buildingId == selectedBuildingId && (instance != null || missingPrefabLogged))
            {
                return;
            }

            Clear();
            buildingId = selectedBuildingId;
            if (!DataManager.Instance.WorldBuilding.TryGet(selectedBuildingId, out WorldBuildingConfig config) || config == null)
            {
                LogMissingPrefab(config);
                return;
            }

            string prefabLocation = WorldBuildingManager.GetPrefabLocation(config);
            if (string.IsNullOrWhiteSpace(prefabLocation))
            {
                LogMissingPrefab(config);
                return;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(prefabLocation);
            if (prefab == null)
            {
                LogMissingPrefab(config);
                return;
            }

            instance = Object.Instantiate(prefab);
            instance.name = $"BuildingPreview_{selectedBuildingId}";
            PlacementVisualUtility.RemoveColliders(instance);
            SetVisible(false);
        }

        private void LogMissingPrefab(WorldBuildingConfig config)
        {
            if (missingPrefabLogged)
            {
                return;
            }

            string location = config != null ? WorldBuildingManager.GetPrefabLocation(config) : string.Empty;
            Debug.LogError($"Missing building preview prefab. buildingId: {buildingId}, location: {location}");
            missingPrefabLogged = true;
        }

        private void SetVisible(bool visible)
        {
            if (instance != null && instance.activeSelf != visible)
            {
                instance.SetActive(visible);
            }
        }

        private static bool CanPlace(int selectedBuildingId, Vector3Int coord, WorldBuildingConfig config)
        {
            return config.Enable &&
                   WorldBuildingManager.Instance.IsBuildingUnlocked(selectedBuildingId) &&
                   MapManager.Instance.CanPlaceMapObject(
                       coord,
                       WorldBuildingFootprint.GetSizeX(config),
                       WorldBuildingFootprint.GetSizeZ(config));
        }
    }

    public sealed class PlacementMaterials
    {
        private const string ValidMaterialPath = "Assets/Arts/Map/Buildings/Materials/Placement_Valid.mat";
        private const string InvalidMaterialPath = "Assets/Arts/Map/Buildings/Materials/Placement_Invalid.mat";

        private Material valid;
        private Material invalid;
        private bool missingValidLogged;
        private bool missingInvalidLogged;

        public Material Valid => valid != null ? valid : valid = Load(ValidMaterialPath, "valid", ref missingValidLogged);
        public Material Invalid => invalid != null ? invalid : invalid = Load(InvalidMaterialPath, "invalid", ref missingInvalidLogged);

        private static Material Load(string path, string label, ref bool missingLogged)
        {
            Material material = ResourceManager.Instance.LoadAsset<Material>(path);
            if (material == null && !missingLogged)
            {
                Debug.LogError($"Missing placement {label} material: {path}");
                missingLogged = true;
            }

            return material;
        }
    }

    internal static class PlacementVisualUtility
    {
        public static void ApplyMaterial(GameObject root, Material material)
        {
            if (root == null || material == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sharedMaterial = material;
                }
            }
        }

        public static void RemoveColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                if (colliders[i] != null)
                {
                    Object.Destroy(colliders[i]);
                }
            }
        }
    }
}
