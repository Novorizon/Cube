namespace Game
{
    public enum ProductionStatGroup
    {
        Overview = 0,
        Crops = 1,
        Ores = 2,
        BasicResources = 3,
        Buildings = 4,
    }

    public readonly struct ProductionStat
    {
        public ProductionStat(int itemId, string name, int count, float perMinute)
        {
            ItemId = itemId;
            Name = name;
            Count = count;
            PerMinute = perMinute;
        }

        public int ItemId { get; }
        public string Name { get; }
        public int Count { get; }
        public float PerMinute { get; }
    }
}
