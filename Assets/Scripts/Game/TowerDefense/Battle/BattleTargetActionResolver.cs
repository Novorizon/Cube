using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public static class BattleTargetActionResolver
    {
        private static readonly HashSet<int> unsupportedActionWarnings = new HashSet<int>();

        public static List<TdTargetActionData> Resolve(int actionGroupId, TdTargetRuntimeInfo target)
        {
            List<TdTargetActionData> result = new List<TdTargetActionData>();
            if (actionGroupId <= 0 || DataManager.Instance.BattleTargetAction == null)
            {
                return result;
            }

            IReadOnlyDictionary<int, BattleTargetActionConfig> allActions = DataManager.Instance.BattleTargetAction.GetAll();
            if (allActions == null)
            {
                return result;
            }

            List<BattleTargetActionConfig> groupActions = new List<BattleTargetActionConfig>();
            foreach (BattleTargetActionConfig config in allActions.Values)
            {
                if (config != null && config.Enable && config.GroupId == actionGroupId)
                {
                    groupActions.Add(config);
                }
            }

            groupActions.Sort(CompareConfigOrder);
            for (int i = 0; i < groupActions.Count; i++)
            {
                BattleTargetActionConfig config = groupActions[i];
                TdTargetActionType actionType = (TdTargetActionType)config.ActionType;
                if (!IsSupported(actionType))
                {
                    if (unsupportedActionWarnings.Add(config.ActionType))
                    {
                        Debug.LogWarning($"Unsupported battle target action type: {config.ActionType}. configId: {config.Id}");
                    }

                    continue;
                }

                if (!IsApplicable(actionType, target))
                {
                    continue;
                }

                result.Add(new TdTargetActionData
                {
                    ConfigId = config.Id,
                    Type = actionType,
                    Name = config.Name,
                    IconLocation = config.IconLocation,
                    Interactable = true
                });
            }

            return result;
        }

        private static int CompareConfigOrder(BattleTargetActionConfig a, BattleTargetActionConfig b)
        {
            int order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0 ? order : a.Id.CompareTo(b.Id);
        }

        private static bool IsSupported(TdTargetActionType actionType)
        {
            return actionType == TdTargetActionType.UpgradeTower ||
                   actionType == TdTargetActionType.SellTower;
        }

        private static bool IsApplicable(TdTargetActionType actionType, TdTargetRuntimeInfo target)
        {
            switch (actionType)
            {
                case TdTargetActionType.UpgradeTower:
                    return target.Type == TdTargetInfoType.Tower && target.CanUpgrade;
                case TdTargetActionType.SellTower:
                    return target.Type == TdTargetInfoType.Tower && target.CanSell;
                default:
                    return false;
            }
        }
    }
}
