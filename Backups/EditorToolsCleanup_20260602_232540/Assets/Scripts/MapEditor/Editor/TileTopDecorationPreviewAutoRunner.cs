#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class TileTopDecorationPreviewAutoRunner
    {
        private const string RunFlagPath = "Temp/RunTileTopDecorationPreview.flag";

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
            TileTopDecorationPreviewCreator.CreatePreviewGrid();
            Debug.Log("Tile top decoration preview auto-run completed.");
        }
    }
}

#endif
