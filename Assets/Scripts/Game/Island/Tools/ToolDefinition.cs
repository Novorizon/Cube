namespace Game
{
    public sealed class ToolDefinition
    {
        public ToolDefinition(int itemId, ToolType toolType, int level, string name)
        {
            ItemId = itemId;
            ToolType = toolType;
            Level = level;
            Name = name;
        }

        public int ItemId { get; }
        public ToolType ToolType { get; }
        public int Level { get; }
        public string Name { get; }
    }
}
