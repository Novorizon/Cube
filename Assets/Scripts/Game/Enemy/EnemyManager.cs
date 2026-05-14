using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class EnemyManager : Singleton<EnemyManager>
    {
        private readonly List<Enemy> activeEnemies = new List<Enemy>();

        public IReadOnlyList<Enemy> ActiveEnemies
        {
            get
            {
                return activeEnemies;
            }
        }

        public void Register(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (activeEnemies.Contains(enemy))
            {
                return;
            }

            activeEnemies.Add(enemy);
        }

        public void Unregister(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            activeEnemies.Remove(enemy);
        }

        public void UpdateEnemies(float deltaTime)
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = activeEnemies[i];

                if (enemy == null)
                {
                    activeEnemies.RemoveAt(i);
                    continue;
                }

                UpdateEnemyMove(enemy, deltaTime);
            }
        }

        public void RemoveEnemy(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            Unregister(enemy);
            GameObject.Destroy(enemy.gameObject);
        }

        public void Clear()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = activeEnemies[i];

                if (enemy != null)
                {
                    GameObject.Destroy(enemy.gameObject);
                }
            }

            activeEnemies.Clear();
        }

        private void UpdateEnemyMove(Enemy enemy, float deltaTime)
        {
            if (enemy == null)
            {
                return;
            }

            if (!enemy.Moving)
            {
                return;
            }

            if (enemy.ReachedGoal)
            {
                return;
            }

            if (enemy.Path == null || enemy.Path.Count == 0)
            {
                enemy.SetMovingRaw(false);
                return;
            }

            if (enemy.PathIndex >= enemy.Path.Count)
            {
                ReachGoal(enemy);
                return;
            }

            Vector3Int targetCoord = enemy.Path[enemy.PathIndex];
            Vector3 targetPosition = GetEnemyWorldPosition(targetCoord);
            Vector3 currentPosition = enemy.transform.position;

            float step = enemy.MoveSpeed * deltaTime;
            enemy.transform.position = Vector3.MoveTowards(currentPosition, targetPosition, step);

            Vector3 direction = targetPosition - currentPosition;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                enemy.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            float distance = Vector3.Distance(enemy.transform.position, targetPosition);

            if (distance > 0.01f)
            {
                return;
            }

            enemy.SetPathIndexRaw(enemy.PathIndex + 1);

            if (enemy.PathIndex >= enemy.Path.Count)
            {
                ReachGoal(enemy);
            }
        }

        private void ReachGoal(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (enemy.ReachedGoal)
            {
                return;
            }

            enemy.SetMovingRaw(false);
            enemy.SetReachedGoalRaw(true);

            Debug.Log($"Enemy reached goal. Id: {enemy.Config?.Id}, DamageToBase: {enemy.DamageToBase}");

            BaseManager.Instance.TakeDamage(enemy.DamageToBase);

            RemoveEnemy(enemy);
        }

        private Vector3 GetEnemyWorldPosition(Vector3Int coord)
        {
            if (MapManager.Instance.TryGetTileView(coord, out TileView tileView))
            {
                return tileView.transform.position + Vector3.up * 0.6f;
            }

            return MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * 0.6f;
        }
    }
}
