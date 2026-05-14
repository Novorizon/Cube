using Game.Framework;

namespace Game
{
    public sealed class NpcManager : Singleton<NpcManager>
    {
        public bool TryGetNpc(int id, out NpcConfig config)
        {
            return DataManager.Instance.TryGetNpc(id, out config);
        }

        public NpcConfig GetNpc(int id)
        {
            return DataManager.Instance.GetNpc(id);
        }

        public bool IsEnemy(int id)
        {
            if (!TryGetNpc(id, out NpcConfig config))
            {
                return false;
            }

            return config.Kind == (int)GameEntityKind.Actor && config.ActorType == (int)ActorType.Enemy;
        }
    }
}
