namespace Game
{
    /// <summary>
    /// 项目级可调参数的唯一代码入口。
    /// 这里只放设计、表现和手感参数；算法不变量、资源路径和生成代码常量留在所属模块。
    /// </summary>
    public static class GameConfig
    {
        public static class Story
        {
            public const StoryProgressDisplayMode ProgressDisplayMode =
                StoryProgressDisplayMode.DialogueOnly;

            public const float ZoomOutStartScale = 1.85f;
            public const float ZoomInEndScale = 1.18f;
            public const float PanViewScale = 0.72f;
        }

        public static class World
        {
            public const float FarmPreviewSurfaceLiftInTiles = 0.005f;
            public const float FarmPreviewGridScale = 1f;
            public const float FarmPreviewValidGridStrength = 0.1f;
            public const float FarmPreviewInvalidGridStrength = 0.18f;
            public const float FarmPreviewGridWidth = 0.025f;
            public const float FarmPreviewRimStrength = 0.15f;
        }

        public static class Calendar
        {
            public const int HoursPerDay = 24;
            public const int MinutesPerHour = 60;
            public const int DaysPerMonth = 28;
            public const int MonthsPerSeason = 1;
            public const int SeasonsPerYear = 4;
            public const int MonthsPerYear = MonthsPerSeason * SeasonsPerYear;

            public const int DayStartHour = 6;
            public const int NightStartHour = 18;
            public const int DayNightDiskZeroHour = 12;
            public const float RealSecondsPerDay = 600f;
        }
    }
}
