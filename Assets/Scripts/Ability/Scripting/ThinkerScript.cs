namespace Ability
{
    public abstract class ThinkerScript
    {
        public Thinker Thinker { get; private set; }
        public AbilitySystem Engine => Thinker != null ? Thinker.Engine : null;
        public Ability Ability => Thinker != null ? Thinker.Ability : null;
        public IUnit Caster => Thinker != null ? Thinker.Caster : null;

        internal void Bind(Thinker thinker)
        {
            Thinker = thinker;
        }

        public virtual void OnCreated() { }
        public virtual void OnDestroy() { }
        public virtual void OnIntervalThink() { }
        public virtual void OnThink(float deltaTime) { }
    }

    public sealed class DefaultThinkerScript : ThinkerScript
    {
    }
}
