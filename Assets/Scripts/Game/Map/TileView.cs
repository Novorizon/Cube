using UnityEngine;

namespace Game
{
    /// <summary>
    /// 地块表现层。
    /// 
    /// 注意：
    /// TileView 不保存独立坐标。
    /// 坐标唯一来源是 MapTileData.X/Y/Z。
    /// </summary>
    public sealed class TileView : MonoBehaviour
    {
        private MapTileData data;

        public MapTileData Data
        {
            get
            {
                return data;
            }
        }

        public Vector3Int Coord
        {
            get
            {
                if (data == null)
                {
                    return default;
                }

                return new Vector3Int(data.X, data.Y, data.Z);
            }
        }

        public MapTileType Type
        {
            get
            {
                if (data == null)
                {
                    return MapTileType.None;
                }

                return data.Type;
            }
        }

        public void Initialize(MapTileData tileData)
        {
            data = tileData;
        }

        public void SetSelected(bool selected)
        {
            // 暂时留空。
            // 后面可以在这里做高亮、描边、选中框等表现。
        }
    }
}