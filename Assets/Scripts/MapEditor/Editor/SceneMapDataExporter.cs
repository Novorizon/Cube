#if UNITY_EDITOR

using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class SceneMapDataExporter
    {
        private const string DefaultDirectory = "Assets/Data/Map";
        private const float TileSize = 1f;
        private const float BridgeOverlayYOffset = 0.62f;
        private const float StairOverlayYOffset = 0.5f;

        [MenuItem("Tools/Map/Scene Export/Export Selected Root To Map JSON")]
        public static void ExportSelectedRootToMapJson()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Export Map JSON", "Select a map root GameObject first.", "OK");
                return;
            }

            ExportToJson(new[] { selected }, $"{selected.name}_Map");
        }

        [MenuItem("Tools/Map/Scene Export/Export Whole Scene To Map JSON")]
        public static void ExportWholeSceneToMapJson()
        {
            Scene scene = SceneManager.GetActiveScene();
            ExportToJson(scene.GetRootGameObjects(), $"{scene.name}_Map");
        }

        private static void ExportToJson(GameObject[] roots, string defaultName)
        {
            List<ScannedTile> scannedTiles = ScanTiles(roots);
            if (scannedTiles.Count == 0)
            {
                EditorUtility.DisplayDialog("Export Map JSON", "No known map tile prefabs were found.", "OK");
                return;
            }

            MapData mapData = BuildMapData(scannedTiles, defaultName);
            string path = EditorUtility.SaveFilePanelInProject(
                "Export Map JSON",
                defaultName,
                "json",
                "Choose where to save the exported map json.",
                DefaultDirectory);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog(
                    "Overwrite Map JSON",
                    $"Map json already exists:\n{path}\n\nOverwrite it?",
                    "Overwrite",
                    "Cancel"))
            {
                return;
            }

            SaveMapJson(mapData, path);
            AssetDatabase.Refresh();
            Debug.Log($"Exported map json: {path}, tiles: {mapData.Cells.Count}, size: {mapData.Width}x{mapData.Height}x{mapData.Depth}");
        }

        private static List<ScannedTile> ScanTiles(GameObject[] roots)
        {
            List<ScannedTile> result = new List<ScannedTile>();
            HashSet<GameObject> visitedPrefabRoots = new HashSet<GameObject>();

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null)
                {
                    continue;
                }

                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    GameObject current = transforms[j].gameObject;
                    GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(current);

                    if (prefabRoot != null)
                    {
                        if (prefabRoot != current || visitedPrefabRoots.Contains(prefabRoot))
                        {
                            continue;
                        }

                        visitedPrefabRoots.Add(prefabRoot);
                    }

                    if (TryClassifyObject(current, out TileClassification classification))
                    {
                        result.Add(CreateScannedTile(current.transform, classification));
                    }
                }
            }

            return result;
        }

        private static bool TryClassifyObject(GameObject gameObject, out TileClassification classification)
        {
            classification = default;

            string assetPath = GetSourceAssetPath(gameObject);
            string objectName = gameObject.name;
            string key = $"{assetPath}/{objectName}".ToLowerInvariant();
            string normalizedName = objectName.Replace("(Clone)", string.Empty).Trim().ToLowerInvariant();

            if (ContainsAny(key, "decoration/bridge", "wooden_plank_bridge"))
            {
                classification = TileClassification.OverlayTile(MapTileOverlay.Bridge, BridgeOverlayYOffset);
                return true;
            }

            if (ContainsAny(key, "decoration/stair", "staircase"))
            {
                classification = TileClassification.OverlayTile(MapTileOverlay.Stair, StairOverlayYOffset);
                return true;
            }

            if (normalizedName == "road" || ContainsAny(key, "/road.prefab", "road_"))
            {
                classification = TileClassification.Base(MapTileType.Road);
                return true;
            }

            if (normalizedName == "grass" || ContainsAny(key, "/grass.prefab", "grass_"))
            {
                classification = TileClassification.Base(MapTileType.Grass);
                return true;
            }

            if (normalizedName == "hill" || ContainsAny(key, "/hill.prefab", "hill_"))
            {
                classification = TileClassification.Base(MapTileType.Hill);
                return true;
            }

            if (normalizedName == "snow" || ContainsAny(key, "/snow.prefab", "snow_"))
            {
                classification = TileClassification.Base(MapTileType.Snow);
                return true;
            }

            if (normalizedName == "water" || ContainsAny(key, "/water.prefab", "water_"))
            {
                classification = TileClassification.Base(MapTileType.Water);
                return true;
            }

            if (ContainsAny(key, "referencestylegrasstile", "styletile"))
            {
                classification = TileClassification.Base(MapTileType.Grass);
                return true;
            }

            return false;
        }

        private static string GetSourceAssetPath(GameObject gameObject)
        {
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            return source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        }

        private static bool ContainsAny(string value, params string[] patterns)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                if (value.Contains(patterns[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static ScannedTile CreateScannedTile(Transform transform, TileClassification classification)
        {
            Vector3 position = transform.position;
            if (classification.IsOverlay)
            {
                position.y -= classification.OverlayYOffset;
            }

            return new ScannedTile
            {
                Coord = new Vector3Int(
                    Mathf.RoundToInt(position.x / TileSize),
                    Mathf.RoundToInt(position.y / TileSize),
                    Mathf.RoundToInt(position.z / TileSize)),
                RotationY = NormalizeAngle(transform.eulerAngles.y),
                Classification = classification,
                SourceName = transform.name
            };
        }

        private static MapData BuildMapData(List<ScannedTile> scannedTiles, string mapName)
        {
            BoundsInt bounds = GetBounds(scannedTiles);
            Vector3Int offset = new Vector3Int(-bounds.xMin, -bounds.yMin, -bounds.zMin);
            Dictionary<Vector3Int, TileBuildState> states = new Dictionary<Vector3Int, TileBuildState>();

            for (int i = 0; i < scannedTiles.Count; i++)
            {
                ScannedTile scanned = scannedTiles[i];
                Vector3Int coord = scanned.Coord + offset;

                if (!states.TryGetValue(coord, out TileBuildState state))
                {
                    state = new TileBuildState
                    {
                        Type = MapTileType.Grass,
                        TypeDirection = MapDirection.North,
                        Overlay = MapTileOverlay.None,
                        OverlayDirection = MapDirection.None
                    };
                }

                if (scanned.Classification.IsOverlay)
                {
                    state.Overlay = scanned.Classification.Overlay;
                    state.OverlayDirection = GetDirection(scanned.RotationY);
                }
                else
                {
                    state.Type = scanned.Classification.Type;
                    state.TypeDirection = GetDirection(scanned.RotationY);
                }

                states[coord] = state;
            }

            MapData mapData = new MapData(0, mapName, bounds.size.x, bounds.size.y, bounds.size.z)
            {
                Description = $"Exported from scene objects. Scene coordinate offset: {bounds.min}."
            };

            List<Vector3Int> coords = new List<Vector3Int>(states.Keys);
            coords.Sort(CompareCoord);

            for (int i = 0; i < coords.Count; i++)
            {
                Vector3Int coord = coords[i];
                TileBuildState state = states[coord];
                MapCellData tile = new MapCellData(coord.x, coord.y, coord.z, state.Type)
                {
                    TypeDirection = state.TypeDirection,
                    Overlay = new MapOverlayLayerData(state.Overlay, state.OverlayDirection)
                };
                tile.ApplyDefaultLogic(mapData);
                mapData.Cells.Add(tile);
            }

            return mapData;
        }

        private static BoundsInt GetBounds(List<ScannedTile> tiles)
        {
            Vector3Int min = tiles[0].Coord;
            Vector3Int max = tiles[0].Coord;

            for (int i = 1; i < tiles.Count; i++)
            {
                Vector3Int coord = tiles[i].Coord;
                min = Vector3Int.Min(min, coord);
                max = Vector3Int.Max(max, coord);
            }

            return new BoundsInt(min, max - min + Vector3Int.one);
        }

        private static int CompareCoord(Vector3Int left, Vector3Int right)
        {
            int z = left.z.CompareTo(right.z);
            if (z != 0)
            {
                return z;
            }

            int y = left.y.CompareTo(right.y);
            if (y != 0)
            {
                return y;
            }

            return left.x.CompareTo(right.x);
        }

        private static MapDirection GetDirection(float rotationY)
        {
            int quarterTurns = Mathf.RoundToInt(rotationY / 90f) % 4;
            if (quarterTurns < 0)
            {
                quarterTurns += 4;
            }

            switch (quarterTurns)
            {
                case 1:
                    return MapDirection.East;

                case 2:
                    return MapDirection.South;

                case 3:
                    return MapDirection.West;

                default:
                    return MapDirection.North;
            }
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return angle;
        }

        private static void SaveMapJson(MapData mapData, string assetPath)
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(mapData, Formatting.Indented);
            File.WriteAllText(absolutePath, json, new UTF8Encoding(false));
        }

        private struct ScannedTile
        {
            public Vector3Int Coord;
            public float RotationY;
            public TileClassification Classification;
            public string SourceName;
        }

        private struct TileClassification
        {
            public bool IsOverlay;
            public MapTileType Type;
            public MapTileOverlay Overlay;
            public float OverlayYOffset;

            public static TileClassification Base(MapTileType type)
            {
                return new TileClassification
                {
                    IsOverlay = false,
                    Type = type,
                    Overlay = MapTileOverlay.None,
                    OverlayYOffset = 0f
                };
            }

            public static TileClassification OverlayTile(MapTileOverlay overlay, float overlayYOffset)
            {
                return new TileClassification
                {
                    IsOverlay = true,
                    Type = MapTileType.Grass,
                    Overlay = overlay,
                    OverlayYOffset = overlayYOffset
                };
            }
        }

        private struct TileBuildState
        {
            public MapTileType Type;
            public MapDirection TypeDirection;
            public MapTileOverlay Overlay;
            public MapDirection OverlayDirection;
        }
    }
}

#endif
