using System.Collections.Generic;

namespace Game.Skill
{
    /// <summary>
    /// Modifier 生命周期系统。
    /// Modifier 是持续状态的统一表达：减速、眩晕、中毒、被动属性、周期伤害、事件触发等都在这里管理。
    /// 业务层不应该直接修改 Modifier 列表，而应该通过 SkillManager 暴露的方法添加、驱散或查询。
    /// </summary>
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

        /// <summary>
        /// 注册特殊 C# Modifier 逻辑。普通 Modifier 走 DefaultSkillModifierLogic 即可。
        /// </summary>
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

        /// <summary>
        /// 添加 Modifier。若目标身上已有同 id Modifier，则刷新持续时间并按 MaxStack 叠层。
        /// </summary>
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

        /// <summary>
        /// 移除 Modifier。debuffOnly 用于只驱散负面效果，purgableOnly 用于只移除可驱散效果。
        /// </summary>
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

        /// <summary>
        /// 处理业务层传入的战斗事件，例如攻击命中、受伤、死亡。
        /// 满足 TriggerEventType 的 Modifier 会执行配置的 TriggerActionGroup，并通知 C# ModifierLogic。
        /// </summary>
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

        /// <summary>
        /// 查询某单位当前所有 Modifier 对某个属性的修正总和。
        /// 例如 MoveSpeedPercent = -40 表示基础速度乘以 0.6。
        /// </summary>
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

        /// <summary>
        /// 查询单位是否被某种状态影响，例如 Stunned、Silenced。
        /// </summary>
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