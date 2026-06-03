#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class TileTopMaterialAutoRunner
    {
        private const string RunFlagPath = "Temp/RunTileTopMaterials.flag";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (!File.Exists(RunFlagPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            File.Delete(RunFlagPath);
            TileTopMaterialCreator.CreateAll();
            Debug.Log("Tile top material auto-run completed.");
        }
    }
}

#endif
