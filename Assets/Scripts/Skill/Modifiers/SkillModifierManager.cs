using System.Collections.Generic;

namespace Game.Skill
{
    public sealed class SkillModifierManager
    {
        private readonly List<SkillModifierInstance> modifiers = new List<SkillModifierInstance>();
        private readonly Dictionary<int, SkillModifierData> modifierMap = new Dictionary<int, SkillModifierData>();
        private SkillManager skillManager;

        public IReadOnlyList<SkillModifierInstance> Modifiers => modifiers;

        public void Initialize(SkillManager skillManager)
        {
            this.skillManager = skillManager;
            modifiers.Clear();
        }

        public void RegisterModifier(SkillModifierData modifierData)
        {
            if (modifierData == null)
            {
                return;
            }

            modifierMap[modifierData.Id] = modifierData;
        }

        public void Update(float deltaTime)
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                SkillModifierInstance instance = modifiers[i];

                if (ShouldRemoveForDeadParent(instance))
                {
                    RemoveAt(i);
                    continue;
                }

                if (instance.Duration > 0f)
                {
                    instance.TimeLeft -= deltaTime;

                    if (instance.TimeLeft <= 0f)
                    {
                        RemoveAt(i);
                        continue;
                    }
                }

                if (instance.Interval > 0f)
                {
                    instance.IntervalTimer -= deltaTime;

                    if (instance.IntervalTimer <= 0f)
                    {
                        instance.IntervalTimer = instance.Interval;
                        ExecuteActionGroup(instance.Data.PeriodicActionGroupId, instance);
                    }
                }
            }
        }

        public bool AddModifier(ISkillUnit caster, ISkillUnit target, int modifierId, float duration)
        {
            if (target == null || !modifierMap.TryGetValue(modifierId, out SkillModifierData modifierData))
            {
                return false;
            }

            SkillModifierInstance existing = FindModifier(target, modifierId);

            if (existing != null)
            {
                RefreshModifier(existing, duration);
                return true;
            }

            SkillModifierInstance instance = new SkillModifierInstance();
            instance.ModifierId = modifierId;
            instance.Data = modifierData;
            instance.Caster = caster;
            instance.Parent = target;
            instance.Duration = duration > 0f ? duration : modifierData.Duration;
            instance.TimeLeft = instance.Duration;
            instance.Interval = modifierData.Interval;
            instance.IntervalTimer = modifierData.Interval;
            instance.StackCount = 1;

            modifiers.Add(instance);
            ExecuteActionGroup(modifierData.OnCreatedActionGroupId, instance);
            skillManager.FireEvent(SkillMessageTopic.ModifierAdded);
            return true;
        }

        public int RemoveModifiers(ISkillUnit unit, bool debuffOnly, bool purgableOnly)
        {
            int count = 0;

            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                SkillModifierInstance instance = modifiers[i];

                if (instance.Parent != unit || instance.Data == null)
                {
                    continue;
                }

                if (debuffOnly && !instance.Data.IsDebuff)
                {
                    continue;
                }

                if (purgableOnly && !instance.Data.IsPurgable)
                {
                    continue;
                }

                RemoveAt(i);
                count++;
            }

            return count;
        }

        public float GetProperty(ISkillUnit unit, SkillModifierPropertyType propertyType)
        {
            if (unit == null || propertyType == SkillModifierPropertyType.None)
            {
                return 0f;
            }

            float value = 0f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifierInstance instance = modifiers[i];

                if (instance.Parent != unit || instance.Data == null || instance.Data.PropertyType != propertyType)
                {
                    continue;
                }

                value += instance.Data.PropertyValue * instance.StackCount;
            }

            return value;
        }

        public bool HasState(ISkillUnit unit, SkillUnitState state)
        {
            if (unit == null || state == SkillUnitState.None)
            {
                return false;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifierInstance instance = modifiers[i];

                if (instance.Parent == unit && instance.Data != null && instance.Data.State == state)
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                RemoveAt(i);
            }
        }

        private bool ShouldRemoveForDeadParent(SkillModifierInstance instance)
        {
            if (instance == null || instance.Parent == null)
            {
                return true;
            }

            if (instance.Parent.IsAlive)
            {
                return false;
            }

            return instance.Data == null || instance.Data.RemoveOnDeath;
        }

        private SkillModifierInstance FindModifier(ISkillUnit target, int modifierId)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifierInstance instance = modifiers[i];

                if (instance.Parent == target && instance.ModifierId == modifierId)
                {
                    return instance;
                }
            }

            return null;
        }

        private void RefreshModifier(SkillModifierInstance instance, float duration)
        {
            if (instance == null || instance.Data == null)
            {
                return;
            }

            if (instance.Data.MaxStack > 1 && instance.StackCount < instance.Data.MaxStack)
            {
                instance.StackCount++;
            }

            instance.TimeLeft = duration > 0f ? duration : instance.Duration;
        }

        private void RemoveAt(int index)
        {
            SkillModifierInstance instance = modifiers[index];
            ExecuteActionGroup(instance.Data != null ? instance.Data.OnDestroyActionGroupId : 0, instance);
            modifiers.RemoveAt(index);
            skillManager.FireEvent(SkillMessageTopic.ModifierRemoved);
        }

        private void ExecuteActionGroup(int actionGroupId, SkillModifierInstance instance)
        {
            if (actionGroupId <= 0 || instance == null || skillManager == null)
            {
                return;
            }

            SkillContext context = new SkillContext();
            context.Config = SkillConfigData.Empty;
            context.Caster = instance.Caster;
            context.TargetUnit = instance.Parent;
            context.Targets.Add(instance.Parent);
            context.World = skillManager.World;
            context.EffectService = skillManager.EffectService;
            skillManager.ExecuteActionGroup(actionGroupId, context);
        }
    }
}
