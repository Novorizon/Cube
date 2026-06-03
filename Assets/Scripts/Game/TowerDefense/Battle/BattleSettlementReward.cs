using System.Collections.Generic;

public class BattleSettlementReward
{
    public readonly Dictionary<int, int> OuterItemMap = new Dictionary<int, int>();

    public void AddOuterItem(int itemId, int count)
    {
        if (itemId <= 0 || count <= 0)
        {
            return;
        }

        if (!OuterItemMap.ContainsKey(itemId))
        {
            OuterItemMap.Add(itemId, 0);
        }

        OuterItemMap[itemId] += count;
    }
}
