#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class TileTopMaterialPreviewCreator
    {
        private const string PreviewRootName = "TileTopMaterialPreview";
        private const string PreviewCameraName = "TileTopPreviewCamera";
        private const string PreviewKeyLightName = "TileTopPreviewKeyLight";
        private const string PreviewFillLightName = "TileTopPreviewFillLight";
        private const string TestTilePrefabPath = "Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_Test.prefab";
        private const string GrassMaterialPath = "Assets/Arts/Map/Tiles/Materials/GrassTop_Stylized.mat";
        private const string SnowMaterialPath = "Assets/Arts/Map/Tiles/Materials/SnowTop_Stylized.mat";
        private const string RoadMaterialPath = "Assets/Arts/Map/Tiles/Materials/RoadTop_Stylized.mat";
        private const string WaterMaterialPath = "Assets/Arts/Map/Tiles/Materials/WaterTop_Stylized.mat";

        [MenuItem("Tools/Map Art/Tile Top/Create Material Preview Grid")]
        [MenuItem("Tools/Map Art/Tile Top/Create Standard Art Preview")]
        public static void CreatePreviewGrid()
        {
            TileTopMaterialCreator.CreateAll();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestTilePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Missing test tile prefab: {TestTilePrefabPath}");
                return;
            }

            Material grass = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
            Material snow = AssetDatabase.LoadAssetAtPath<Material>(SnowMaterialPath);
            Material road = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
            Material water = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            List<Material> materials = new List<Material> { grass, snow, road, water };
            string[] rowNames = { "Grass", "Snow", "Road", "Water" };

            for (int i = materials.Count - 1; i >= 0; i--)
            {
                if (materials[i] == null)
                {
                    Debug.LogError($"Missing preview material: {i}");
                    return;
                }
            }

            GameObject oldRoot = GameObject.Find(PreviewRootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot);
            }

            GameObject root = new GameObject(PreviewRootName);
            const int columns = 3;
            const float spacing = 1.08f;
            for (int row = 0; row < materials.Count; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (instance == null)
                    {
                        instance = Object.Instantiate(prefab);
                    }

                    instance.name = $"{rowNames[row]}_TopPreview_{column + 1}";
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.position = new Vector3(column * spacing, 0f, -row * spacing);
                    AssignTopMaterial(instance, materials[row]);
                }
            }

            Vector3 gridCenter = new Vector3((columns - 1) * spacing * 0.5f, 0.52f, -(materials.Count - 1) * spacing * 0.5f);
            SetupPreviewLightingAndCamera(root, gridCenter);

            Selection.activeGameObject = root;
            FocusSceneView(gridCenter);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Tile top material preview grid created: {PreviewRootName}");
        }

        private static void AssignTopMaterial(GameObject root, Material material)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && renderer.name == "TopBody")
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private static void SetupPreviewLightingAndCamera(GameObject root, Vector3 focus)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.55f, 0.50f, 1f);

            GameObject cameraObject = new GameObject(PreviewCameraName);
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = focus + new Vector3(2.75f, 2.65f, -3.25f);
            cameraObject.transform.LookAt(focus);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.40f, 0.36f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 2.35f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.depth = 10f;
            Camera.SetupCurrent(camera);

            GameObject keyLightObject = new GameObject(PreviewKeyLightName);
            keyLightObject.transform.SetParent(root.transform, false);
            keyLightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1.00f, 0.96f, 0.88f, 1f);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.28f;

            GameObject fillLightObject = new GameObject(PreviewFillLightName);
            fillLightObject.transform.SetParent(root.transform, false);
            fillLightObject.transform.position = focus + new Vector3(-2.5f, 1.8f, 2.0f);
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.72f, 0.83f, 1.00f, 1f);
            fillLight.intensity = 0.45f;
            fillLight.range = 5f;
            fillLight.shadows = LightShadows.None;
        }

        private static void FocusSceneView(Vector3 focus)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                SceneView.FrameLastActiveSceneView();
                return;
            }

            sceneView.orthographic = true;
            sceneView.LookAt(focus, Quaternion.Euler(32f, -40f, 0f), 3.4f);
            sceneView.Repaint();
        }
    }
}

#endif
