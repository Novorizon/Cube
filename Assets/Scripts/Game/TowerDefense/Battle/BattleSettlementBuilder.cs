using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TD 结束结算。
/// 当前只生成结算结果，不直接写入全局经营系统。
/// 以后可把 reward.OuterItemMap 交给 GlobalItemManager / GameItemManager。
/// </summary>
namespace Game
{
    public class BattleSettlementBuilder
    {
        public BattleSettlementReward BuildReward(IReadOnlyDictionary<int, BattleItemData> battleItems)
        {
            BattleSettlementReward reward = new BattleSettlementReward();

            foreach (KeyValuePair<int, BattleItemData> pair in battleItems)
            {
                int itemId = pair.Key;
                BattleItemData itemData = pair.Value;

                ItemConfig config = DataManager.Instance.Item.Get(itemId);

                if (config == null)
                {
                    continue;
                }

                ItemUseScope useScope = (ItemUseScope)config.UseScope;

                if (useScope != ItemUseScope.Settlement && useScope != ItemUseScope.Both)
                {
                    continue;
                }

                if (config.SettlementItemId <= 0)
                {
                    continue;
                }

                int countPerItem = config.SettlementCountPerItem;

                if (countPerItem <= 0)
                {
                    countPerItem = 1;
                }

                int outerCount = itemData.Count * countPerItem;
                reward.AddOuterItem(config.SettlementItemId, outerCount);
            }

            return reward;
        }

        public void PrintReward(BattleSettlementReward reward)
        {
            foreach (KeyValuePair<int, int> pair in reward.OuterItemMap)
            {
                Debug.Log($"Battle settlement item. outerItemId: {pair.Key}, count: {pair.Value}");
            }
        }
    }
}