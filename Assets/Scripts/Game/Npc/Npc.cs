using UnityEngine;

namespace Game
{
    public sealed class Npc : Actor
    {
        private NpcConfig config;
        private NpcData data;

        public NpcConfig Config
        {
            get
            {
                return config;
            }
        }

        public NpcData Data
        {
            get
            {
                return data;
            }
        }

        public void InitializeRaw(NpcConfig config, NpcData data)
        {
            this.config = config;
            this.data = data;

            ActorType actorType = config != null ? (ActorType)config.ActorType : ActorType.None;
            int configId = config != null ? config.Id : 0;

            InitializeActor(actorType, configId);
        }

        public void SetDataRaw(NpcData data)
        {
            this.data = data;
        }
    }
}
