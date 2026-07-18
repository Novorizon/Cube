using System.Collections.Generic;

namespace Game
{
    public partial class MapManager
    {
        public void MarkMapObjectRemoved(int objectId)
        {
            if (objectId <= 0 || currentMapId <= 0)
            {
                return;
            }

            removedMapObjectKeys.Add(MakeRemovedMapObjectKey(currentMapId, objectId));
            StorageManager.Instance.MarkDirty();
        }

        public SaveRemovedMapObjectData[] CreateRemovedMapObjectSaveData()
        {
            List<SaveRemovedMapObjectData> result = new List<SaveRemovedMapObjectData>();
            foreach (string key in removedMapObjectKeys)
            {
                if (!TryParseRemovedMapObjectKey(key, out int mapId, out int objectId))
                {
                    continue;
                }

                result.Add(new SaveRemovedMapObjectData
                {
                    MapId = mapId,
                    ObjectId = objectId,
                });
            }

            return result.ToArray();
        }

        public void LoadRemovedMapObjectSaveData(IReadOnlyList<SaveRemovedMapObjectData> removedObjects)
        {
            removedMapObjectKeys.Clear();
            if (removedObjects == null)
            {
                return;
            }

            for (int i = 0; i < removedObjects.Count; i++)
            {
                SaveRemovedMapObjectData removed = removedObjects[i];
                if (removed == null || removed.MapId <= 0 || removed.ObjectId <= 0)
                {
                    continue;
                }

                removedMapObjectKeys.Add(MakeRemovedMapObjectKey(removed.MapId, removed.ObjectId));
            }
        }

        private void ApplyRemovedMapObjects(MapData mapData)
        {
            if (mapData == null || mapData.Objects == null || currentMapId <= 0 || removedMapObjectKeys.Count == 0)
            {
                return;
            }

            mapData.Objects.RemoveAll(mapObject =>
                mapObject != null &&
                mapObject.ObjectId > 0 &&
                removedMapObjectKeys.Contains(MakeRemovedMapObjectKey(currentMapId, mapObject.ObjectId)));
        }

        private static string MakeRemovedMapObjectKey(int mapId, int objectId)
        {
            return $"{mapId}:{objectId}";
        }

        private static bool TryParseRemovedMapObjectKey(string key, out int mapId, out int objectId)
        {
            mapId = 0;
            objectId = 0;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int separator = key.IndexOf(':');
            if (separator <= 0 || separator >= key.Length - 1)
            {
                return false;
            }

            return int.TryParse(key.Substring(0, separator), out mapId) &&
                   int.TryParse(key.Substring(separator + 1), out objectId);
        }
    }
}
