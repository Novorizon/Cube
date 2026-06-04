using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    [Obsolete("Use MapObjectData instead.")]
    public class MapDecorationData : MapObjectData
    {
        public int DecorationId
        {
            get
            {
                return ConfigId;
            }
            set
            {
                ConfigId = value;
            }
        }

        public MapDecorationData()
        {
        }

        public MapDecorationData(int decorationId, Vector3Int coord, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
            : base(decorationId, MapObjectType.Decoration, decorationId, coord, localPosition, localEuler, localScale)
        {
        }
    }
}
