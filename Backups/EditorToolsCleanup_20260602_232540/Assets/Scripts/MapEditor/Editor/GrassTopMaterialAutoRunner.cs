#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    internal static class GrassTopMaterialAutoRunner
    {
        private const string RunFlagPath = "Temp/RunGrassTopMaterial.flag";

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
            GrassTopMaterialCreator.CreateAndAssignMaterial();
            Debug.Log("Grass top material auto-run completed.");
        }
    }
}

#endif
