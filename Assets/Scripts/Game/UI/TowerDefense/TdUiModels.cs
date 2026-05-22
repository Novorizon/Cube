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

    public enum TdTargetInfoType
    {
        None = 0,
        Base = 1,
        Tower = 2,
        Npc = 3,
        Item = 4,
    }

    [Serializable]
    public struct TdTargetRuntimeInfo
    {
        public TdTargetInfoType Type;
        public int TargetId;
        public string Name;
        public string Description;
        public Sprite Icon;

        public int Level;
        public int CurrentHp;
        public int MaxHp;

        public int Attack;
        public int AttackAdd;
        public float Range;
        public float AttackInterval;

        public int UpgradeCost;
        public int SellGold;
        public bool CanUpgrade;
        public bool CanSell;
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
