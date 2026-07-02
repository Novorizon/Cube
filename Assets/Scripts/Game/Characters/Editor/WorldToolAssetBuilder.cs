#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class WorldToolAssetBuilder
    {
        private const string ToolRoot = "Assets/Arts/Character/Player/Tools";
        private const string PrefabFolder = ToolRoot + "/Prefabs";
        private const string MaterialFolder = ToolRoot + "/Materials";

        [MenuItem("Debug/World/Build Temporary Tool Assets")]
        public static void BuildTemporaryToolAssets()
        {
            EnsureFolders();

            Material wood = CreateOrUpdateMaterial("Tool_Wood", new Color(0.45f, 0.25f, 0.10f));
            Material metal = CreateOrUpdateMaterial("Tool_Metal", new Color(0.72f, 0.72f, 0.68f));
            Material darkMetal = CreateOrUpdateMaterial("Tool_DarkMetal", new Color(0.25f, 0.25f, 0.25f));
            Material waterBlue = CreateOrUpdateMaterial("Tool_WaterBlue", new Color(0.18f, 0.50f, 0.95f));
            Material line = CreateOrUpdateMaterial("Tool_Line", new Color(0.92f, 0.88f, 0.72f));

            CreateAxe(wood, metal);
            CreatePickaxe(wood, metal);
            CreateHoe(wood, metal);
            CreateShovel(wood, metal);
            CreateHammer(wood, darkMetal);
            CreateWateringCan(waterBlue, metal);
            CreateFishingRod(wood, line);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldToolAssetBuilder] Built temporary tool assets: {PrefabFolder}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Arts/Character/Player", "Tools");
            EnsureFolder(ToolRoot, "Prefabs");
            EnsureFolder(ToolRoot, "Materials");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material CreateOrUpdateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.35f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateAxe(Material wood, Material metal)
        {
            GameObject root = CreateRoot("Tool_Axe");
            CreateCylinder("Handle", root.transform, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.025f, 0.26f, 0.025f), wood);
            CreateCube("Blade", root.transform, new Vector3(0.08f, 0.50f, 0f), new Vector3(0f, 0f, 12f), new Vector3(0.16f, 0.10f, 0.025f), metal);
            CreateCube("BladeTip", root.transform, new Vector3(0.15f, 0.50f, 0f), new Vector3(0f, 0f, 28f), new Vector3(0.06f, 0.13f, 0.025f), metal);
            SavePrefab(root, "Tool_Axe");
        }

        private static void CreatePickaxe(Material wood, Material metal)
        {
            GameObject root = CreateRoot("Tool_Pickaxe");
            CreateCylinder("Handle", root.transform, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.024f, 0.27f, 0.024f), wood);
            CreateCube("Head", root.transform, new Vector3(0f, 0.53f, 0f), Vector3.zero, new Vector3(0.24f, 0.035f, 0.035f), metal);
            CreateCube("LeftPoint", root.transform, new Vector3(-0.14f, 0.54f, 0f), new Vector3(0f, 0f, -25f), new Vector3(0.09f, 0.025f, 0.03f), metal);
            CreateCube("RightPoint", root.transform, new Vector3(0.14f, 0.54f, 0f), new Vector3(0f, 0f, 25f), new Vector3(0.09f, 0.025f, 0.03f), metal);
            SavePrefab(root, "Tool_Pickaxe");
        }

        private static void CreateHoe(Material wood, Material metal)
        {
            GameObject root = CreateRoot("Tool_Hoe");
            CreateCylinder("Handle", root.transform, new Vector3(0f, 0.24f, 0f), Vector3.zero, new Vector3(0.024f, 0.28f, 0.024f), wood);
            CreateCube("Neck", root.transform, new Vector3(0.06f, 0.52f, 0f), Vector3.zero, new Vector3(0.09f, 0.025f, 0.025f), metal);
            CreateCube("Blade", root.transform, new Vector3(0.13f, 0.46f, 0f), new Vector3(0f, 0f, -8f), new Vector3(0.10f, 0.14f, 0.025f), metal);
            SavePrefab(root, "Tool_Hoe");
        }

        private static void CreateShovel(Material wood, Material metal)
        {
            GameObject root = CreateRoot("Tool_Shovel");
            CreateCylinder("Handle", root.transform, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.024f, 0.27f, 0.024f), wood);
            CreateCube("Blade", root.transform, new Vector3(0f, 0.54f, 0f), Vector3.zero, new Vector3(0.11f, 0.13f, 0.035f), metal);
            CreateCube("Tip", root.transform, new Vector3(0f, 0.61f, 0f), new Vector3(0f, 0f, 45f), new Vector3(0.08f, 0.08f, 0.035f), metal);
            SavePrefab(root, "Tool_Shovel");
        }

        private static void CreateHammer(Material wood, Material metal)
        {
            GameObject root = CreateRoot("Tool_Hammer");
            CreateCylinder("Handle", root.transform, new Vector3(0f, 0.23f, 0f), Vector3.zero, new Vector3(0.026f, 0.25f, 0.026f), wood);
            CreateCube("Head", root.transform, new Vector3(0f, 0.50f, 0f), Vector3.zero, new Vector3(0.22f, 0.08f, 0.07f), metal);
            SavePrefab(root, "Tool_Hammer");
        }

        private static void CreateWateringCan(Material blue, Material metal)
        {
            GameObject root = CreateRoot("Tool_WateringCan");
            CreateCube("Body", root.transform, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.20f, 0.16f, 0.13f), blue);
            CreateCylinder("Top", root.transform, new Vector3(0f, 0.37f, 0f), Vector3.zero, new Vector3(0.045f, 0.025f, 0.045f), metal);
            CreateCylinder("Spout", root.transform, new Vector3(0.16f, 0.29f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.025f, 0.12f, 0.025f), metal);
            CreateCube("HandleTop", root.transform, new Vector3(-0.12f, 0.33f, 0f), Vector3.zero, new Vector3(0.04f, 0.12f, 0.03f), metal);
            CreateCube("HandleBottom", root.transform, new Vector3(-0.12f, 0.18f, 0f), Vector3.zero, new Vector3(0.04f, 0.10f, 0.03f), metal);
            SavePrefab(root, "Tool_WateringCan");
        }

        private static void CreateFishingRod(Material wood, Material line)
        {
            GameObject root = CreateRoot("Tool_FishingRod");
            CreateCylinder("Rod", root.transform, new Vector3(0f, 0.34f, 0f), new Vector3(0f, 0f, -12f), new Vector3(0.014f, 0.42f, 0.014f), wood);
            CreateCube("Line", root.transform, new Vector3(0.13f, 0.63f, 0f), Vector3.zero, new Vector3(0.008f, 0.30f, 0.008f), line);
            CreateCube("Hook", root.transform, new Vector3(0.13f, 0.47f, 0f), Vector3.zero, new Vector3(0.04f, 0.014f, 0.014f), line);
            SavePrefab(root, "Tool_FishingRod");
        }

        private static GameObject CreateRoot(string name)
        {
            GameObject root = new GameObject(name);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            return CreatePrimitive(name, PrimitiveType.Cube, parent, position, euler, scale, material);
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            return CreatePrimitive(name, PrimitiveType.Cylinder, parent, position, euler, scale, material);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }

        private static void SavePrefab(GameObject root, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
