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
        private SavePlayerData loadedPlayer;

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
            loadedPlayer = null;
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

                if (data.Version != SaveVersion.Current)
                {
                    Debug.LogWarning($"Ignore save file because version changed. Save: {data.Version}, Current: {SaveVersion.Current}, Path: {SavePath}");
                    loadedPlayer = null;
                    return false;
                }

                WorldItemManager.Instance.LoadSaveData(data.WorldItems);
                TechManager.Instance.LoadSaveData(data.Tech);
                WorldGatherManager.Instance.LoadSaveData(data.GatherNodes);
                WorldBuildingManager.Instance.LoadSaveData(data.WorldBuildings);
                WorldBuildingManager.Instance.LoadRuntimeUnlockSaveData(data.RuntimeUnlockedBuildingIds);
                FarmManager.Instance.LoadSaveData(data.Farms, data.WorldFarmPlots);
                MapManager.Instance.LoadRemovedMapObjectSaveData(data.RemovedMapObjects);
                ToolKitManager.Instance.LoadSaveData(data.ToolKit);
                CalendarManager.Instance.LoadSaveData(data.Calendar);
                ApplyOfflineCalendarProgress(data.SavedAtUnixTime);
                BagManager.Instance.LoadSaveData(data.Bag);
                loadedPlayer = data.Player;
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
                    SavedAtUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    WorldItems = WorldItemManager.Instance.CreateSaveData(),
                    GatherNodes = WorldGatherManager.Instance.CreateSaveData(),
                    WorldBuildings = WorldBuildingManager.Instance.CreateSaveData(),
                    RuntimeUnlockedBuildingIds = WorldBuildingManager.Instance.CreateRuntimeUnlockSaveData(),
                    Farms = FarmManager.Instance.CreateSaveData(),
                    RemovedMapObjects = MapManager.Instance.CreateRemovedMapObjectSaveData(),
                    ToolKit = ToolKitManager.Instance.CreateSaveData(),
                    Calendar = CalendarManager.Instance.CreateSaveData(),
                    Bag = BagManager.Instance.CreateSaveData(),
                    Tech = TechManager.Instance.CreateSaveData(),
                    Player = WorldGameplayController.Instance != null ? WorldGameplayController.Instance.CreatePlayerSaveData() : loadedPlayer,
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

        public bool TryGetPlayerSaveData(int mapId, out SavePlayerData data)
        {
            data = loadedPlayer;
            return data != null && data.MapId == mapId;
        }

        private static void ApplyOfflineCalendarProgress(long savedAtUnixTime)
        {
            if (savedAtUnixTime <= 0)
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long offlineSeconds = now - savedAtUnixTime;
            if (offlineSeconds <= 0)
            {
                return;
            }

            float secondsPerGameMinute = Mathf.Max(0.01f, CalendarManager.Instance.RealSecondsPerGameMinute);
            long gameMinutes = (long)Math.Floor(offlineSeconds / secondsPerGameMinute);
            if (gameMinutes <= 0)
            {
                return;
            }

            CalendarManager.Instance.AdvanceMinutes((int)Math.Min(int.MaxValue, gameMinutes));
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
                loadedPlayer = null;
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
