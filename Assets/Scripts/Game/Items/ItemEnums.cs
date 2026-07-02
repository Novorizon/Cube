public enum ItemType
{
    // ItemType is a behavior category. Concrete item id ranges are defined in ItemIds.
    None = 0,
    Currency = 1,
    Consumable = 2,
    Blueprint = 3,
    Seed = 4,
    Material = 5,
    Tool = 6,
}

public enum ItemUseScope
{
    None = 0,
    BattleOnly = 1,
    Settlement = 2,
    Both = 3,
}
