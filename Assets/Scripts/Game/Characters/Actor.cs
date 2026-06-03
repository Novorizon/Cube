namespace Game
{
    public abstract class Actor : GameEntity
    {
        private ActorType actorType;

        public ActorType ActorType
        {
            get
            {
                return actorType;
            }
        }

        public virtual void InitializeActor(ActorType actorType, int configId)
        {
            this.actorType = actorType;
            InitializeGameEntity(GameEntityKind.Actor, configId);
        }
    }
}
