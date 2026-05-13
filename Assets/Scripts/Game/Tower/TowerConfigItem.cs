using UnityEngine;

namespace Game
{
    [System.Serializable]
    public sealed class TowerConfigItem
    {
        public TowerType Type;
        public string Name;
        public GameObject Prefab;
        public int Cost = 100;
        public float Range = 3f;
        public int Damage = 10;
        public float AttackInterval = 1f;
    }
}
