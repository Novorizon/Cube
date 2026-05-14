using UnityEngine;

namespace Game
{
    public sealed class NpcSpawnTestController : MonoBehaviour
    {
        [SerializeField]
        private int testMapId = 1;

        [SerializeField]
        private int testNpcId = 1001;

        [SerializeField]
        private bool loadMapOnStart;

        [SerializeField]
        private bool spawnOnStart;

        [SerializeField]
        private KeyCode loadMapAndSpawnKey = KeyCode.F5;

        [SerializeField]
        private KeyCode spawnKey = KeyCode.F6;

        private void Start()
        {
            if (loadMapOnStart)
            {
                LoadMap();
            }

            if (spawnOnStart)
            {
                SpawnEnemy();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(loadMapAndSpawnKey))
            {
                LoadMap();

                SpawnEnemy();
                return;
            }

            if (Input.GetKeyDown(spawnKey))
            {
                SpawnEnemy();
            }
        }

        private void LoadMap()
        {
            bool success = MapManager.Instance.LoadMap(testMapId);

            if (!success)
            {
                Debug.LogError($"NpcSpawnTestController load map failed. MapId: {testMapId}");
                return;
            }

            Debug.Log($"NpcSpawnTestController load map success. MapId: {testMapId}");
        }

        private void SpawnEnemy()
        {
            bool success = EnemySpawner.Instance.SpawnEnemyFromFirstSpawn(testNpcId);

            if (!success)
            {
                Debug.LogWarning($"NpcSpawnTestController spawn failed. NpcId: {testNpcId}");
                return;
            }

            Debug.Log($"NpcSpawnTestController spawn success. NpcId: {testNpcId}");
        }
    }
}
