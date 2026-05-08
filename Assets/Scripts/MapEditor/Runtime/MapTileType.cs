namespace Game
{
    /// <summary>
    /// 地块类型。
    /// 当前先只保留最基础的逻辑类型。
    /// </summary>
    public enum MapTileType
    {
        None = 0,

        /// <summary>
        /// 草地，默认可行走地块。
        /// </summary>
        Grass ,

        /// <summary>
        /// 山地，后续可设置为高消耗或不可行走。
        /// </summary>
        Hill ,

        /// <summary>
        /// 雪地，后续可设置为低速或特殊消耗。
        /// </summary>
        Snow ,

        /// <summary>
        /// 水，后续通常不可行走，或只能特定单位通过。
        /// </summary>
        Water,



        Soil = 1000,
    }
}
