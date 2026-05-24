namespace Skill2
{
    public sealed class SkillThinker
    {
        public string Name { get; }
        public Skill2Engine Engine { get; }
        public SkillAbility Ability { get; }
        public ISkill2Unit Caster { get; }
        public UnityEngine.Vector3 Position { get; }
        public float Radius { get; }
        public float Duration { get; }
        public float RemainingTime { get; private set; }
        public float Interval { get; }
        public bool IsDestroyed { get; private set; }
        public SkillThinkerScript Script { get; }

        private float intervalTimer;

        internal SkillThinker(Skill2Engine engine, SkillThinkerRequest request)
        {
            Engine = engine;
            Name = request.Name;
            Ability = request.Ability;
            Caster = request.Caster;
            Position = request.Position;
            Radius = request.Radius;
            Duration = request.Duration;
            RemainingTime = request.Duration;
            Interval = request.Interval;
            intervalTimer = request.Interval;
            Script = request.Script ?? new DefaultSkillThinkerScript();
            Script.Bind(this);
            Script.OnCreated();
        }

        internal void Tick(float deltaTime)
        {
            if (IsDestroyed)
            {
                return;
            }

            if (Duration > 0f)
            {
                RemainingTime -= deltaTime;
                if (RemainingTime <= 0f)
                {
                    Destroy();
                    return;
                }
            }

            if (Interval <= 0f)
            {
                return;
            }

            intervalTimer -= deltaTime;
            if (intervalTimer > 0f)
            {
                return;
            }

            intervalTimer += Interval;
            Script.OnIntervalThink();
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
