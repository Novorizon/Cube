#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UI.Editor
{
    public static class UIFrameworkMenu
    {
        [MenuItem("Tools/UI Framework Pro/Create UIBootstrap in Scene")]
        public static void CreateBootstrap()
        {
            var go = new GameObject("UIBootstrap");
            go.AddComponent<UI.UIBootstrap>();
            Selection.activeObject = go;
        }

        [MenuItem("Tools/UI Framework Pro/Create UISettings Asset")]
        public static void CreateSettings()
        {
            var asset = ScriptableObject.CreateInstance<UI.UISettings>();
            string path = "Assets/Resources/UISettings.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }
    }
}
#endif
