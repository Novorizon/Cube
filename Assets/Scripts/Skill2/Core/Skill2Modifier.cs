using UnityEngine;

namespace Skill2
{
    public sealed class SkillModifier
    {
        public Skill2Engine Engine { get; }
        public string Name => Definition.Name;
        public SkillModifierDefinition Definition { get; }
        public SkillModifierScript Script { get; }
        public ISkill2Unit Caster { get; }
        public ISkill2Unit Parent { get; }
        public SkillAbility Ability { get; }
        public SkillModifier SourceAura { get; }
        public bool IsAuraApplied { get; }
        public int StackCount { get; private set; }
        public float Duration { get; private set; }
        public float RemainingTime { get; private set; }
        public float ElapsedTime { get; private set; }
        public bool IsDestroyed { get; private set; }

        private float intervalTimer;
        private float auraTimer;

        internal SkillModifier(Skill2Engine engine, SkillModifierDefinition definition, ISkill2Unit caster, ISkill2Unit parent, SkillAbility ability, SkillModifierScript script, SkillModifierApplyOptions options)
        {
            Engine = engine;
            Definition = definition;
            Caster = caster;
            Parent = parent;
            Ability = ability;
            Script = script ?? new DefaultSkillModifierScript();
            SourceAura = options != null ? options.SourceModifier : null;
            IsAuraApplied = options != null && options.IsAura;
            StackCount = Mathf.Max(1, options != null ? options.StackCount : 1);
            Duration = ResolveDuration(definition, options);
            RemainingTime = Duration;
            intervalTimer = definition.Interval;
            auraTimer = 0f;
            Script.Bind(this);
            Script.OnCreated(options);
        }

        public void SetStackCount(int value)
        {
            StackCount = Mathf.Clamp(value, 1, Mathf.Max(1, Definition.MaxStack));
        }

        public void IncrementStack(int count = 1)
        {
            SetStackCount(StackCount + Mathf.Max(1, count));
        }

        public void Refresh(SkillModifierApplyOptions options)
        {
            if ((Definition.Attributes & SkillModifierAttribute.NoDurationRefresh) == 0)
            {
                Duration = ResolveDuration(Definition, options);
                RemainingTime = Duration;
            }

            if ((Definition.Attributes & SkillModifierAttribute.StackIndependent) == 0)
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

            if (ShouldRemoveForDeath())
            {
                Engine.RemoveModifier(this);
                return;
            }

            TickDuration(deltaTime);
            if (IsDestroyed)
            {
                return;
            }

            TickInterval(deltaTime);
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
            Script.OnDestroy();
        }

        internal float GetProperty(SkillModifierProperty property, SkillModifierPropertyContext context)
        {
            return Script.GetProperty(property, context);
        }

        internal bool CheckState(SkillUnitState state)
        {
            return Script.CheckState(state);
        }

        private void TickDuration(float deltaTime)
        {
            if (Duration <= 0f || (Definition.Attributes & SkillModifierAttribute.Permanent) != 0)
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
            if (intervalTimer > 0f)
            {
                return;
            }

            intervalTimer += Definition.Interval;
            Script.OnIntervalThink();
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

            if (Parent.IsAlive)
            {
                return false;
            }

            return Definition.RemoveOnDeath;
        }

        private static float ResolveDuration(SkillModifierDefinition definition, SkillModifierApplyOptions options)
        {
            if (options != null && !float.IsNaN(options.Duration))
            {
                return options.Duration;
            }

            return definition.Duration;
        }
    }
}
