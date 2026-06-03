using UnityEngine;

namespace Game
{
    public class FlatTileVisual : MonoBehaviour
    {
        [Header("Main")]
        [SerializeField]
        private GameObject main;

        [Header("Outer Edge")]
        [SerializeField]
        private GameObject edgeNorth;

        [SerializeField]
        private GameObject edgeEast;

        [SerializeField]
        private GameObject edgeSouth;

        [SerializeField]
        private GameObject edgeWest;

        [Header("Water Shore Edge")]
        [SerializeField]
        private GameObject shoreNorth;

        [SerializeField]
        private GameObject shoreEast;

        [SerializeField]
        private GameObject shoreSouth;

        [SerializeField]
        private GameObject shoreWest;

        [Header("Snow Blend Edge")]
        [SerializeField]
        private GameObject snowNorth;

        [SerializeField]
        private GameObject snowEast;

        [SerializeField]
        private GameObject snowSouth;

        [SerializeField]
        private GameObject snowWest;

        [Header("Hill Blend Edge")]
        [SerializeField]
        private GameObject hillNorth;

        [SerializeField]
        private GameObject hillEast;

        [SerializeField]
        private GameObject hillSouth;

        [SerializeField]
        private GameObject hillWest;

        [Header("Corners")]
        [SerializeField]
        private GameObject cornerNorthEast;

        [SerializeField]
        private GameObject cornerEastSouth;

        [SerializeField]
        private GameObject cornerSouthWest;

        [SerializeField]
        private GameObject cornerWestNorth;

        [Header("Decoration")]
        [SerializeField]
        private GameObject[] randomDecorations;

        [SerializeField]
        private bool randomizeDecoration = true;

        [SerializeField]
        private int decorationSeedOffset = 17;

        public void Refresh(MapTileType centerType, MapTileType northType, MapTileType eastType, MapTileType southType, MapTileType westType)
        {
            RefreshOuterEdges(northType, eastType, southType, westType);
            RefreshShoreEdges(centerType, northType, eastType, southType, westType);
            RefreshSnowEdges(centerType, northType, eastType, southType, westType);
            RefreshHillEdges(centerType, northType, eastType, southType, westType);
            RefreshCorners(northType, eastType, southType, westType);
            RefreshDecoration(centerType);
        }

        private void RefreshOuterEdges(MapTileType northType, MapTileType eastType, MapTileType southType, MapTileType westType)
        {
            SetActive(edgeNorth, northType == MapTileType.None);
            SetActive(edgeEast, eastType == MapTileType.None);
            SetActive(edgeSouth, southType == MapTileType.None);
            SetActive(edgeWest, westType == MapTileType.None);
        }

        private void RefreshShoreEdges(MapTileType centerType, MapTileType northType, MapTileType eastType, MapTileType southType, MapTileType westType)
        {
            bool centerIsWater = centerType == MapTileType.Water;

            SetActive(shoreNorth, !centerIsWater && northType == MapTileType.Water);
            SetActive(shoreEast, !centerIsWater && eastType == MapTileType.Water);
            SetActive(shoreSouth, !centerIsWater && southType == MapTileType.Water);
            SetActive(shoreWest, !centerIsWater && westType == MapTileType.Water);
        }

        private void RefreshSnowEdges(MapTileType centerType, MapTileType northType, MapTileType eastType, MapTileType southType, MapTileType westType)
        {
            bool centerIsSnow = centerType == MapTileType.Snow;

            SetActive(snowNorth, !centerIsSnow && northType == MapTileType.Snow);
            SetActive(snowEast, !centerIsSnow && eastType == MapTileType.Snow);
            SetActive(snowSouth, !centerIsSnow && southType == MapTileType.Snow);
            SetActive(snowWest, !centerIsSnow && westType == MapTileType.Snow);
        }

        private void RefreshHillEdges(MapTileType centerType, MapTileType northType, MapTileType eastType, MapTileType southType, MapTileType westType)
        {
            bool centerIsHill = centerType == MapTileType.Hill;

            SetActive(hillNorth, !centerIsHill && northType == MapTileType.Hill);
            SetActive(hillEast, !centerIsHill && eastType == MapTileType.Hill);
            SetActive(hillSouth, !centerIsHill && southType == MapTileType.Hill);
            SetActive(hillWest, !centerIsHill && westType == MapTileType.Hill);
        }

        private void RefreshCorners(MapTileType northType, MapTileType eastType, MapTileType southType, MapTileType westType)
        {
            SetActive(cornerNorthEast, northType == MapTileType.None && eastType == MapTileType.None);
            SetActive(cornerEastSouth, eastType == MapTileType.None && southType == MapTileType.None);
            SetActive(cornerSouthWest, southType == MapTileType.None && westType == MapTileType.None);
            SetActive(cornerWestNorth, westType == MapTileType.None && northType == MapTileType.None);
        }

        private void RefreshDecoration(MapTileType centerType)
        {
            if (!randomizeDecoration || randomDecorations == null || randomDecorations.Length == 0)
            {
                return;
            }

            for (int i = 0; i < randomDecorations.Length; i++)
            {
                SetActive(randomDecorations[i], false);
            }

            if (centerType == MapTileType.Water || centerType == MapTileType.Soil || centerType == MapTileType.None)
            {
                return;
            }

            int seed = Mathf.Abs(Mathf.RoundToInt(transform.position.x * 31f + transform.position.z * 97f + decorationSeedOffset));
            int value = seed % 100;

            if (value >= 25)
            {
                return;
            }

            int index = seed % randomDecorations.Length;
            SetActive(randomDecorations[index], true);
        }

        private void SetActive(GameObject target, bool active)
        {
            if (target == null)
            {
                return;
            }

            if (target.activeSelf == active)
            {
                return;
            }

            target.SetActive(active);
        }
    }
}