using Game.Framework;
using Newtonsoft.Json;
using UnityEngine;

namespace Game
{
    public partial class MapManager
    {
        private bool LoadMapData(string location)
        {
            TextAsset json = ResourceManager.Instance.LoadTextAsset(location);

            if (json == null)
            {
                Debug.LogError($"Failed to load map json: {location}");
                return false;
            }

            MapData data = JsonConvert.DeserializeObject<MapData>(json.text);

            if (data == null)
            {
                Debug.LogError($"Failed to parse map json: {location}");
                return false;
            }

            data.EnsureRuntimeCollections();
            currentMapId = data.Id;
            ApplyRemovedMapObjects(data);

            currentMap = data;
            RebuildTileIndex();
            RebuildObjectIndex();
            WorldBuildingManager.Instance.RegisterMapObjects();

            return true;
        }
    }
}
