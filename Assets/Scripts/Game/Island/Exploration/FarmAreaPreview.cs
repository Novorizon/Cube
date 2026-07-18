using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class FarmAreaPreview
    {
        private readonly PlacementMaterials materials;
        private readonly List<GameObject> views = new List<GameObject>();
        private GameObject root;
        private GameObject prefab;
        private bool missingPrefabLogged;

        public FarmAreaPreview(PlacementMaterials materials)
        {
            this.materials = materials;
        }

        public void Show(Vector3Int a, Vector3Int b)
        {
            EnsureRoot();
            EnsurePrefab();
            Material validMaterial = materials.Valid;
            Material invalidMaterial = materials.Invalid;
            if (root == null || prefab == null || validMaterial == null || invalidMaterial == null)
            {
                Hide();
                return;
            }

            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minZ = Mathf.Min(a.z, b.z);
            int maxZ = Mathf.Max(a.z, b.z);
            int neededCount = Mathf.Max(1, (maxX - minX + 1) * (maxZ - minZ + 1));
            EnsureViewCount(neededCount);

            int index = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3Int coord = new Vector3Int(x, a.y, z);
                    GameObject view = views[index++];
                    view.SetActive(true);
                    view.transform.position = MapManager.Instance.GetTileWorldPosition(coord) +
                                              Vector3.up * (MapManager.Instance.TileSize * 1.03f);
                    view.transform.rotation = Quaternion.identity;
                    view.transform.localScale = Vector3.one * MapManager.Instance.TileSize;
                    PlacementVisualUtility.ApplyMaterial(
                        view,
                        MapManager.Instance.CanPlaceMapObject(coord) ? validMaterial : invalidMaterial);
                }
            }

            for (int i = index; i < views.Count; i++)
            {
                if (views[i] != null)
                {
                    views[i].SetActive(false);
                }
            }
        }

        public void Hide()
        {
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] != null)
                {
                    views[i].SetActive(false);
                }
            }
        }

        public void Clear()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }

            root = null;
            prefab = null;
            views.Clear();
            missingPrefabLogged = false;
        }

        private void EnsureRoot()
        {
            if (root == null)
            {
                root = new GameObject("FarmAreaPreview");
            }
        }

        private void EnsurePrefab()
        {
            if (prefab != null || missingPrefabLogged)
            {
                return;
            }

            prefab = ResourceManager.Instance.LoadGameObject(FarmManager.FarmPlotPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing farm area preview prefab: {FarmManager.FarmPlotPrefabPath}");
                missingPrefabLogged = true;
            }
        }

        private void EnsureViewCount(int count)
        {
            while (views.Count < count)
            {
                GameObject view = Object.Instantiate(prefab, root.transform);
                view.name = $"FarmAreaPreview_{views.Count}";
                PlacementVisualUtility.RemoveColliders(view);
                views.Add(view);
            }
        }
    }
}
