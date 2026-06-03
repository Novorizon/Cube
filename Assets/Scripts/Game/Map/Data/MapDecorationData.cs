using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class MapDecorationData
    {
        public int DecorationId;
        public int X;
        public int Y;
        public int Z;
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public Vector3 LocalScale = Vector3.one;

        public Vector3Int Coord
        {
            get
            {
                return new Vector3Int(X, Y, Z);
            }
        }

        public MapDecorationData()
        {
        }

        public MapDecorationData(int decorationId, Vector3Int coord, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
        {
            DecorationId = decorationId;
            X = coord.x;
            Y = coord.y;
            Z = coord.z;
            LocalPosition = localPosition;
            LocalEuler = localEuler;
            LocalScale = localScale;
        }
    }
}
