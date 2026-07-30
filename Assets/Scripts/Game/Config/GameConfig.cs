namespace Game
{
    /// <summary>
    /// 项目级可调参数的唯一代码入口。
    /// 这里只放设计、表现和手感参数；算法不变量、资源路径和生成代码常量留在所属模块。
    /// </summary>
    public static class GameConfig
    {
        /// <summary>
        /// 剧情 UI 与静态插画镜头的全局表现参数。
        /// </summary>
        public static class Story
        {
            /// <summary>
            /// 剧情文本框右上角的进度显示方式。
            /// DialogueOnly 只统计 Text 和 Mixed，AllSteps 统计全部 Step，Hidden 完全隐藏。
            /// </summary>
            public const StoryProgressDisplayMode ProgressDisplayMode =
                StoryProgressDisplayMode.DialogueOnly;

            /// <summary>
            /// ZoomOut 的初始放大倍数，必须不小于 1。
            /// 1.85 表示开场只显示图片中央约 1 / 1.85 的宽度和高度；数值越大，初始取景越近、拉远幅度越明显。
            /// </summary>
            public const float ZoomOutStartScale = 1.85f;

            /// <summary>
            /// ZoomIn 的结束放大倍数，必须不小于 1。
            /// 1.18 表示结束时显示图片中央约 1 / 1.18 的宽度和高度；数值越大，推进幅度越明显。
            /// </summary>
            public const float ZoomInEndScale = 1.18f;

            /// <summary>
            /// 横向平移镜头一次显示的图片 UV 比例，范围为 0 到 1。
            /// 0.72 表示同时显示图片 72% 的宽度和高度；数值越小，取景越近、可平移距离越长。
            /// </summary>
            public const float PanViewScale = 0.72f;
        }

        /// <summary>
        /// 世界场景及放置预览的全局表现参数。
        /// </summary>
        public static class World
        {
            /// <summary>
            /// 农田预览面相对地表向上抬升的地块尺寸比例，用于避免与地表发生 Z-Fighting。
            /// 实际抬升高度为 TileSize * 此值；数值越大，预览面离地表越高。
            /// </summary>
            public const float FarmPreviewSurfaceLiftInTiles = 0.005f;

            /// <summary>
            /// 农田预览材质中每个地块的网格重复密度，对应 Placement Shader 的 _GridScale。
            /// 有效范围为 0.5 到 12；数值越大，网格越密。
            /// </summary>
            public const float FarmPreviewGridScale = 1f;

            /// <summary>
            /// 可放置农田预览的网格线强度，范围为 0 到 1；数值越大，网格线越明显。
            /// </summary>
            public const float FarmPreviewValidGridStrength = 0.1f;

            /// <summary>
            /// 不可放置农田预览的网格线强度，范围为 0 到 1；数值越大，网格线越明显。
            /// </summary>
            public const float FarmPreviewInvalidGridStrength = 0.18f;

            /// <summary>
            /// 农田预览网格线宽度，对应 Placement Shader 的 _GridWidth。
            /// 有效范围为 0.005 到 0.2；数值越大，网格线越粗。
            /// </summary>
            public const float FarmPreviewGridWidth = 0.025f;

            /// <summary>
            /// 农田预览的视角边缘光强度，对应 Placement Shader 的 _RimStrength。
            /// 有效范围为 0 到 2；数值越大，轮廓边缘越亮。
            /// </summary>
            public const float FarmPreviewRimStrength = 0.15f;
        }

        /// <summary>
        /// 游戏内日历结构、昼夜边界与现实时间换算参数。
        /// </summary>
        public static class Calendar
        {
            /// <summary>
            /// 一个游戏日包含的游戏小时数。
            /// </summary>
            public const int HoursPerDay = 24;

            /// <summary>
            /// 一个游戏小时包含的游戏分钟数。
            /// </summary>
            public const int MinutesPerHour = 60;

            /// <summary>
            /// 一个游戏月份包含的游戏天数。
            /// </summary>
            public const int DaysPerMonth = 28;

            /// <summary>
            /// 一个季节包含的游戏月份数。
            /// </summary>
            public const int MonthsPerSeason = 1;

            /// <summary>
            /// 一年包含的季节数；当前必须与 Spring、Summer、Autumn、Winter 四个 Season 枚举一致。
            /// </summary>
            public const int SeasonsPerYear = 4;

            /// <summary>
            /// 一年包含的游戏月份数，由每季月份数乘以每年季节数计算。
            /// </summary>
            public const int MonthsPerYear = MonthsPerSeason * SeasonsPerYear;

            /// <summary>
            /// 白天开始的游戏小时，也是新游戏和“跳到白天”使用的默认时间；该小时计入白天。
            /// </summary>
            public const int DayStartHour = 6;

            /// <summary>
            /// 夜晚开始的游戏小时；该小时起不再计入白天。
            /// </summary>
            public const int NightStartHour = 18;

            /// <summary>
            /// 顶部昼夜圆盘旋转角为 0 度时对应的游戏小时。
            /// 圆盘从该时刻起每经过完整一个游戏日旋转 360 度。
            /// </summary>
            public const int DayNightDiskZeroHour = 12;

            /// <summary>
            /// GameTimeScale 为 1 时，一个完整游戏日对应的现实秒数。
            /// 600 表示现实 10 分钟经过一个游戏日；数值越大，游戏时间流逝越慢，同时影响离线时间换算。
            /// </summary>
            public const float RealSecondsPerDay = 600f;
        }
    }
}
