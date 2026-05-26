using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class WaveManager : Singleton<WaveManager>
    {
        private readonly List<WaveConfig> waveConfigs = new List<WaveConfig>();

        private int currentWaveIndex;
        private int currentConfigIndex;
        private int currentWaveSpawnedCount;
        private int aliveEnemyCount;
        private int killedEnemyCount;
        private int totalEnemyCount;

        private float spawnTimer;
        private bool initialized;
        private bool running;
        private bool currentWaveAllSpawned;
        private bool allWavesFinished;

        /// <summary>
        /// true:
        ///     每一波都必须等敌人全部清空，才进入下一波。
        /// false:
        ///     当前波全部生成完后，直接进入下一波。
        ///     下一波会按自己的 startDelay 延迟开始生成。
        ///     最后一波仍然会等待全部敌人清空后 Victory。
        /// </summary>
        private bool waitAllEnemiesKilledBeforeNextWave;

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

        public int KilledEnemyCount
        {
            get
            {
                return killedEnemyCount;
            }
        }

        public int TotalEnemyCount
        {
            get
            {
                return totalEnemyCount;
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

        public bool WaitAllEnemiesKilledBeforeNextWave
        {
            get
            {
                return waitAllEnemiesKilledBeforeNextWave;
            }
        }

        public bool Initialize()
        {
            initialized = true;
            running = false;
            allWavesFinished = false;
            waitAllEnemiesKilledBeforeNextWave = false;
            waveConfigs.Clear();
            ResetRuntimeState();

            return true;
        }

        public void SetWaitAllEnemiesKilledBeforeNextWave(bool wait)
        {
            waitAllEnemiesKilledBeforeNextWave = wait;
        }

        public bool StartWave()
        {
            if (!initialized)
            {
                Initialize();
            }

            waveConfigs.Clear();

            if (DataManager.Instance.Wave == null)
            {
                Debug.LogError("Start wave failed. Wave table is not loaded.");
                ResetRuntimeState();
                NotifyWaveChanged();
                return false;
            }

            foreach (KeyValuePair<int, WaveConfig> pair in DataManager.Instance.Wave.GetAll())
            {
                WaveConfig config = pair.Value;

                if (config == null)
                {
                    continue;
                }

                waveConfigs.Add(config);
            }

            waveConfigs.Sort(CompareWaveConfig);

            if (waveConfigs.Count == 0)
            {
                Debug.LogWarning("Start wave failed. No wave config.");
                ResetRuntimeState();
                NotifyWaveChanged();
                return false;
            }

            ResetRuntimeState();
            totalEnemyCount = CalculateTotalEnemyCount();

            running = true;
            allWavesFinished = false;

            StartCurrentWave();

            Debug.Log($"Wave started. waveCount: {waveConfigs.Count}");

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

            UpdateAfterCurrentWaveAllSpawned();
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

            // Ignore late death notifications after defeat has already stopped wave simulation.
            if (!running && !allWavesFinished)
            {
                return;
            }

            aliveEnemyCount--;
            killedEnemyCount++;

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

                Debug.Log($"Wave spawn npc. wave: {currentWaveIndex}/{MaxWave}, waveConfigId: {config.Id}, npcConfigId: {config.NpcConfigId}, spawned: {currentWaveSpawnedCount}/{config.Count}, alive: {aliveEnemyCount}");
            }

            spawnTimer = Mathf.Max(0.05f, config.Interval);
        }

        private void UpdateAfterCurrentWaveAllSpawned()
        {
            if (IsLastWave())
            {
                if (aliveEnemyCount <= 0)
                {
                    FinishAllWaves();
                }

                return;
            }

            if (waitAllEnemiesKilledBeforeNextWave)
            {
                if (aliveEnemyCount <= 0)
                {
                    StartNextWaveOrFinish();
                }

                return;
            }

            StartNextWaveOrFinish();
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

            currentWaveIndex = currentConfigIndex + 1;
            currentWaveSpawnedCount = 0;
            currentWaveAllSpawned = false;
            spawnTimer = Mathf.Max(0f, config.StartDelay);

            NotifyWaveChanged();

            Debug.Log($"Wave started. wave: {currentWaveIndex}/{MaxWave}, waveConfigId: {config.Id}, npcConfigId: {config.NpcConfigId}, count: {config.Count}, startDelay: {config.StartDelay}");
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
            BattleFlowManager.Instance.CompleteVictory();
        }

        private bool IsLastWave()
        {
            return currentConfigIndex >= waveConfigs.Count - 1;
        }

        private void ResetRuntimeState()
        {
            currentConfigIndex = 0;
            currentWaveIndex = 0;
            currentWaveSpawnedCount = 0;
            aliveEnemyCount = 0;
            killedEnemyCount = 0;
            totalEnemyCount = 0;
            spawnTimer = 0f;
            currentWaveAllSpawned = false;
        }

        private void NotifyWaveChanged()
        {
            WaveMessage message = new WaveMessage();
            message.CurrentWave = currentWaveIndex;
            message.MaxWave = MaxWave;
            message.AliveEnemyCount = aliveEnemyCount;
            message.TotalEnemyCount = totalEnemyCount;
            message.KilledEnemyCount = killedEnemyCount;
            message.CurrentWaveSpawnedCount = currentWaveSpawnedCount;
            message.CurrentWaveTotalCount = GetCurrentWaveTotalCount();

            Messager.Instance.Notify(BattleMessageTopic.WaveChanged, message);
        }

        private int CalculateTotalEnemyCount()
        {
            int total = 0;
            for (int i = 0; i < waveConfigs.Count; i++)
            {
                WaveConfig config = waveConfigs[i];
                if (config != null && config.Count > 0)
                {
                    total += config.Count;
                }
            }

            return total;
        }

        private int GetCurrentWaveTotalCount()
        {
            if (currentConfigIndex < 0 || currentConfigIndex >= waveConfigs.Count)
            {
                return 0;
            }

            WaveConfig config = waveConfigs[currentConfigIndex];
            return config != null ? Mathf.Max(0, config.Count) : 0;
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

            return a.Id.CompareTo(b.Id);
        }
    }
}