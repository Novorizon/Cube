#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class MapArtPrototypeAutoRunner
    {
        private const string RunFlagPath = "Temp/RunMapArtPrototype.flag";

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
            MapArtPrototypeCreator.FullSetupAndPreview();
            Debug.Log("Map art prototype auto-run completed.");
        }
    }
}

#endif
