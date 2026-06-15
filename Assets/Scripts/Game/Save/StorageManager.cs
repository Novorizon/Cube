using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

namespace Game
{
    public sealed class StorageManager
    {
        private const string SaveFileName = "save_0.json";
        private const float AutoSaveDelay = 5f;

        public static StorageManager Instance { get; } = new StorageManager();

        private bool initialized;
        private bool dirty;
        private float dirtyAtTime;
        private bool suppressSaveUntilInitialize;

        private StorageManager()
        {
        }

        public bool Initialized
        {
            get
            {
                return initialized;
            }
        }

        public string SavePath
        {
            get
            {
                return Path.Combine(Application.persistentDataPath, SaveFileName);
            }
        }

        public void Initialize()
        {
            initialized = true;
            dirty = false;
            dirtyAtTime = 0f;
            suppressSaveUntilInitialize = false;
        }

        public void Update()
        {
            if (!dirty)
            {
                return;
            }

            if (Time.unscaledTime - dirtyAtTime < AutoSaveDelay)
            {
                return;
            }

            Save();
        }

        public void MarkDirty()
        {
            if (!initialized)
            {
                return;
            }

            dirty = true;
            dirtyAtTime = Time.unscaledTime;
        }

        public bool Load()
        {
            if (!File.Exists(SavePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                SaveData data = JsonConvert.DeserializeObject<SaveData>(json);
                if (data == null)
                {
                    Debug.LogWarning($"Load save failed. Empty data: {SavePath}");
                    return false;
                }

                WorldItemManager.Instance.LoadSaveData(data.WorldItems);
                WorldGatherManager.Instance.LoadSaveData(data.GatherNodes);
                WorldBuildingManager.Instance.LoadSaveData(data.WorldBuildings);
                FarmManager.Instance.LoadSaveData(data.Farms, data.WorldFarmPlots);
                MapManager.Instance.LoadRemovedMapObjectSaveData(data.RemovedMapObjects);
                dirty = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        public bool Save()
        {
            if (suppressSaveUntilInitialize)
            {
                return false;
            }

            try
            {
                SaveData data = new SaveData
                {
                    Version = SaveVersion.Current,
                    WorldItems = WorldItemManager.Instance.CreateSaveData(),
                    GatherNodes = WorldGatherManager.Instance.CreateSaveData(),
                    WorldBuildings = WorldBuildingManager.Instance.CreateSaveData(),
                    Farms = FarmManager.Instance.CreateSaveData(),
                    RemovedMapObjects = MapManager.Instance.CreateRemovedMapObjectSaveData(),
                };

                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string tempPath = SavePath + ".tmp";
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(tempPath, json);

                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                File.Move(tempPath, SavePath);
                dirty = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        public bool DeleteSaveFile(bool suppressSaveForCurrentSession)
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                }

                string tempPath = SavePath + ".tmp";
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                dirty = false;
                dirtyAtTime = 0f;
                suppressSaveUntilInitialize = suppressSaveForCurrentSession;
                Debug.Log($"Deleted save file: {SavePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }
    }
}
