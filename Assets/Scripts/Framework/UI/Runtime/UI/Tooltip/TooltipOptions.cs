namespace UI
{
    public enum TooltipPlacement
    {
        Auto = 0,
        Left = 1,
        Right = 2,
        Above = 3,
        Below = 4,
    }

    public sealed class TooltipOptions
    {
        public float DelaySeconds { get; set; } = -1f;
        public TooltipPlacement Placement { get; set; } = TooltipPlacement.Auto;
    }
}
