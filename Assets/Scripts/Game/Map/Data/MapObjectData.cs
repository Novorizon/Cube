using System;
using UnityEngine;

namespace Game
{
    public enum MapObjectType
    {
        Decoration = 0,
        Resource = 1,
        Building = 2,
        Interactable = 3,
    }

    [Serializable]
    public class MapObjectData
    {
        public int ObjectId;
        public MapObjectType ObjectType = MapObjectType.Decoration;
        public int ConfigId;
        public int X;
        public int Y;
        public int Z;
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public Vector3 LocalScale = Vector3.one;
        public bool BlocksBuild;
        public bool BlocksMove;

        public Vector3Int Coord
        {
            get
            {
                return new Vector3Int(X, Y, Z);
            }
        }

        public MapObjectData()
        {
        }

        public MapObjectData(int objectId, MapObjectType objectType, int configId, Vector3Int coord, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
        {
            ObjectId = objectId;
            ObjectType = objectType;
            ConfigId = configId;
            X = coord.x;
            Y = coord.y;
            Z = coord.z;
            LocalPosition = localPosition;
            LocalEuler = localEuler;
            LocalScale = localScale;
        }

        public MapObjectData(int decorationId, Vector3Int coord, Vector3 localPosition, Vector3 localEuler, Vector3 localScale)
            : this(decorationId, MapObjectType.Decoration, decorationId, coord, localPosition, localEuler, localScale)
        {
        }
    }
}
