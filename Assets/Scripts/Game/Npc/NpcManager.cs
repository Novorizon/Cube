using Game.Framework;

namespace Game
{
    public sealed class NpcManager : Singleton<NpcManager>
    {
        public bool TryGetNpc(int id, out Npc config)
        {
            return DataManager.Instance.TryGetNpc(id, out config);
        }

        public Npc GetNpc(int id)
        {
            return DataManager.Instance.GetNpc(id);
        }

        public bool IsEnemy(int id)
        {
            if (!TryGetNpc(id, out Npc config))
            {
                return false;
            }

            return config.Kind == (int)GameEntityKind.Actor && config.ActorType == (int)ActorType.Enemy;
        }
    }
}
