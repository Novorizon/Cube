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

        public bool HasData()
        {
            return data != null;
        }

        public static bool TryGetValidFrom(Transform start, out TileView tileView)
        {
            tileView = null;

            for (Transform current = start; current != null; current = current.parent)
            {
                TileView candidate = current.GetComponent<TileView>();
                if (candidate == null || !candidate.HasData())
                {
                    continue;
                }

                tileView = candidate;
                return true;
            }

            return false;
        }

        public static TileView InitializeHierarchy(GameObject root, TileData tileData)
        {
            if (root == null)
            {
                return null;
            }

            TileView rootTileView = root.GetComponent<TileView>();
            if (rootTileView == null)
            {
                return null;
            }

            rootTileView.Initialize(tileData);

            TileView[] tileViews = root.GetComponentsInChildren<TileView>(true);
            for (int i = 0; i < tileViews.Length; i++)
            {
                TileView tileView = tileViews[i];
                if (tileView == null || tileView == rootTileView)
                {
                    continue;
                }

                tileView.Initialize(tileData);
            }

            return rootTileView;
        }

        public void SetSelected(bool selected)
        {
            // 后面可以在这里做选中框、描边、高亮。
        }
    }
}
