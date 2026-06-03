using UnityEngine;

namespace Game
{
    public sealed class MapTerrainBlendDebugState : MonoBehaviour
    {
        public MapTileType Self;
        public MapTileType North;
        public MapTileType East;
        public MapTileType South;
        public MapTileType West;
        public bool Applied;
        public bool UsedRuntimeMaterialInstance;
        public string MaterialName;
        public string BaseTextureName;
        public string NorthTransitionName;
        public string EastTransitionName;
        public string SouthTransitionName;
        public string WestTransitionName;
    }
}
