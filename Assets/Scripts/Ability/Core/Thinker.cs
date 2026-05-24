namespace Ability
{
    public sealed class Thinker
    {
        public AbilitySystem Engine { get; }
        public Ability Ability { get; }
        public IUnit Caster { get; }
        public UnityEngine.Vector3 Position { get; }
        public float Duration { get; }
        public float Interval { get; }
        public float Radius { get; }
        public ThinkerScript Script { get; }
        public bool IsDestroyed { get; private set; }

        private float remainingTime;
        private float intervalTimer;

        internal Thinker(AbilitySystem engine, ThinkerRequest request)
        {
            Engine = engine;
            Ability = request.Ability;
            Caster = request.Caster;
            Position = request.Position;
            Duration = request.Duration;
            Interval = request.Interval;
            Radius = request.Radius;
            remainingTime = request.Duration;
            intervalTimer = request.Interval;
            Script = request.Script ?? new DefaultThinkerScript();
            Script.Bind(this);
            Script.OnCreated();
        }

        internal void Tick(float deltaTime)
        {
            if (IsDestroyed)
            {
                return;
            }

            Script.OnThink(deltaTime);
            if (Interval > 0f)
            {
                intervalTimer -= deltaTime;
                if (intervalTimer <= 0f)
                {
                    intervalTimer += Interval;
                    Script.OnIntervalThink();
                }
            }

            if (Duration > 0f)
            {
                remainingTime -= deltaTime;
                if (remainingTime <= 0f)
                {
                    Destroy();
                }
            }
        }

        internal void Destroy()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;
            Script.OnDestroy();
        }
    }
}
