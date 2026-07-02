using UnityEngine;

namespace Game
{
    public sealed class NpcSpawnTestController : MonoBehaviour
    {
        [SerializeField]
        private int testMapId = 30950001;

        [SerializeField]
        private int testNpcConfigId = 41000001;

        [SerializeField]
        private KeyCode loadMapAndSpawnKey = KeyCode.F5;

        [SerializeField]
        private KeyCode spawnKey = KeyCode.F6;

        private void Update()
        {
            if (Input.GetKeyDown(loadMapAndSpawnKey))
            {
                LoadMap();
                SpawnNpc();
                return;
            }

            if (Input.GetKeyDown(spawnKey))
            {
                SpawnNpc();
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

        private void SpawnNpc()
        {
            bool success = NpcManager.Instance.SpawnFromFirstSpawn(testNpcConfigId);

            if (!success)
            {
                Debug.LogWarning($"NpcSpawnTestController spawn failed. NpcConfigId: {testNpcConfigId}");
                return;
            }

            Debug.Log($"NpcSpawnTestController spawn success. NpcConfigId: {testNpcConfigId}");
        }
    }
}
