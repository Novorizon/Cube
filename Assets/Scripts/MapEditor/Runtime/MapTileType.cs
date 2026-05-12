namespace Game
{
    /// <summary>
    /// 地块类型。
    /// </summary>
    public enum MapTileType
    {
        None = 0,

        /// <summary>
        /// 草地。
        /// 可走，可建塔。
        /// </summary>
        Grass,

        /// <summary>
        /// 山地。
        /// 可走，不可建塔。
        /// 可以覆盖在 Grass 上，后续可被炸掉露出 Grass。
        /// </summary>
        Hill,

        /// <summary>
        /// 雪地。
        /// 可走，不可建塔。
        /// </summary>
        Snow,

        /// <summary>
        /// 水。
        /// 不可走，不可建塔。
        /// 上方不允许普通地块。
        /// </summary>
        Water,

        /// <summary>
        /// 地基层。
        /// 固定 y = -1，仅表现用，不参与寻路、建造、点位。
        /// </summary>
        Soil = 1000,
    }
}