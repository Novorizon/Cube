using UnityEngine;

namespace Game
{
    public sealed class EnemyUpdateDriver : MonoBehaviour
    {
        private void Update()
        {
            EnemyManager.Instance.UpdateEnemies(Time.deltaTime);
        }
    }
}
