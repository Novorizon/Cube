using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class NpcManager : Singleton<NpcManager>
    {
        private readonly List<Npc> activeNpcs = new List<Npc>();
        private readonly MapPathFinder pathFinder = new MapPathFinder();

        private Transform npcRoot;
        private bool initialized;

        public IReadOnlyList<Npc> ActiveNpcs
        {
            get
            {
                return activeNpcs;
            }
        }

        public bool Initialize()
        {
            EnsureNpcRoot();
            initialized = true;
            return true;
        }

        public void Update(float deltaTime)
        {
            if (!initialized)
            {
                return;
            }

            for (int i = activeNpcs.Count - 1; i >= 0; i--)
            {
                Npc npc = activeNpcs[i];

                if (npc == null)
                {
                    activeNpcs.RemoveAt(i);
                    continue;
                }

                UpdateNpc(npc, deltaTime);
            }
        }

        public bool SpawnFromFirstSpawn(int npcConfigId)
        {
            IReadOnlyList<Vector3Int> spawnPoints = MapManager.Instance.SpawnPoints;

            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("Spawn npc failed. No spawn point.");
                return false;
            }

            if (!MapManager.Instance.TryGetGoalPoint(out Vector3Int goalPoint))
            {
                Debug.LogWarning("Spawn npc failed. No goal point.");
                return false;
            }

            return SpawnToTarget(npcConfigId, spawnPoints[0], goalPoint);
        }

        public bool SpawnFromRandomSpawn(int npcConfigId)
        {
            IReadOnlyList<Vector3Int> spawnPoints = MapManager.Instance.SpawnPoints;

            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning("Spawn npc failed. No spawn point.");
                return false;
            }

            if (!MapManager.Instance.TryGetGoalPoint(out Vector3Int goalPoint))
            {
                Debug.LogWarning("Spawn npc failed. No goal point.");
                return false;
            }

            int index = Random.Range(0, spawnPoints.Count);
            return SpawnToTarget(npcConfigId, spawnPoints[index], goalPoint);
        }

        public bool SpawnToTarget(int npcConfigId, Vector3Int spawnCoord, Vector3Int targetCoord)
        {
            if (!DataManager.Instance.TryGetNpcConfig(npcConfigId, out NpcConfig config))
            {
                Debug.LogWarning($"Spawn npc failed. Missing npc config: {npcConfigId}");
                return false;
            }

            if (config.Kind != (int)GameEntityKind.Actor)
            {
                Debug.LogWarning($"Spawn npc failed. Config is not Actor. Id: {npcConfigId}, Kind: {config.Kind}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(config.PrefabLocation))
            {
                Debug.LogWarning($"Spawn npc failed. Empty prefab location. Id: {npcConfigId}");
                return false;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(config.PrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Spawn npc failed. Missing prefab. Id: {npcConfigId}, Location: {config.PrefabLocation}");
                return false;
            }

            bool pathFound = pathFinder.TryFindPath(spawnCoord, targetCoord, out List<Vector3Int> path);

            if (!pathFound || path == null || path.Count == 0)
            {
                Debug.LogWarning($"Spawn npc failed. No path. Spawn: {spawnCoord}, Target: {targetCoord}");
                return false;
            }

            Vector3 spawnPosition = GetWorldPosition(spawnCoord);
            GameObject instance = GameObject.Instantiate(prefab, spawnPosition, Quaternion.identity, npcRoot);
            instance.name = $"{npcConfigId}_{config.Name}_Npc";

            Npc npc = instance.GetComponent<Npc>();

            if (npc == null)
            {
                npc = instance.AddComponent<Npc>();
            }

            NpcData data = new NpcData();
            data.Initialize(config, path);

            npc.InitializeRaw(config, data);
            Register(npc);

            Debug.Log($"Spawn npc success. Id: {npcConfigId}, Name: {config.Name}, ActorType: {config.ActorType}, Spawn: {spawnCoord}, Target: {targetCoord}, PathCount: {path.Count}");

            return true;
        }

        public void Register(Npc npc)
        {
            if (npc == null)
            {
                return;
            }

            if (activeNpcs.Contains(npc))
            {
                return;
            }

            activeNpcs.Add(npc);
        }

        public void Unregister(Npc npc)
        {
            if (npc == null)
            {
                return;
            }

            activeNpcs.Remove(npc);
        }

        public void Remove(Npc npc)
        {
            if (npc == null)
            {
                return;
            }

            Unregister(npc);
            GameObject.Destroy(npc.gameObject);
        }

        public void Clear()
        {
            for (int i = activeNpcs.Count - 1; i >= 0; i--)
            {
                Npc npc = activeNpcs[i];

                if (npc != null)
                {
                    GameObject.Destroy(npc.gameObject);
                }
            }

            activeNpcs.Clear();
        }

        private void UpdateNpc(Npc npc, float deltaTime)
        {
            if (npc == null)
            {
                return;
            }

            NpcData data = npc.Data;

            if (data == null)
            {
                return;
            }

            if (!data.Moving)
            {
                return;
            }

            if (data.ReachedGoal)
            {
                return;
            }

            if (data.Path.Count == 0)
            {
                data.Moving = false;
                return;
            }

            if (data.PathIndex >= data.Path.Count)
            {
                OnNpcReachTarget(npc);
                return;
            }

            Vector3Int targetCoord = data.Path[data.PathIndex];
            Vector3 targetPosition = GetWorldPosition(targetCoord);
            Vector3 currentPosition = npc.transform.position;

            float step = data.MoveSpeed * deltaTime;
            npc.transform.position = Vector3.MoveTowards(currentPosition, targetPosition, step);

            Vector3 direction = targetPosition - currentPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                npc.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            float distance = Vector3.Distance(npc.transform.position, targetPosition);

            if (distance > 0.01f)
            {
                return;
            }

            data.PathIndex++;

            if (data.PathIndex >= data.Path.Count)
            {
                OnNpcReachTarget(npc);
            }
        }

        private void OnNpcReachTarget(Npc npc)
        {
            if (npc == null || npc.Data == null)
            {
                return;
            }

            if (npc.Data.ReachedGoal)
            {
                return;
            }

            npc.Data.Moving = false;
            npc.Data.ReachedGoal = true;

            if (npc.ActorType == ActorType.Enemy)
            {
                OnEnemyReachGoal(npc);
                return;
            }

            Debug.Log($"Npc reached target. Id: {npc.Config?.Id}, ActorType: {npc.ActorType}");
        }

        private void OnEnemyReachGoal(Npc npc)
        {
            int damage = npc.Data != null ? npc.Data.DamageToBase : 0;

            Debug.Log($"Enemy reached goal. Id: {npc.Config?.Id}, DamageToBase: {damage}");

            BaseManager.Instance.TakeDamage(damage);

            Remove(npc);
        }

        private Vector3 GetWorldPosition(Vector3Int coord)
        {
            if (MapManager.Instance.TryGetTileView(coord, out TileView tileView))
            {
                return tileView.transform.position + Vector3.up * 0.6f;
            }

            return MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * 0.6f;
        }

        private void EnsureNpcRoot()
        {
            GameObject rootObject = GameObject.Find("NpcRoot");

            if (rootObject == null)
            {
                rootObject = new GameObject("NpcRoot");
                rootObject.transform.position = Vector3.zero;
            }

            npcRoot = rootObject.transform;
        }
    }
}
