using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public enum TdTargetInfoType
    {
        None = 0,
        Base = 1,
        Tower = 2,
        Npc = 3,
        Item = 4,
    }

    public enum TdTargetActionType
    {
        None = 0,
        UpgradeTower = 1,
        SellTower = 2,
    }

    [Serializable]
    public struct TdTargetActionData
    {
        public int ConfigId;
        public TdTargetActionType Type;
        public string Name;
        public string IconLocation;
        public bool Interactable;
    }

    [Serializable]
    public struct TdTargetActionRequest
    {
        public TdTargetActionData Action;
        public TdTargetRuntimeInfo Target;
    }

    [Serializable]
    public struct TdInfoSlotData
    {
        public string Key;
        public Sprite Icon;
        public string Name;
        public string Value;
        public string AddValue;
        public bool Visible;

        public TdInfoSlotData(string key, string name, string value, string addValue = null, bool visible = true)
        {
            Key = key;
            Icon = null;
            Name = name;
            Value = value;
            AddValue = addValue;
            Visible = visible;
        }

        public TdInfoSlotData(string key, Sprite icon, string name, string value, string addValue = null, bool visible = true)
        {
            Key = key;
            Icon = icon;
            Name = name;
            Value = value;
            AddValue = addValue;
            Visible = visible;
        }
    }

    [Serializable]
    public struct TdTargetRuntimeInfo
    {
        public TdTargetInfoType Type;
        public int TargetId;
        public string Name;
        public string Description;
        public Sprite Icon;
        public string PreviewPrefabLocation;
        public Vector3Int Coord;
        public int ActionGroupId;

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

        public List<TdInfoSlotData> InfoSlots;
        public List<TdTargetActionData> Actions;
    }

    [Serializable]
    public struct TdTowerRuntimeInfo
    {
        public int TowerId;
        public string Name;
        public Sprite Icon;
        public string PreviewPrefabLocation;
        public int Level;
        public int Attack;
        public int AttackAdd;
        public float Range;
        public float AttackInterval;
        public int UpgradeCost;
        public int SellGold;
        public bool CanUpgrade;
        public List<TdInfoSlotData> InfoSlots;
    }
}
