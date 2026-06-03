#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class TileTopPreviewAutoRunner
    {
        private const string RunFlagPath = "Temp/RunTileTopPreview.flag";

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
            TileTopMaterialPreviewCreator.CreatePreviewGrid();
            Debug.Log("Tile top preview auto-run completed.");
        }
    }
}

#endif
