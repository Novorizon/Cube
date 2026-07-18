using System;
using System.Collections.Generic;

namespace Game
{
    public enum BattleItemUseResult
    {
        Success,
        MissingItem,
        NotUsableInBattle,
        NoHandler,
        Failed
    }

    public sealed class BattleItemUseService
    {
        public static BattleItemUseService Instance { get; } = new BattleItemUseService();

        private readonly Dictionary<int, Func<ItemConfig, bool>> handlers = new Dictionary<int, Func<ItemConfig, bool>>();

        private BattleItemUseService()
        {
        }

        public void Register(int itemId, Func<ItemConfig, bool> handler)
        {
            if (itemId > 0 && handler != null)
            {
                handlers[itemId] = handler;
            }
        }

        public void Unregister(int itemId)
        {
            handlers.Remove(itemId);
        }

        public BattleItemUseResult TryUse(int itemId)
        {
            if (!BattleItemManager.Instance.HasItem(itemId, 1))
            {
                return BattleItemUseResult.MissingItem;
            }

            if (DataManager.Instance.Item == null || !DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) || config == null)
            {
                return BattleItemUseResult.NotUsableInBattle;
            }

            ItemUseScope scope = (ItemUseScope)config.UseScope;
            if (scope != ItemUseScope.BattleOnly && scope != ItemUseScope.Both)
            {
                return BattleItemUseResult.NotUsableInBattle;
            }

            if (!handlers.TryGetValue(itemId, out Func<ItemConfig, bool> handler))
            {
                return BattleItemUseResult.NoHandler;
            }

            if (!handler(config))
            {
                return BattleItemUseResult.Failed;
            }

            return BattleItemManager.Instance.TryConsume(itemId, 1)
                ? BattleItemUseResult.Success
                : BattleItemUseResult.MissingItem;
        }
    }
}
