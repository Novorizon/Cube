using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public sealed class TdTowerUiConfig
    {
        public int Id;
        public string Name;
        public Sprite Icon;
        public int Cost;
    }

    [Serializable]
    public sealed class TdSkillUiConfig
    {
        public int Id;
        public string Name;
        public Sprite Icon;
        public int Count;
    }

    [Serializable]
    public struct TdTowerRuntimeInfo
    {
        public int TowerId;
        public string Name;
        public Sprite Icon;
        public int Level;
        public int Attack;
        public int AttackAdd;
        public float Range;
        public float AttackInterval;
        public int UpgradeCost;
        public int SellGold;
        public bool CanUpgrade;
    }
}
