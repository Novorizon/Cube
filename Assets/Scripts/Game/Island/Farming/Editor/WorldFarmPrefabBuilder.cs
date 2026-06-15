using Game;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class WorldFarmPrefabBuilder
    {
        private const string Root = "Assets/Arts/Map/Farming";
        private const string PrefabDir = Root + "/Prefabs";
        private const string MaterialDir = Root + "/Materials";
        private const string PlotMaterialPath = MaterialDir + "/WorldFarmPlot.mat";
        private const string CropMaterialPath = MaterialDir + "/WorldCropSphere.mat";

        [MenuItem("Tools/World/Farming/Rebuild Farm Prefabs")]
        public static void RebuildFarmPrefabs()
        {
            EnsureFolder("Assets/Arts");
            EnsureFolder("Assets/Arts/Map");
            EnsureFolder(Root);
            EnsureFolder(PrefabDir);
            EnsureFolder(MaterialDir);

            Material plotMaterial = CreateOrUpdateMaterial(PlotMaterialPath, new Color(0.42f, 0.25f, 0.12f));
            Material cropMaterial = CreateOrUpdateMaterial(CropMaterialPath, new Color(0.32f, 0.72f, 0.28f));

            CreateFarmPlotPrefab(plotMaterial);
            CreateCropPrefab(cropMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[World Farm] Rebuilt farm prefabs:\n{FarmManager.FarmPlotPrefabPath}\n{FarmManager.CropPrefabPath}");
        }

        private static void CreateFarmPlotPrefab(Material material)
        {
            GameObject root = new GameObject("WorldFarmPlot");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.88f, 0.05f, 0.88f);
            ApplyMaterialAndRemoveCollider(visual, material);

            PrefabUtility.SaveAsPrefabAsset(root, FarmManager.FarmPlotPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateCropPrefab(Material material)
        {
            GameObject root = new GameObject("WorldCropSphere");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            ApplyMaterialAndRemoveCollider(visual, material);

            PrefabUtility.SaveAsPrefabAsset(root, FarmManager.CropPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void ApplyMaterialAndRemoveCollider(GameObject instance, Material material)
        {
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(FindShader());
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Shader FindShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit") ??
                   Shader.Find("Standard") ??
                   Shader.Find("Diffuse");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            int slashIndex = assetPath.LastIndexOf('/');
            if (slashIndex <= 0)
            {
                return;
            }

            string parent = assetPath.Substring(0, slashIndex);
            string name = assetPath.Substring(slashIndex + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
