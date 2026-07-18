namespace Game
{
    /// <summary>Read-only middleware binding data for the Ability workbench.</summary>
    public sealed class AbilityBindingDebugInfo
    {
        public TdUnitKind Kind;
        public int BusinessObjectId;
        public int RuntimeEntityId;
        public string DisplayName;
        public bool IsValid;
    }
}
