///------------------------------------
/// Author閿涙uanjinbiao
/// Mail閿涙ovogooglor@gmail.com
/// Date閿?025-12-10
/// Description閿涙艾婀撮崶鍓ь吀閻炲棗娅?///------------------------------------

using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public partial class MapManager : Singleton<MapManager>
    {
        private const string PrefabConfigPath = "Assets/Data/Configs/MapTilePrefabConfig.asset";
        private const string DecorationConfigPath = "Assets/Data/Configs/MapDecorationPrefabConfig.asset";
        private const float PickHeightEpsilon = 0.0001f;
        private const float MinTileSize = 0.01f;

        private MapTilePrefabConfig mapTilePrefabConfig;
        private MapDecorationPrefabConfig decorationPrefabConfig;
        private MapData currentMap;
        private int currentMapId;

        private readonly Dictionary<Vector3Int, MapCellData> tileMap = new Dictionary<Vector3Int, MapCellData>();
        private readonly Dictionary<Vector3Int, TileData> tileDataMap = new Dictionary<Vector3Int, TileData>();
        private readonly Dictionary<Vector3Int, TileView> tileViews = new Dictionary<Vector3Int, TileView>();
        private readonly Dictionary<Vector3Int, List<MapObjectData>> objectsByCoord = new Dictionary<Vector3Int, List<MapObjectData>>();
        private readonly Dictionary<Vector2Int, TileData> topTileDataMap = new Dictionary<Vector2Int, TileData>();
        private readonly Dictionary<Vector2Int, TileData> topLogicTileDataMap = new Dictionary<Vector2Int, TileData>();
        private readonly List<float> topLogicPickHeights = new List<float>();
        private readonly HashSet<string> removedMapObjectKeys = new HashSet<string>();

        private Transform mapRoot;
        private float tileSize = 1f;

        private bool initialized = false;

        public bool Initialized
        {
            get
            {
                return initialized;
            }
        }

        public MapData CurrentMap
        {
            get
            {
                return currentMap;
            }
        }

        public float TileSize
        {
            get
            {
                return tileSize;
            }
        }

        public Transform MapRoot
        {
            get
            {
                return mapRoot;
            }
        }

        public IReadOnlyList<Vector3Int> SpawnPoints
        {
            get
            {
                if (currentMap == null || currentMap.SpawnPoints == null)
                {
                    return null;
                }

                return currentMap.SpawnPoints;
            }
        }

        public bool HasGoalPoint
        {
            get
            {
                if (currentMap == null)
                {
                    return false;
                }

                return currentMap.HasGoalPoint;
            }
        }

        public Vector3Int GoalPoint
        {
            get
            {
                if (currentMap == null)
                {
                    return default;
                }

                return currentMap.GoalPoint;
            }
        }

        public bool Initialize()
        {
            mapTilePrefabConfig = ResourceManager.Instance.LoadAsset<MapTilePrefabConfig>(PrefabConfigPath);
            decorationPrefabConfig = ResourceManager.Instance.LoadAsset<MapDecorationPrefabConfig>(DecorationConfigPath);
            if (mapTilePrefabConfig == null)
            {
                Debug.LogError($"MapManager initialize failed. Missing prefab config: {PrefabConfigPath}");
                initialized = false;
                return false;
            }

            if (decorationPrefabConfig != null)
            {
                decorationPrefabConfig.RebuildCache();
            }

            initialized = true;
            return true;
        }

        private void CreateMap()
        {
            if (currentMap == null)
            {
                Debug.LogWarning("CreateMap failed. Current map is null.");
                return;
            }

            ClearMapObjects();
            EnsureMapRoot();

            tileViews.Clear();

            if (currentMap.Cells == null)
            {
                Debug.LogWarning("CreateMap failed. Current map tiles is null.");
                return;
            }

            for (int i = 0; i < currentMap.Cells.Count; i++)
            {
                MapCellData MapCellData = currentMap.Cells[i];

                if (MapCellData == null)
                {
                    continue;
                }

                Vector3Int coord = new Vector3Int(MapCellData.X, MapCellData.Y, MapCellData.Z);

                if (!tileDataMap.TryGetValue(coord, out TileData tileData))
                {
                    continue;
                }

                CreateTileView(tileData);
            }

            CreateDecorationViews();
            CreateResourceViews();
            WorldBuildingManager.Instance.CreateViews();
            Debug.Log($"Create map success. Count: {tileViews.Count}");
        }

        private void CreateTileView(TileData tileData)
        {
            Vector3Int key = tileData.Coord;

            GameObject prefab = GetPrefab(tileData.Type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {tileData.Type}, Coord: {key}");
                return;
            }

            Vector3 position = GetWorldPosition(tileData.X, tileData.Y, tileData.Z);

            GameObject instance = GameObject.Instantiate(prefab, position, Quaternion.identity, mapRoot);
            instance.name = $"{tileData.Type}_{tileData.Overlay}_{tileData.X}_{tileData.Y}_{tileData.Z}";
            instance.transform.localRotation = GetDirectionRotation(tileData.TypeDirection);
            CreateOverlayView(tileData, instance.transform);

            TileView tileView = TileView.InitializeHierarchy(instance, tileData);
            if (tileView == null)
            {
                Debug.LogWarning($"Tile prefab root must contain TileView. Type: {tileData.Type}, Coord: {key}, Instance: {instance.name}");
                return;
            }

            ApplyTileVisual(tileData, tileView);
            tileViews[key] = tileView;
        }

        private void ApplyTileVisual(TileData tileData, TileView tileView)
        {
            if (tileData == null || tileView == null)
            {
                return;
            }

            GrassTileMaterialOverride grassVisual = tileView.GetComponent<GrassTileMaterialOverride>();
            if (grassVisual == null)
            {
                return;
            }

            MapGrassVisualData visualData = tileData.Type == MapTileType.Grass
                ? tileData.MapCellData?.GrassVisual
                : null;
            grassVisual.ApplyVisualData(visualData);
        }

        private void CreateOverlayView(TileData tileData, Transform parent)
        {
            GameObject overlay = CreateOverlayInstance(tileData.Overlay);
            if (overlay == null) return;

            overlay.transform.SetParent(parent, false);
            overlay.name = $"Overlay_{tileData.Overlay}_{tileData.OverlayDirection}";
            overlay.transform.localPosition = GetOverlayLocalPosition(tileData.Overlay);
            overlay.transform.localRotation = Quaternion.Inverse(parent.localRotation) * GetDirectionRotation(tileData.OverlayDirection);
        }

        private GameObject CreateOverlayInstance(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    GameObject bridgePrefab = GetPrefab(MapTileType.Bridge);
                    return bridgePrefab != null ? GameObject.Instantiate(bridgePrefab) : null;

                case MapTileOverlay.Stair:
                    return CreateOverlayFallback("Stair", new Color(0.75f, 0.62f, 0.42f));

                case MapTileOverlay.Ramp:
                    return CreateOverlayFallback("Ramp", new Color(0.65f, 0.55f, 0.35f));

                default:
                    return null;
            }
        }

        private GameObject CreateOverlayFallback(string name, Color color)
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = name;
            fallback.transform.localScale = new Vector3(tileSize * 0.85f, 0.08f, tileSize * 0.85f);

            Renderer renderer = fallback.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(FindRuntimeColorShader());
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                else
                {
                    material.color = color;
                }

                renderer.sharedMaterial = material;
            }

            return fallback;
        }

        private static Shader FindRuntimeColorShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                return shader;
            }

            return Shader.Find("Sprites/Default");
        }

        private Quaternion GetDirectionRotation(MapDirection direction)
        {
            switch (direction)
            {
                case MapDirection.East:
                    return Quaternion.Euler(0f, 90f, 0f);

                case MapDirection.South:
                    return Quaternion.Euler(0f, 180f, 0f);

                case MapDirection.West:
                    return Quaternion.Euler(0f, 270f, 0f);

                default:
                    return Quaternion.identity;
            }
        }

        private Vector3 GetOverlayLocalPosition(MapTileOverlay overlay)
        {
            switch (overlay)
            {
                case MapTileOverlay.Bridge:
                    return Vector3.up * tileSize;

                case MapTileOverlay.Stair:
                case MapTileOverlay.Ramp:
                    return Vector3.up * (tileSize * 0.5f);

                default:
                    return Vector3.zero;
            }
        }

        private void CreateDecorationViews()
        {
            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                CreateDecorationView(currentMap.Objects[i], i);
            }
        }

        private void CreateDecorationView(MapObjectData decoration, int index)
        {
            if (decoration == null || decoration.ObjectType != MapObjectType.Decoration || decoration.ConfigId <= 0)
            {
                return;
            }

            if (!tileViews.TryGetValue(decoration.Coord, out TileView tileView) || tileView == null)
            {
                Debug.LogWarning($"Decoration skipped. Tile not found. Id: {decoration.ConfigId}, Coord: {decoration.Coord}");
                return;
            }

            GameObject prefab = GetDecorationPrefab(decoration);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing decoration prefab. Id: {decoration.ConfigId}");
                return;
            }

            GameObject instance = GameObject.Instantiate(prefab, tileView.transform);
            instance.name = $"Decoration_{index}_{prefab.name}";
            instance.transform.localPosition = decoration.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(decoration.LocalEuler);
            instance.transform.localScale = decoration.LocalScale;
        }

        private GameObject GetDecorationPrefab(MapObjectData decoration)
        {
            if (decorationPrefabConfig != null && decoration.ConfigId > 0)
            {
                GameObject prefab = decorationPrefabConfig.GetPrefab(decoration.ConfigId);
                if (prefab != null) return prefab;
            }

            return null;
        }

        private void CreateResourceViews()
        {
            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                CreateResourceView(currentMap.Objects[i], i);
            }
        }

        private void CreateResourceView(MapObjectData mapObject, int index)
        {
            if (mapObject == null || mapObject.ObjectType != MapObjectType.Resource || mapObject.ConfigId <= 0)
            {
                return;
            }

            if (!tileViews.TryGetValue(mapObject.Coord, out TileView tileView) || tileView == null)
            {
                Debug.LogWarning($"Resource skipped. Tile not found. Id: {mapObject.ConfigId}, Coord: {mapObject.Coord}");
                return;
            }

            GameObject prefab = GetWorldResourcePrefab(mapObject.ConfigId);
            if (prefab == null)
            {
                Debug.LogWarning($"Missing world resource prefab. Id: {mapObject.ConfigId}");
                return;
            }

            GameObject instance = GameObject.Instantiate(prefab, tileView.transform);
            instance.name = $"Resource_{index}_{prefab.name}";
            instance.transform.localPosition = mapObject.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(mapObject.LocalEuler);
            instance.transform.localScale = mapObject.LocalScale;

            WorldResourceView resourceView = instance.GetComponent<WorldResourceView>();
            if (resourceView == null)
            {
                resourceView = instance.AddComponent<WorldResourceView>();
            }

            resourceView.Initialize(mapObject);
        }

        private GameObject GetWorldResourcePrefab(int worldResourceId)
        {
            if (DataManager.Instance.Resource == null ||
                !DataManager.Instance.Resource.TryGet(worldResourceId, out ResourceConfig config) ||
                config == null ||
                string.IsNullOrEmpty(config.PrefabLocation))
            {
                return null;
            }

            return ResourceManager.Instance.LoadAsset<GameObject>(config.PrefabLocation);
        }

        private void RebuildTileIndex()
        {
            tileMap.Clear();
            tileDataMap.Clear();
            topTileDataMap.Clear();
            topLogicTileDataMap.Clear();
            topLogicPickHeights.Clear();

            if (currentMap == null || currentMap.Cells == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Cells.Count; i++)
            {
                MapCellData MapCellData = currentMap.Cells[i];

                if (MapCellData == null)
                {
                    continue;
                }

                MapCellData.EnsureLayers();

                Vector3Int key = new Vector3Int(MapCellData.X, MapCellData.Y, MapCellData.Z);

                tileMap[key] = MapCellData;
                tileDataMap[key] = new TileData(MapCellData);
            }

            RebuildTopTileIndex();
        }

        private void RebuildTopTileIndex()
        {
            topTileDataMap.Clear();
            topLogicTileDataMap.Clear();
            topLogicPickHeights.Clear();

            foreach (KeyValuePair<Vector3Int, TileData> pair in tileDataMap)
            {
                Vector3Int coord = pair.Key;
                TileData tileData = pair.Value;
                Vector2Int column = new Vector2Int(coord.x, coord.z);

                if (!topTileDataMap.TryGetValue(column, out TileData topTile) || coord.y > topTile.Y)
                {
                    topTileDataMap[column] = tileData;
                }

                if (!MapTileRule.IsLogicTile(tileData.Type))
                {
                    continue;
                }

                if (!topLogicTileDataMap.TryGetValue(column, out TileData topLogicTile) || coord.y > topLogicTile.Y)
                {
                    topLogicTileDataMap[column] = tileData;
                }
            }

            RebuildTopLogicPickHeights();
        }

        private void RebuildTopLogicPickHeights()
        {
            topLogicPickHeights.Clear();

            foreach (KeyValuePair<Vector2Int, TileData> pair in topLogicTileDataMap)
            {
                TileData tileData = pair.Value;
                if (tileData == null)
                {
                    continue;
                }

                float topWorldY = GetTileTopWorldY(tileData);
                if (!ContainsPickHeight(topWorldY))
                {
                    topLogicPickHeights.Add(topWorldY);
                }
            }

            topLogicPickHeights.Sort((left, right) => right.CompareTo(left));
        }

        private bool ContainsPickHeight(float height)
        {
            for (int i = 0; i < topLogicPickHeights.Count; i++)
            {
                if (Mathf.Abs(topLogicPickHeights[i] - height) <= PickHeightEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildObjectIndex()
        {
            objectsByCoord.Clear();

            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                AddObjectToIndex(currentMap.Objects[i]);
            }
        }

        private void AddObjectToIndex(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return;
            }

            GetMapObjectFootprintSize(mapObject, out int sizeX, out int sizeZ);
            for (int offsetX = 0; offsetX < sizeX; offsetX++)
            {
                for (int offsetZ = 0; offsetZ < sizeZ; offsetZ++)
                {
                    AddObjectToIndexCoord(mapObject, new Vector3Int(
                        mapObject.X + offsetX,
                        mapObject.Y,
                        mapObject.Z + offsetZ));
                }
            }
        }

        private void AddObjectToIndexCoord(MapObjectData mapObject, Vector3Int coord)
        {
            if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects))
            {
                objects = new List<MapObjectData>();
                objectsByCoord[coord] = objects;
            }

            objects.Add(mapObject);
        }

        private void RemoveObjectFromIndex(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return;
            }

            GetMapObjectFootprintSize(mapObject, out int sizeX, out int sizeZ);
            for (int offsetX = 0; offsetX < sizeX; offsetX++)
            {
                for (int offsetZ = 0; offsetZ < sizeZ; offsetZ++)
                {
                    RemoveObjectFromIndexCoord(mapObject, new Vector3Int(
                        mapObject.X + offsetX,
                        mapObject.Y,
                        mapObject.Z + offsetZ));
                }
            }
        }

        private void RemoveObjectFromIndexCoord(MapObjectData mapObject, Vector3Int coord)
        {
            if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects) || objects == null)
            {
                return;
            }

            objects.Remove(mapObject);
            if (objects.Count == 0)
            {
                objectsByCoord.Remove(coord);
            }
        }

        private void EnsureMapRoot()
        {
            GameObject rootObject = GameObject.Find("MapRoot");

            if (rootObject == null)
            {
                rootObject = new GameObject("MapRoot");
                rootObject.transform.position = Vector3.zero;
            }

            mapRoot = rootObject.transform;
        }

        public void ClearMap()
        {
            BaseManager.Instance.ClearBaseObject();

            currentMap = null;
            currentMapId = 0;
            tileMap.Clear();
            tileDataMap.Clear();
            topTileDataMap.Clear();
            topLogicTileDataMap.Clear();
            topLogicPickHeights.Clear();
            objectsByCoord.Clear();
            WorldBuildingManager.Instance.ClearViews();
            FarmManager.Instance.ClearViews();
            ClearMapObjects();
        }

        private void ClearMapObjects()
        {
            if (mapRoot == null)
            {
                GameObject oldRoot = GameObject.Find("MapRoot");

                if (oldRoot != null)
                {
                    mapRoot = oldRoot.transform;
                }
            }

            if (mapRoot == null)
            {
                tileViews.Clear();
                return;
            }

            for (int i = mapRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = mapRoot.GetChild(i);

                if (child == null)
                {
                    continue;
                }

                GameObject.Destroy(child.gameObject);
            }

            tileViews.Clear();
        }

        private Vector3 GetWorldPosition(int x, int y, int z)
        {
            return new Vector3(x * tileSize, y * tileSize, z * tileSize);
        }

        private float GetSafeTileSize()
        {
            return Mathf.Max(MinTileSize, tileSize);
        }

        private float GetTileTopLocalY(MapTileType type)
        {
            return mapTilePrefabConfig != null
                ? mapTilePrefabConfig.GetTopLocalY(type)
                : MapTilePrefabConfig.DefaultTopLocalY;
        }

        private float GetTileTopWorldY(TileData tileData)
        {
            if (tileData == null)
            {
                return 0f;
            }

            return tileData.Y * tileSize + GetTileTopLocalY(tileData.Type);
        }

        private Vector2Int WorldPointToTileColumn(Vector3 point)
        {
            float safeTileSize = GetSafeTileSize();
            return new Vector2Int(
                Mathf.FloorToInt(point.x / safeTileSize + 0.5f),
                Mathf.FloorToInt(point.z / safeTileSize + 0.5f));
        }

        private bool IsPointInsideTileFootprint(Vector3 point, TileData tileData)
        {
            if (tileData == null)
            {
                return false;
            }

            float halfSize = GetSafeTileSize() * 0.5f + PickHeightEpsilon;
            Vector3 center = GetTileWorldPosition(tileData);
            return Mathf.Abs(point.x - center.x) <= halfSize &&
                   Mathf.Abs(point.z - center.z) <= halfSize;
        }

        private GameObject GetPrefab(MapTileType type)
        {
            if (mapTilePrefabConfig == null)
            {
                Debug.LogWarning("Prefab config is null.");
                return null;
            }

            GameObject prefab = mapTilePrefabConfig.GetPrefab(type);

            if (prefab == null)
            {
                Debug.LogWarning($"Missing prefab for tile type: {type}");
            }

            return prefab;
        }

        public bool TryGetMapCellData(Vector3Int coord, out MapCellData MapCellData)
        {
            return tileMap.TryGetValue(coord, out MapCellData);
        }

        public bool TryGetMapCellData(int x, int y, int z, out MapCellData MapCellData)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetMapCellData(coord, out MapCellData);
        }

        public bool TryGetMapObjectsAt(Vector3Int coord, out IReadOnlyList<MapObjectData> objects)
        {
            if (objectsByCoord.TryGetValue(coord, out List<MapObjectData> result) && result.Count > 0)
            {
                objects = result;
                return true;
            }

            objects = null;
            return false;
        }

        public bool TryGetMapObjectsAt(int x, int y, int z, out IReadOnlyList<MapObjectData> objects)
        {
            return TryGetMapObjectsAt(new Vector3Int(x, y, z), out objects);
        }

        public bool CanPlaceMapObject(Vector3Int coord)
        {
            return IsBuildable(coord);
        }

        public bool CanPlaceMapObject(Vector3Int coord, int sizeX, int sizeZ)
        {
            sizeX = Mathf.Max(1, sizeX);
            sizeZ = Mathf.Max(1, sizeZ);
            for (int offsetX = 0; offsetX < sizeX; offsetX++)
            {
                for (int offsetZ = 0; offsetZ < sizeZ; offsetZ++)
                {
                    Vector3Int footprintCoord = new Vector3Int(coord.x + offsetX, coord.y, coord.z + offsetZ);
                    if (!IsBuildable(footprintCoord))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool CanPlaceMapObject(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return false;
            }

            GetMapObjectFootprintSize(mapObject, out int sizeX, out int sizeZ);
            return CanPlaceMapObject(mapObject.Coord, sizeX, sizeZ);
        }

        public bool TryAddMapObject(MapObjectData mapObject)
        {
            if (mapObject == null)
            {
                return false;
            }

            if (currentMap == null)
            {
                return false;
            }

            currentMap.EnsureRuntimeCollections();

            if (mapObject.ObjectId > 0 && TryGetMapObject(mapObject.ObjectId, out _))
            {
                Debug.LogWarning($"Add map object failed. Duplicate object id: {mapObject.ObjectId}");
                return false;
            }

            if (!CanPlaceMapObject(mapObject))
            {
                return false;
            }

            currentMap.Objects.Add(mapObject);
            AddObjectToIndex(mapObject);
            return true;
        }

        public bool TryGetMapObject(int objectId, out MapObjectData mapObject)
        {
            mapObject = null;

            if (objectId <= 0 || currentMap == null || currentMap.Objects == null)
            {
                return false;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                MapObjectData current = currentMap.Objects[i];
                if (current != null && current.ObjectId == objectId)
                {
                    mapObject = current;
                    return true;
                }
            }

            return false;
        }

        public bool TryRemoveMapObject(int objectId)
        {
            if (objectId <= 0 || currentMap == null || currentMap.Objects == null)
            {
                return false;
            }

            for (int i = 0; i < currentMap.Objects.Count; i++)
            {
                MapObjectData mapObject = currentMap.Objects[i];
                if (mapObject == null || mapObject.ObjectId != objectId)
                {
                    continue;
                }

                currentMap.Objects.RemoveAt(i);
                RemoveObjectFromIndex(mapObject);
                return true;
            }

            return false;
        }

        public bool TryGetTileData(Vector3Int coord, out TileData tileData)
        {
            return tileDataMap.TryGetValue(coord, out tileData);
        }

        public bool TryGetTileData(int x, int y, int z, out TileData tileData)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetTileData(coord, out tileData);
        }

        public bool TryGetTileView(Vector3Int coord, out TileView tileView)
        {
            return tileViews.TryGetValue(coord, out tileView);
        }

        public bool TryGetTileView(int x, int y, int z, out TileView tileView)
        {
            Vector3Int coord = new Vector3Int(x, y, z);
            return TryGetTileView(coord, out tileView);
        }

        public bool TryPickTile(Vector2 screenPosition, Camera camera, out TileView tileView)
        {
            tileView = null;

            if (camera == null)
            {
                Debug.LogWarning("TryPickTile failed. Camera is null.");
                return false;
            }

            if (TryPickTileByMath(screenPosition, camera, out tileView))
            {
                return true;
            }

            return TryPickTileByCollider(screenPosition, camera, out tileView);
        }

        public bool TryPickTile(Vector2 screenPosition, Camera camera, out TileView tileView, out Vector3 worldPosition)
        {
            tileView = null;
            worldPosition = Vector3.zero;

            if (camera == null)
            {
                Debug.LogWarning("TryPickTile failed. Camera is null.");
                return false;
            }

            if (TryPickTileByMath(screenPosition, camera, out tileView, out worldPosition))
            {
                return true;
            }

            return TryPickTileByCollider(screenPosition, camera, out tileView, out worldPosition);
        }

        public bool TryPickTileByMath(Vector2 screenPosition, Camera camera, out TileView tileView)
        {
            tileView = null;

            if (!TryPickTileDataByMath(screenPosition, camera, out TileData tileData))
            {
                return false;
            }

            return tileData != null &&
                   tileViews.TryGetValue(tileData.Coord, out tileView) &&
                   tileView != null;
        }

        public bool TryPickTileByMath(Vector2 screenPosition, Camera camera, out TileView tileView, out Vector3 worldPosition)
        {
            tileView = null;
            worldPosition = Vector3.zero;

            if (!TryPickTileDataByMath(screenPosition, camera, out TileData tileData, out worldPosition))
            {
                return false;
            }

            return tileData != null &&
                   tileViews.TryGetValue(tileData.Coord, out tileView) &&
                   tileView != null;
        }

        public bool TryPickTileDataByMath(Vector2 screenPosition, Camera camera, out TileData tileData)
        {
            return TryPickTileDataByMath(screenPosition, camera, out tileData, out _);
        }

        public bool TryPickTileDataByMath(Vector2 screenPosition, Camera camera, out TileData tileData, out Vector3 worldPosition)
        {
            tileData = null;
            worldPosition = Vector3.zero;

            if (camera == null)
            {
                return false;
            }

            if (topLogicPickHeights.Count == 0 && topLogicTileDataMap.Count > 0)
            {
                RebuildTopLogicPickHeights();
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            for (int i = 0; i < topLogicPickHeights.Count; i++)
            {
                float pickHeight = topLogicPickHeights[i];
                Plane plane = new Plane(Vector3.up, new Vector3(0f, pickHeight, 0f));
                if (!plane.Raycast(ray, out float enter) || enter < 0f)
                {
                    continue;
                }

                Vector3 point = ray.GetPoint(enter);
                Vector2Int column = WorldPointToTileColumn(point);
                if (!topLogicTileDataMap.TryGetValue(column, out TileData candidate) || candidate == null)
                {
                    continue;
                }

                if (Mathf.Abs(GetTileTopWorldY(candidate) - pickHeight) > PickHeightEpsilon)
                {
                    continue;
                }

                if (!IsPointInsideTileFootprint(point, candidate))
                {
                    continue;
                }

                tileData = candidate;
                worldPosition = point;
                return true;
            }

            return false;
        }

        private bool TryPickTileByCollider(Vector2 screenPosition, Camera camera, out TileView tileView)
        {
            return TryPickTileByCollider(screenPosition, camera, out tileView, out _);
        }

        private bool TryPickTileByCollider(Vector2 screenPosition, Camera camera, out TileView tileView, out Vector3 worldPosition)
        {
            tileView = null;
            worldPosition = Vector3.zero;

            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                if (TileView.TryGetValidFrom(hit.collider.transform, out tileView))
                {
                    worldPosition = hit.point;
                    return true;
                }
            }

            return false;
        }

        public Vector3 GetTileWorldPosition(Vector3Int coord)
        {
            return GetWorldPosition(coord.x, coord.y, coord.z);
        }

        public Vector3 GetTileWorldPosition(MapCellData MapCellData)
        {
            if (MapCellData == null)
            {
                return Vector3.zero;
            }

            return GetWorldPosition(MapCellData.X, MapCellData.Y, MapCellData.Z);
        }

        public Vector3 GetTileWorldPosition(TileData tileData)
        {
            if (tileData == null)
            {
                return Vector3.zero;
            }

            return GetWorldPosition(tileData.X, tileData.Y, tileData.Z);
        }

        public Vector3 GetMapPointWorldPosition(Vector3Int coord)
        {
            return GetTileWorldPosition(coord);
        }

        public bool TryGetGoalPoint(out Vector3Int coord)
        {
            coord = default;

            if (currentMap == null)
            {
                return false;
            }

            if (!currentMap.HasGoalPoint)
            {
                return false;
            }

            coord = currentMap.GoalPoint;
            return true;
        }

        public bool IsInsideMap(Vector3Int coord)
        {
            return tileDataMap.ContainsKey(coord);
        }

        public bool IsLogicTile(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            return MapTileRule.IsLogicTile(tileData.Type);
        }

        public bool IsLogicTileType(MapTileType type)
        {
            return MapTileRule.IsLogicTile(type);
        }

        public bool HasTileAbove(Vector3Int coord)
        {
            Vector3Int aboveCoord = new Vector3Int(coord.x, coord.y + 1, coord.z);
            return tileDataMap.ContainsKey(aboveCoord);
        }

        public bool IsExposed(Vector3Int coord)
        {
            if (!tileDataMap.ContainsKey(coord))
            {
                return false;
            }

            return !HasTileAbove(coord);
        }

        public bool IsWalkable(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!MapTileRule.IsLogicTile(tileData.Type))
            {
                return false;
            }

            if (!IsExposed(coord))
            {
                return false;
            }

            if (HasBlockingObject(coord, blockMove: true))
            {
                return false;
            }

            return tileData.IsRuntimeWalkable;
        }

        public bool IsBuildable(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!MapTileRule.IsLogicTile(tileData.Type))
            {
                return false;
            }

            if (!IsExposed(coord))
            {
                return false;
            }

            if (HasBlockingObject(coord, blockMove: false))
            {
                return false;
            }

            return tileData.IsRuntimeBuildable;
        }

        public int GetMoveCost(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return int.MaxValue;
            }

            if (!IsWalkable(coord))
            {
                return int.MaxValue;
            }

            return tileData.MoveCost;
        }

        public bool HasTower(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            return tileData.HasTower;
        }

        public bool TryGetTower(Vector3Int coord, out Tower tower)
        {
            tower = null;

            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!tileData.HasTower)
            {
                return false;
            }

            tower = tileData.Tower;
            return true;
        }

        public bool CanPlaceTower(Vector3Int coord)
        {
            return CanPlaceMapObject(coord);
        }

        private bool HasBlockingObject(Vector3Int coord, bool blockMove)
        {
            if (!objectsByCoord.TryGetValue(coord, out List<MapObjectData> objects) || objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                MapObjectData mapObject = objects[i];
                if (mapObject == null)
                {
                    continue;
                }

                if (blockMove ? ObjectBlocksMove(mapObject) : ObjectBlocksBuild(mapObject))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ObjectBlocksMove(MapObjectData mapObject)
        {
            if (mapObject.BlocksMove)
            {
                return true;
            }

            MapDecorationPrefabConfig.DecorationPrefabItem item = decorationPrefabConfig != null ? decorationPrefabConfig.GetItem(mapObject.ConfigId) : null;
            return item != null && item.BlocksMove;
        }

        private bool ObjectBlocksBuild(MapObjectData mapObject)
        {
            if (mapObject.BlocksBuild)
            {
                return true;
            }

            MapDecorationPrefabConfig.DecorationPrefabItem item = decorationPrefabConfig != null ? decorationPrefabConfig.GetItem(mapObject.ConfigId) : null;
            return item != null && item.BlocksBuild;
        }

        private static void GetMapObjectFootprintSize(MapObjectData mapObject, out int sizeX, out int sizeZ)
        {
            sizeX = 1;
            sizeZ = 1;
            if (mapObject == null || mapObject.ObjectType != MapObjectType.Building)
            {
                return;
            }

            if (DataManager.Instance.WorldBuilding == null ||
                !DataManager.Instance.WorldBuilding.TryGet(mapObject.ConfigId, out WorldBuildingConfig config) ||
                config == null)
            {
                return;
            }

            sizeX = WorldBuildingFootprint.GetSizeX(config);
            sizeZ = WorldBuildingFootprint.GetSizeZ(config);
        }

        private static bool MapObjectContainsCoord(MapObjectData mapObject, Vector3Int coord)
        {
            if (mapObject == null)
            {
                return false;
            }

            GetMapObjectFootprintSize(mapObject, out int sizeX, out int sizeZ);
            return WorldBuildingFootprint.Contains(mapObject.Coord, sizeX, sizeZ, coord);
        }

        public bool TryPlaceTower(Vector3Int coord, Tower tower)
        {
            if (tower == null)
            {
                return false;
            }

            if (!CanPlaceTower(coord))
            {
                return false;
            }

            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            return tileData.TrySetTower(tower);
        }

        public bool RemoveTower(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (!tileData.HasTower)
            {
                return false;
            }

            tileData.ClearTower();
            return true;
        }

        public bool CanRemoveTile(Vector3Int coord)
        {
            if (!tileDataMap.ContainsKey(coord))
            {
                return false;
            }

            if (HasTileAbove(coord))
            {
                return false;
            }

            if (HasTower(coord))
            {
                return false;
            }

            return true;
        }

        public bool TryRemoveTile(Vector3Int coord)
        {
            if (!CanRemoveTile(coord))
            {
                return false;
            }

            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            tileDataMap.Remove(coord);
            tileMap.Remove(coord);
            RebuildTopTileIndex();

            if (currentMap != null && currentMap.Cells != null)
            {
                currentMap.Cells.Remove(tileData.MapCellData);
            }

            RemoveMapObjectsAt(coord);

            if (tileViews.TryGetValue(coord, out TileView tileView))
            {
                if (tileView != null)
                {
                    GameObject.Destroy(tileView.gameObject);
                }

                tileViews.Remove(coord);
            }

            return true;
        }

        private void RemoveMapObjectsAt(Vector3Int coord)
        {
            if (currentMap == null || currentMap.Objects == null)
            {
                return;
            }

            currentMap.Objects.RemoveAll(mapObject => MapObjectContainsCoord(mapObject, coord));
            RebuildObjectIndex();
        }

        private MapTileType GetTileTypeOrNone(Vector3Int coord)
        {
            return tileMap.TryGetValue(coord, out MapCellData tile) ? tile.Type : MapTileType.None;
        }

        public bool TryDestroyHill(Vector3Int coord)
        {
            if (!tileDataMap.TryGetValue(coord, out TileData tileData))
            {
                return false;
            }

            if (tileData.Type != MapTileType.Hill)
            {
                return false;
            }

            return TryRemoveTile(coord);
        }

        public bool TryGetTopTile(int x, int z, out TileData tileData)
        {
            return topTileDataMap.TryGetValue(new Vector2Int(x, z), out tileData);
        }

        public bool TryGetTopLogicTile(int x, int z, out TileData tileData)
        {
            return topLogicTileDataMap.TryGetValue(new Vector2Int(x, z), out tileData);
        }

    }
}

