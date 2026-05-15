using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class WaveManager : Singleton<WaveManager>
    {
        private readonly List<WaveConfig> waveConfigs = new List<WaveConfig>();

        private int waveGroupId;
        private int currentWaveIndex;
        private int currentConfigIndex;
        private int currentWaveSpawnedCount;
        private int aliveEnemyCount;

        private float spawnTimer;
        private bool initialized;
        private bool running;
        private bool currentWaveAllSpawned;
        private bool allWavesFinished;

        public int CurrentWave
        {
            get
            {
                return currentWaveIndex;
            }
        }

        public int MaxWave
        {
            get
            {
                return waveConfigs.Count;
            }
        }

        public int AliveEnemyCount
        {
            get
            {
                return aliveEnemyCount;
            }
        }

        public bool Running
        {
            get
            {
                return running;
            }
        }

        public bool AllWavesFinished
        {
            get
            {
                return allWavesFinished;
            }
        }

        public bool Initialize()
        {
            initialized = true;
            running = false;
            allWavesFinished = false;
            waveConfigs.Clear();
            ResetRuntimeState();

            return true;
        }

        public bool StartWaveGroup(int waveGroupId)
        {
            if (!initialized)
            {
                Initialize();
            }

            this.waveGroupId = waveGroupId;
            waveConfigs.Clear();

            foreach (KeyValuePair<int, WaveConfig> pair in DataManager.Instance.Wave.GetAll())
            {
                WaveConfig config = pair.Value;

                if (config == null)
                {
                    continue;
                }

                if (config.WaveGroupId != waveGroupId)
                {
                    continue;
                }

                waveConfigs.Add(config);
            }

            waveConfigs.Sort(CompareWaveConfig);

            if (waveConfigs.Count == 0)
            {
                Debug.LogWarning($"Start wave group failed. No wave config. waveGroupId: {waveGroupId}");
                ResetRuntimeState();
                NotifyWaveChanged();
                return false;
            }

            ResetRuntimeState();

            running = true;
            allWavesFinished = false;

            StartCurrentWave();

            Debug.Log($"Wave group started. waveGroupId: {waveGroupId}, waveCount: {waveConfigs.Count}");

            return true;
        }

        public void Stop()
        {
            running = false;
        }

        public void Clear()
        {
            running = false;
            allWavesFinished = false;
            waveConfigs.Clear();
            ResetRuntimeState();

            NotifyWaveChanged();
        }

        public void Update(float deltaTime)
        {
            if (!initialized || !running || allWavesFinished)
            {
                return;
            }

            if (currentConfigIndex < 0 || currentConfigIndex >= waveConfigs.Count)
            {
                return;
            }

            WaveConfig config = waveConfigs[currentConfigIndex];

            if (!currentWaveAllSpawned)
            {
                UpdateSpawn(config, deltaTime);
                return;
            }

            if (aliveEnemyCount <= 0)
            {
                StartNextWaveOrFinish();
            }
        }

        public void NotifyEnemyKilled(Npc npc)
        {
            if (npc == null)
            {
                return;
            }

            if (npc.ActorType != ActorType.Enemy)
            {
                return;
            }

            aliveEnemyCount--;

            if (aliveEnemyCount < 0)
            {
                aliveEnemyCount = 0;
            }

            Debug.Log($"Wave enemy killed. aliveEnemyCount: {aliveEnemyCount}");

            NotifyWaveChanged();
        }

        private void UpdateSpawn(WaveConfig config, float deltaTime)
        {
            if (config == null)
            {
                return;
            }

            if (currentWaveSpawnedCount >= config.Count)
            {
                currentWaveAllSpawned = true;
                Debug.Log($"Wave all spawned. wave: {currentWaveIndex}, count: {currentWaveSpawnedCount}");
                return;
            }

            spawnTimer -= deltaTime;

            if (spawnTimer > 0f)
            {
                return;
            }

            bool spawned = SpawnNpc(config);

            if (spawned)
            {
                currentWaveSpawnedCount++;
                aliveEnemyCount++;
                NotifyWaveChanged();

                Debug.Log($"Wave spawn npc. wave: {currentWaveIndex}, npcConfigId: {config.NpcConfigId}, spawned: {currentWaveSpawnedCount}/{config.Count}, alive: {aliveEnemyCount}");
            }

            spawnTimer = Mathf.Max(0.05f, config.Interval);
        }

        private bool SpawnNpc(WaveConfig config)
        {
            WaveSpawnMode spawnMode = (WaveSpawnMode)config.SpawnMode;

            switch (spawnMode)
            {
                case WaveSpawnMode.FirstSpawnPoint:
                    return NpcManager.Instance.SpawnFromFirstSpawn(config.NpcConfigId);

                case WaveSpawnMode.RandomSpawnPoint:
                    return NpcManager.Instance.SpawnFromRandomSpawn(config.NpcConfigId);

                default:
                    Debug.LogWarning($"Unknown wave spawn mode: {config.SpawnMode}, use first spawn point instead.");
                    return NpcManager.Instance.SpawnFromFirstSpawn(config.NpcConfigId);
            }
        }

        private void StartCurrentWave()
        {
            if (currentConfigIndex < 0 || currentConfigIndex >= waveConfigs.Count)
            {
                FinishAllWaves();
                return;
            }

            WaveConfig config = waveConfigs[currentConfigIndex];

            currentWaveIndex = config.WaveIndex;
            currentWaveSpawnedCount = 0;
            currentWaveAllSpawned = false;
            spawnTimer = Mathf.Max(0f, config.StartDelay);

            NotifyWaveChanged();

            Debug.Log($"Wave started. waveGroupId: {waveGroupId}, wave: {currentWaveIndex}/{MaxWave}, npcConfigId: {config.NpcConfigId}, count: {config.Count}");
        }

        private void StartNextWaveOrFinish()
        {
            currentConfigIndex++;

            if (currentConfigIndex >= waveConfigs.Count)
            {
                FinishAllWaves();
                return;
            }

            StartCurrentWave();
        }

        private void FinishAllWaves()
        {
            running = false;
            allWavesFinished = true;

            NotifyWaveChanged();

            Debug.Log("All waves finished. Victory.");
        }

        private void ResetRuntimeState()
        {
            currentConfigIndex = 0;
            currentWaveIndex = 0;
            currentWaveSpawnedCount = 0;
            aliveEnemyCount = 0;
            spawnTimer = 0f;
            currentWaveAllSpawned = false;
        }

        private void NotifyWaveChanged()
        {
            WaveMessage message = new WaveMessage();
            message.CurrentWave = currentWaveIndex;
            message.MaxWave = MaxWave;

            Messager.Instance.Notify(BattleMessageTopic.WaveChanged, message);
        }

        private int CompareWaveConfig(WaveConfig a, WaveConfig b)
        {
            if (a == null && b == null)
            {
                return 0;
            }

            if (a == null)
            {
                return -1;
            }

            if (b == null)
            {
                return 1;
            }

            int waveCompare = a.WaveIndex.CompareTo(b.WaveIndex);

            if (waveCompare != 0)
            {
                return waveCompare;
            }

            return a.Id.CompareTo(b.Id);
        }
    }
}
