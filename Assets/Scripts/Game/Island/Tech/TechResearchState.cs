namespace Game
{
    public enum TechResearchState
    {
        Invalid = 0,
        Researched = 1,
        CanResearch = 2,
        Disabled = 3,
        LockedByPrerequisite = 4,
        MissingCostConfig = 5,
        NotEnoughCost = 6,
    }
}
