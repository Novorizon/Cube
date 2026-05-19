#if UNITY_EDITOR
using UnityEditor;

namespace Game.EditorTools
{
    internal static class TowerDefenseUIFolderUtility
    {
        private static readonly string[] FolderPaths =
        {
            "Assets/GameRes",
            "Assets/GameRes/UI",
            "Assets/GameRes/UI/TowerDefense",
            "Assets/GameRes/UI/TowerDefense/Sprites",
            "Assets/GameRes/Prefabs",
            "Assets/GameRes/Prefabs/UI",
            "Assets/GameRes/Prefabs/UI/TowerDefense"
        };

        [InitializeOnLoadMethod]
        private static void EnsureFoldersOnLoad()
        {
            EditorApplication.delayCall += EnsureFolders;
        }

        [MenuItem("Tools/Game/Tower Defense UI/Ensure Folders")]
        public static void EnsureFolders()
        {
            for (int i = 0; i < FolderPaths.Length; i++)
            {
                EnsureFolderPath(FolderPaths[i]);
            }

            AssetDatabase.Refresh();
        }

        private static void EnsureFolderPath(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
#endif
