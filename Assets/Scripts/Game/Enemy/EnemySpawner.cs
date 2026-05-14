using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class EnemySpawner : Singleton<EnemySpawner>
    {
        private Transform enemyRoot;
        private readonly RuntimeMapAStarPathFinder pathFinder = new RuntimeMapAStarPathFinder();

        public bool Initialize()
        {
            EnsureEnemyRoot();
            return true;
        }

        public bool SpawnEnemyFromFirstSpawn(int npcId)
        {
            IReadOnlyList<Vector3Int> spawnPoints = MapManager.Instance.SpawnPoints;

            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("Spawn enemy failed. No spawn point.");
                return false;
            }

            if (!MapManager.Instance.TryGetGoalPoint(out Vector3Int goalPoint))
            {
                Debug.LogWarning("Spawn enemy failed. No goal point.");
                return false;
            }

            return SpawnEnemy(npcId, spawnPoints[0], goalPoint);
        }

        public bool SpawnEnemyFromRandomSpawn(int npcId)
        {
            IReadOnlyList<Vector3Int> spawnPoints = MapManager.Instance.SpawnPoints;

            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("Spawn enemy failed. No spawn point.");
                return false;
            }

            if (!MapManager.Instance.TryGetGoalPoint(out Vector3Int goalPoint))
            {
                Debug.LogWarning("Spawn enemy failed. No goal point.");
                return false;
            }

            int index = Random.Range(0, spawnPoints.Count);
            return SpawnEnemy(npcId, spawnPoints[index], goalPoint);
        }

        public bool SpawnEnemy(int npcId, Vector3Int spawnCoord, Vector3Int goalCoord)
        {
            if (!NpcManager.Instance.TryGetNpc(npcId, out Npc config))
            {
                Debug.LogWarning($"Spawn enemy failed. Missing npc config: {npcId}");
                return false;
            }

            if (config.Kind != (int)GameEntityKind.Actor || config.ActorType != (int)ActorType.Enemy)
            {
                Debug.LogWarning($"Spawn enemy failed. Npc is not enemy. Id: {npcId}, Kind: {config.Kind}, ActorType: {config.ActorType}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.PrefabLocation))
            {
                Debug.LogWarning($"Spawn enemy failed. Empty prefab location. Id: {npcId}");
                return false;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(config.PrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Spawn enemy failed. Missing prefab. Id: {npcId}, Location: {config.PrefabLocation}");
                return false;
            }

            bool pathFound = pathFinder.TryFindPath(spawnCoord, goalCoord, out List<Vector3Int> path);

            if (!pathFound || path == null || path.Count == 0)
            {
                Debug.LogWarning($"Spawn enemy failed. No path. Spawn: {spawnCoord}, Goal: {goalCoord}");
                return false;
            }

            Vector3 spawnPosition = GetEnemyWorldPosition(spawnCoord);
            GameObject instance = GameObject.Instantiate(prefab, spawnPosition, Quaternion.identity, enemyRoot);
            instance.name = $"{npcId}_{config.Name}_Enemy";

            Enemy enemy = instance.GetComponent<Enemy>();

            if (enemy == null)
            {
                enemy = instance.AddComponent<Enemy>();
            }

            enemy.InitializeRaw(config, path);
            EnemyManager.Instance.Register(enemy);

            return true;
        }

        private Vector3 GetEnemyWorldPosition(Vector3Int coord)
        {
            Vector3 tilePosition = MapManager.Instance.GetTileWorldPosition(coord);
            float tileSize = MapManager.Instance.TileSize;

            return tilePosition + Vector3.up * tileSize;
        }

        private void EnsureEnemyRoot()
        {
            GameObject rootObject = GameObject.Find("EnemyRoot");

            if (rootObject == null)
            {
                rootObject = new GameObject("EnemyRoot");
                rootObject.transform.position = Vector3.zero;
            }

            enemyRoot = rootObject.transform;
        }
    }
}
