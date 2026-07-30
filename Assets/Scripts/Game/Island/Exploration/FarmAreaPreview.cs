using Game.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    public sealed class FarmAreaPreview
    {
        private readonly PlacementMaterials materials;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> validTriangles = new List<int>();
        private readonly List<int> invalidTriangles = new List<int>();

        private GameObject root;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Material validMaterial;
        private Material invalidMaterial;
        private bool materialsAssigned;
        private Vector3Int lastMinCoord;
        private Vector3Int lastMaxCoord;
        private float lastTileSize;
        private bool hasLastArea;

        public FarmAreaPreview(PlacementMaterials materials)
        {
            this.materials = materials;
        }

        public void Show(Vector3Int a, Vector3Int b)
        {
            EnsureRoot();
            EnsureMaterials();
            if (root == null || mesh == null || validMaterial == null || invalidMaterial == null)
            {
                Hide();
                return;
            }

            Vector3Int minCoord = new Vector3Int(
                Mathf.Min(a.x, b.x),
                a.y,
                Mathf.Min(a.z, b.z));
            Vector3Int maxCoord = new Vector3Int(
                Mathf.Max(a.x, b.x),
                a.y,
                Mathf.Max(a.z, b.z));
            float tileSize = MapManager.Instance.TileSize;

            root.SetActive(true);
            if (hasLastArea &&
                minCoord == lastMinCoord &&
                maxCoord == lastMaxCoord &&
                Mathf.Approximately(tileSize, lastTileSize))
            {
                return;
            }

            BuildMesh(minCoord, maxCoord, tileSize);
            lastMinCoord = minCoord;
            lastMaxCoord = maxCoord;
            lastTileSize = tileSize;
            hasLastArea = true;
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Clear()
        {
            if (mesh != null)
            {
                Object.Destroy(mesh);
            }

            if (validMaterial != null)
            {
                Object.Destroy(validMaterial);
            }

            if (invalidMaterial != null)
            {
                Object.Destroy(invalidMaterial);
            }

            if (root != null)
            {
                Object.Destroy(root);
            }

            root = null;
            meshFilter = null;
            meshRenderer = null;
            mesh = null;
            validMaterial = null;
            invalidMaterial = null;
            materialsAssigned = false;
            hasLastArea = false;
            ClearMeshData();
        }

        private void EnsureRoot()
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("FarmAreaPreview");
            meshFilter = root.AddComponent<MeshFilter>();
            meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            mesh = new Mesh
            {
                name = "FarmAreaPreviewMesh",
            };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }

        private void EnsureMaterials()
        {
            if (validMaterial == null)
            {
                validMaterial = CreatePreviewMaterial(
                    materials.Valid,
                    "FarmAreaPreview_Valid",
                    GameConfig.World.FarmPreviewValidGridStrength);
            }

            if (invalidMaterial == null)
            {
                invalidMaterial = CreatePreviewMaterial(
                    materials.Invalid,
                    "FarmAreaPreview_Invalid",
                    GameConfig.World.FarmPreviewInvalidGridStrength);
            }

            if (!materialsAssigned &&
                meshRenderer != null &&
                validMaterial != null &&
                invalidMaterial != null)
            {
                meshRenderer.sharedMaterials = new[]
                {
                    validMaterial,
                    invalidMaterial,
                };
                materialsAssigned = true;
            }
        }

        private void BuildMesh(Vector3Int minCoord, Vector3Int maxCoord, float tileSize)
        {
            ClearMeshData();
            int cellCount = Mathf.Max(
                1,
                (maxCoord.x - minCoord.x + 1) * (maxCoord.z - minCoord.z + 1));
            EnsureMeshDataCapacity(cellCount);

            float halfTileSize = tileSize * 0.5f;
            float surfaceLift = tileSize * GameConfig.World.FarmPreviewSurfaceLiftInTiles;
            for (int x = minCoord.x; x <= maxCoord.x; x++)
            {
                for (int z = minCoord.z; z <= maxCoord.z; z++)
                {
                    Vector3Int coord = new Vector3Int(x, minCoord.y, z);
                    Vector3 center = MapManager.Instance.GetTileSurfaceWorldPosition(coord) +
                                     Vector3.up * surfaceLift;
                    List<int> triangles = MapManager.Instance.CanPlaceMapObject(coord)
                        ? validTriangles
                        : invalidTriangles;
                    AddCell(center, halfTileSize, triangles);
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(validTriangles, 0, true);
            mesh.SetTriangles(invalidTriangles, 1, true);
            mesh.RecalculateBounds();
        }

        private void AddCell(Vector3 center, float halfTileSize, List<int> triangles)
        {
            int startIndex = vertices.Count;
            vertices.Add(center + new Vector3(-halfTileSize, 0f, -halfTileSize));
            vertices.Add(center + new Vector3(-halfTileSize, 0f, halfTileSize));
            vertices.Add(center + new Vector3(halfTileSize, 0f, halfTileSize));
            vertices.Add(center + new Vector3(halfTileSize, 0f, -halfTileSize));

            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        private void EnsureMeshDataCapacity(int cellCount)
        {
            int vertexCount = cellCount * 4;
            int triangleIndexCount = cellCount * 6;
            if (vertices.Capacity < vertexCount)
            {
                vertices.Capacity = vertexCount;
                normals.Capacity = vertexCount;
                uvs.Capacity = vertexCount;
            }

            if (validTriangles.Capacity < triangleIndexCount)
            {
                validTriangles.Capacity = triangleIndexCount;
                invalidTriangles.Capacity = triangleIndexCount;
            }
        }

        private void ClearMeshData()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            validTriangles.Clear();
            invalidTriangles.Clear();
        }

        private static Material CreatePreviewMaterial(
            Material source,
            string materialName,
            float gridStrength)
        {
            if (source == null)
            {
                return null;
            }

            Material material = new Material(source)
            {
                name = materialName,
            };
            SetFloatIfPresent(material, "_GridScale", GameConfig.World.FarmPreviewGridScale);
            SetFloatIfPresent(material, "_GridStrength", gridStrength);
            SetFloatIfPresent(material, "_GridWidth", GameConfig.World.FarmPreviewGridWidth);
            SetFloatIfPresent(material, "_RimStrength", GameConfig.World.FarmPreviewRimStrength);
            return material;
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
