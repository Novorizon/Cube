using System.Collections.Generic;

namespace Game.Skill
{
    public sealed class SkillModifierManager
    {
        private readonly List<SkillModifierInstance> modifiers = new List<SkillModifierInstance>();
        private readonly Dictionary<int, SkillModifierData> modifierMap = new Dictionary<int, SkillModifierData>();
        private readonly Dictionary<int, ISkillModifierLogic> logicMap = new Dictionary<int, ISkillModifierLogic>();
        private readonly ISkillModifierLogic defaultLogic = new DefaultSkillModifierLogic();

        private SkillManager skillManager;

        public IReadOnlyList<SkillModifierInstance> Modifiers => modifiers;

        public void Initialize(SkillManager skillManager)
        {
            this.skillManager = skillManager;
            modifiers.Clear();
            logicMap.Clear();
        }

        public void RegisterModifier(SkillModifierData modifierData)
        {
            if (modifierData == null)
            {
                return;
            }

            modifierMap[modifierData.Id] = modifierData;
        }

        public void RegisterModifierLogic(int modifierId, ISkillModifierLogic logic)
        {
            if (modifierId <= 0 || logic == null)
            {
                return;
            }

            logicMap[modifierId] = logic;
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

                if (UpdateDuration(instance, deltaTime, i))
                {
                    continue;
                }

                UpdateInterval(instance, deltaTime);
            }
        }

        public bool AddModifier(ISkillUnit caster, ISkillUnit target, int modifierId, float duration, int sourceSkillId = 0)
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
            instance.SourceSkillId = sourceSkillId;
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
            GetLogic(instance).OnCreated(CreateContext(instance));
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

        public void HandleTriggerEvent(SkillTriggerEvent triggerEvent)
        {
            if (triggerEvent == null || triggerEvent.EventType == SkillTriggerEventType.None)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifierInstance instance = modifiers[i];

                if (instance == null || instance.Data == null)
                {
                    continue;
                }

                if (!IsTriggerRelated(instance, triggerEvent))
                {
                    continue;
                }

                GetLogic(instance).OnTriggerEvent(CreateContext(instance), triggerEvent);

                if (instance.Data.TriggerEventType != triggerEvent.EventType || instance.Data.TriggerActionGroupId <= 0)
                {
                    continue;
                }

                ExecuteTriggerActionGroup(instance.Data.TriggerActionGroupId, instance, triggerEvent);
            }
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

                if (instance.Parent != unit)
                {
                    continue;
                }

                value += GetLogic(instance).GetProperty(CreateContext(instance), propertyType);
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

                if (instance.Parent != unit)
                {
                    continue;
                }

                if (GetLogic(instance).HasState(CreateContext(instance), state))
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

        private bool UpdateDuration(SkillModifierInstance instance, float deltaTime, int index)
        {
            if (instance.Duration <= 0f)
            {
                return false;
            }

            instance.TimeLeft -= deltaTime;

            if (instance.TimeLeft > 0f)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        private void UpdateInterval(SkillModifierInstance instance, float deltaTime)
        {
            if (instance.Interval <= 0f || instance.Data == null)
            {
                return;
            }

            instance.IntervalTimer -= deltaTime;

            if (instance.IntervalTimer > 0f)
            {
                return;
            }

            instance.IntervalTimer = instance.Interval;
            ExecuteActionGroup(instance.Data.PeriodicActionGroupId, instance);
            GetLogic(instance).OnInterval(CreateContext(instance));
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

        private bool IsTriggerRelated(SkillModifierInstance instance, SkillTriggerEvent triggerEvent)
        {
            return triggerEvent.Source == instance.Parent || triggerEvent.Target == instance.Parent;
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
            GetLogic(instance).OnRefresh(CreateContext(instance));
        }

        private void RemoveAt(int index)
        {
            SkillModifierInstance instance = modifiers[index];
            ExecuteActionGroup(instance.Data != null ? instance.Data.OnDestroyActionGroupId : 0, instance);
            GetLogic(instance).OnRemoved(CreateContext(instance));
            modifiers.RemoveAt(index);
            skillManager.FireEvent(SkillMessageTopic.ModifierRemoved);
        }

        private ISkillModifierLogic GetLogic(SkillModifierInstance instance)
        {
            if (instance != null && logicMap.TryGetValue(instance.ModifierId, out ISkillModifierLogic logic))
            {
                return logic;
            }

            return defaultLogic;
        }

        private SkillModifierContext CreateContext(SkillModifierInstance instance)
        {
            SkillModifierContext context = new SkillModifierContext();
            context.Instance = instance;
            context.SkillManager = skillManager;
            return context;
        }

        private void ExecuteActionGroup(int actionGroupId, SkillModifierInstance instance)
        {
            if (actionGroupId <= 0 || instance == null || skillManager == null)
            {
                return;
            }

            SkillContext context = new SkillContext();
            context.SkillId = instance.SourceSkillId;
            context.ActionGroupId = actionGroupId;
            context.Caster = instance.Caster;
            context.TargetUnit = instance.Parent;
            context.Targets.Add(instance.Parent);
            context.World = skillManager.World;
            context.EffectService = skillManager.EffectService;
            skillManager.ExecuteActionGroup(actionGroupId, context);
        }

        private void ExecuteTriggerActionGroup(int actionGroupId, SkillModifierInstance instance, SkillTriggerEvent triggerEvent)
        {
            if (actionGroupId <= 0 || instance == null || triggerEvent == null || skillManager == null)
            {
                return;
            }

            SkillContext context = new SkillContext();
            context.SkillId = instance.SourceSkillId > 0 ? instance.SourceSkillId : triggerEvent.SkillId;
            context.ActionGroupId = actionGroupId;
            context.Caster = instance.Caster;
            context.TargetUnit = triggerEvent.Target != null ? triggerEvent.Target : instance.Parent;
            context.TargetPosition = triggerEvent.Position;
            context.Targets.Add(context.TargetUnit);
            context.World = skillManager.World;
            context.EffectService = skillManager.EffectService;
            skillManager.ExecuteActionGroup(actionGroupId, context);
        }
    }
}
