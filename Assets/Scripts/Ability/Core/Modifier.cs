using UnityEngine;

namespace Game.Ability
{
    /// <summary>
    /// Runtime modifier instance attached to one parent unit.
    /// It owns duration, stacks, interval thinking, aura refresh, and state/property contribution.
    /// </summary>
    public sealed class Modifier
    {
        public AbilitySystem Engine { get; }
        public string Name => Definition.Name;
        public ModifierDefinition Definition { get; }
        public ModifierScript Script { get; }
        public IUnit Caster { get; }
        public IUnit Parent { get; }
        public Ability Ability { get; }
        public Modifier SourceAura { get; }
        public bool IsAuraApplied { get; }
        public int StackCount { get; private set; }
        public float Duration { get; private set; }
        public float RemainingTime { get; private set; }
        public float ElapsedTime { get; private set; }
        public bool IsDestroyed { get; private set; }

        private float intervalTimer;
        private float auraTimer;
        private IPresentationHandle sustainedPresentation;

        internal Modifier(AbilitySystem engine, ModifierDefinition definition, IUnit caster, IUnit parent, Ability ability, ModifierScript script, ModifierApplyOptions options)
        {
            Engine = engine;
            Definition = definition;
            Caster = caster;
            Parent = parent;
            Ability = ability;
            Script = script ?? new DefaultModifierScript();
            SourceAura = options != null ? options.SourceModifier : null;
            IsAuraApplied = options != null && options.IsAura;
            StackCount = Mathf.Max(1, options != null ? options.StackCount : 1);
            Duration = ResolveDuration(definition, options);
            RemainingTime = Duration;
            intervalTimer = definition.Interval;
            auraTimer = 0f;
            Script.Bind(this);
            Script.OnCreated(options);
            if (!string.IsNullOrEmpty(definition.SustainedEffectName) && engine.Presentation != null)
            {
                sustainedPresentation = engine.Presentation.PlayPersistentEffect(definition.SustainedEffectName, parent);
            }
        }

        public void SetStackCount(int value)
        {
            StackCount = Mathf.Clamp(value, 1, Mathf.Max(1, Definition.MaxStack));
        }

        public void IncrementStack(int count = 1)
        {
            SetStackCount(StackCount + Mathf.Max(1, count));
        }

        public void Refresh(ModifierApplyOptions options)
        {
            // Refresh handles both duration reset and stack growth according to attributes.
            if ((Definition.Attributes & ModifierAttribute.NoDurationRefresh) == 0)
            {
                Duration = ResolveDuration(Definition, options);
                RemainingTime = Duration;
            }

            if ((Definition.Attributes & ModifierAttribute.StackIndependent) == 0)
            {
                int addStack = options != null ? Mathf.Max(1, options.StackCount) : 1;
                if (Definition.MaxStack > 1)
                {
                    SetStackCount(StackCount + addStack);
                }
            }

            Script.OnRefresh(options);
        }

        internal void Tick(float deltaTime)
        {
            if (IsDestroyed)
            {
                return;
            }

            ElapsedTime += deltaTime;
            // States and properties disappear automatically once a modifier is removed.
            if (ShouldRemoveForDeath())
            {
                Engine.RemoveModifier(this);
                return;
            }

            // Interval actions that complete exactly on the expiry boundary still belong to
            // the modifier's active lifetime. Limit catch-up to the remaining active time so
            // a long frame cannot execute ticks after expiry, then remove the modifier.
            float activeDeltaTime = deltaTime;
            if (Duration > 0f && (Definition.Attributes & ModifierAttribute.Permanent) == 0)
            {
                activeDeltaTime = Mathf.Min(deltaTime, Mathf.Max(0f, RemainingTime));
            }

            TickInterval(activeDeltaTime);
            if (IsDestroyed)
            {
                return;
            }

            TickDuration(deltaTime);
            if (IsDestroyed)
            {
                return;
            }

            TickAura(deltaTime);
        }

        internal void Destroy()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;
            sustainedPresentation?.Stop();
            sustainedPresentation = null;
            Script.OnDestroy();
        }

        internal float GetProperty(ModifierProperty property, ModifierPropertyContext context)
        {
            return Script.GetProperty(property, context);
        }

        internal bool CheckState(UnitState state)
        {
            return Script.CheckState(state);
        }

        private void TickDuration(float deltaTime)
        {
            if (Duration <= 0f || (Definition.Attributes & ModifierAttribute.Permanent) != 0)
            {
                return;
            }

            RemainingTime -= deltaTime;
            if (RemainingTime <= 0f)
            {
                Engine.RemoveModifier(this);
            }
        }

        private void TickInterval(float deltaTime)
        {
            if (Definition.Interval <= 0f)
            {
                return;
            }

            intervalTimer -= deltaTime;
            while (intervalTimer <= 0f && !IsDestroyed)
            {
                intervalTimer += Definition.Interval;
                Script.OnIntervalThink();
            }
        }

        private void TickAura(float deltaTime)
        {
            if (string.IsNullOrEmpty(Definition.AuraModifierName))
            {
                return;
            }

            auraTimer -= deltaTime;
            if (auraTimer > 0f)
            {
                return;
            }

            auraTimer = Mathf.Max(0.05f, Definition.AuraThinkInterval);
            Engine.RefreshAura(this);
        }

        private bool ShouldRemoveForDeath()
        {
            if (Parent == null)
            {
                return true;
            }

            return !Parent.IsAlive && Definition.RemoveOnDeath;
        }

        private static float ResolveDuration(ModifierDefinition definition, ModifierApplyOptions options)
        {
            return options != null && !float.IsNaN(options.Duration) ? options.Duration : definition.Duration;
        }
    }
}
