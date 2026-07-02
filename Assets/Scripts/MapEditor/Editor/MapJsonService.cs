#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class MapJsonService
    {
        public static string MapJsonDirectory => Path.Combine(Application.dataPath, "Data", "Map");

        public static void EnsureDirectory()
        {
            if (!Directory.Exists(MapJsonDirectory))
            {
                Directory.CreateDirectory(MapJsonDirectory);
            }
        }

        public static string GetDefaultMapJsonPath(int mapId)
        {
            EnsureDirectory();
            return Path.Combine(MapJsonDirectory, mapId + ".json");
        }

        public static string OpenImportPanel()
        {
            EnsureDirectory();
            return EditorUtility.OpenFilePanel("Import Map Json", MapJsonDirectory, "json");
        }

        public static MapData Load(string path)
        {
            return JsonConvert.DeserializeObject<MapData>(File.ReadAllText(path));
        }

        public static bool ConfirmOverwrite(string path)
        {
            return !File.Exists(path) ||
                   EditorUtility.DisplayDialog(
                       "Overwrite Map Json",
                       $"Map json already exists:\n{path}\n\nOverwrite it?",
                       "Overwrite",
                       "Cancel");
        }

        public static void Save(MapData mapData, string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(mapData, Formatting.Indented));
            AssetDatabase.Refresh();
        }
    }
}
#endif
