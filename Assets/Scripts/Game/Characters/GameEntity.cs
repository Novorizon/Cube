using UnityEngine;

namespace Game
{
    public abstract class GameEntity : MonoBehaviour
    {
        private GameEntityKind kind;
        private int configId;

        public GameEntityKind Kind
        {
            get
            {
                return kind;
            }
        }

        public int ConfigId
        {
            get
            {
                return configId;
            }
        }

        public virtual void InitializeGameEntity(GameEntityKind kind, int configId)
        {
            this.kind = kind;
            this.configId = configId;
        }
    }
}
