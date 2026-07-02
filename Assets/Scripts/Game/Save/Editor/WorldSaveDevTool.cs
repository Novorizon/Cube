using Game;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class WorldSaveDevTool
    {
        [MenuItem("Debug/Delete Local Save File")]
        public static void DeleteLocalSaveFile()
        {
            bool suppressCurrentSessionSave = Application.isPlaying;
            if (StorageManager.Instance.DeleteSaveFile(suppressCurrentSessionSave))
            {
                if (Application.isPlaying)
                {
                    Debug.Log("[World Dev] Save file deleted. Stop and enter Play Mode again to reload a clean world.");
                }
            }
        }

        [MenuItem("Debug/Log Local Save Path")]
        public static void LogLocalSavePath()
        {
            Debug.Log($"[World Dev] Save path: {StorageManager.Instance.SavePath}");
        }
    }
}
