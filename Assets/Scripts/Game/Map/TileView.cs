using UnityEngine;

namespace Game
{
    /// <summary>
    /// 地块表现层。
    /// TileView 不保存独立坐标。
    /// 坐标唯一来源是 TileData.Coord。
    /// </summary>
    public sealed class TileView : MonoBehaviour
    {
        private TileData data;

        public TileData Data
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

                return data.Coord;
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

        public void Initialize(TileData tileData)
        {
            data = tileData;
        }

        public void SetSelected(bool selected)
        {
            // 后面可以在这里做选中框、描边、高亮。
        }
    }
}