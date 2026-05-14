using Game.Framework;
using Luban;
using UnityEngine;

namespace Game
{
    public sealed class DataManager : Singleton<DataManager>
    {
        private Tables tables;

        public Tables Tables
        {
            get
            {
                return tables;
            }
        }

        public bool Initialize()
        {
            tables = new Tables(LoadByteBuf);
            Debug.Log("DataManager initialize success.");
            return true;
        }

        public NpcConfig GetNpcConfig(int id)
        {
            if (tables == null)
            {
                Debug.LogError("DataManager is not initialized.");
                return null;
            }

            return tables.TbNpc.Get(id);
        }

        public bool TryGetNpcConfig(int id, out NpcConfig config)
        {
            config = null;

            if (tables == null)
            {
                Debug.LogError("DataManager is not initialized.");
                return false;
            }

            config = tables.TbNpc.GetOrDefault(id);
            return config != null;
        }

        private ByteBuf LoadByteBuf(string file)
        {
            string location = $"Assets/Data/Bin/{file}.bytes";

            TextAsset textAsset = ResourceManager.Instance.LoadTextAsset(location);

            if (textAsset == null)
            {
                Debug.LogError($"Load config failed: {location}");
                return null;
            }

            return new ByteBuf(textAsset.bytes);
        }
    }
}
