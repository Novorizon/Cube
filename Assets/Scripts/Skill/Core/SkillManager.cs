using Game.Framework;
using UnityEngine;

namespace Game.Skill
{
    /// <summary>
    /// 技能系统对业务层暴露的唯一门面。
    /// 业务层只应该通过 SkillManager 初始化、注册配置、释放技能、查询 Modifier 结果。
    /// SkillManager 可以引用技能底层各子系统；业务层不应该直接操作子系统内部状态。
    /// </summary>
    public sealed class SkillManager : Singleton<SkillManager>
    {
        private readonly SkillEventDispatcher eventDispatcher = new SkillEventDispatcher();
        private readonly SkillActionSystem actionSystem;
        private readonly SkillModifierManager modifierManager;
        private readonly SkillAbilityBook abilityBook;
        private readonly SkillCastSystem castSystem;

        private ISkillWorld world;
        private ISkillEffectService effectService;
        private bool initialized;

        /// <summary>
        /// 技能系统查询战场单位、视野、时间等信息的世界接口，由业务层适配实现。
        /// </summary>
        public ISkillWorld World => world;

        /// <summary>
        /// 技能系统播放表现效果的接口，由业务层适配实现。
        /// </summary>
        public ISkillEffectService EffectService => effectService;

        /// <summary>
        /// 技能事件分发器。业务层可以订阅它来刷新 UI、播放表现或记录日志。
        /// </summary>
        public SkillEventDispatcher EventDispatcher => eventDispatcher;

        /// <summary>
        /// Modifier 子系统，负责 Buff、Debuff、状态、属性修正和周期触发。
        /// </summary>
        public SkillModifierManager ModifierManager => modifierManager;

        /// <summary>
        /// 单位技能持有关系。用于被动技能、技能栏、单位拥有技能的释放方式。
        /// </summary>
        public SkillAbilityBook AbilityBook => abilityBook;

        /// <summary>
        /// Action 子系统，负责根据 groupId 执行技能动作组。
        /// </summary>
        public SkillActionSystem ActionSystem => actionSystem;

        /// <summary>
        /// 施法子系统，负责释放检查、前摇、引导、冷却和资源消耗。
        /// </summary>
        public SkillCastSystem CastSystem => castSystem;

        public SkillManager()
        {
            // 当前实现中，子系统仍通过注入的 SkillManager 门面互相访问。
            // 这避免了 SkillManager.Instance 硬引用，但仍不是完全单向依赖。
            // 未来如果继续收敛，可以用 SkillRuntimeServices 代替这里的门面反向引用。
            actionSystem = new SkillActionSystem(this);
            modifierManager = new SkillModifierManager();
            abilityBook = new SkillAbilityBook(this);
            castSystem = new SkillCastSystem(this);
        }

        /// <summary>
        /// 初始化技能系统。业务层必须先提供世界接口和表现接口，再注册配置和释放技能。
        /// </summary>
        public void Initialize(ISkillWorld world, ISkillEffectService effectService)
        {
            this.world = world;
            this.effectService = effectService;

            eventDispatcher.Clear();
            abilityBook.Clear();
            actionSystem.Initialize();
            modifierManager.Initialize(this);
            castSystem.Initialize();

            initialized = true;
            Debug.Log("SkillManager initialized.");
        }

        /// <summary>
        /// 每帧驱动施法、冷却、引导和 Modifier 生命周期。
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!initialized)
            {
                return;
            }

            castSystem.Update(deltaTime);
            modifierManager.Update(deltaTime);
        }

        public void RegisterConfig(SkillConfigData config)
        {
            castSystem.RegisterConfig(config);
        }

        public void RegisterAction(SkillActionData actionData)
        {
            actionSystem.RegisterAction(actionData);
        }

        public void RegisterActionHandler(ISkillAction action)
        {
            actionSystem.RegisterActionHandler(action);
        }

        public void RegisterModifier(SkillModifierData modifierData)
        {
            modifierManager.RegisterModifier(modifierData);
        }

        public void RegisterModifierLogic(int modifierId, ISkillModifierLogic logic)
        {
            modifierManager.RegisterModifierLogic(modifierId, logic);
        }

        public bool AddAbility(ISkillUnit owner, SkillConfigData config, ISkillResourceOwner resourceOwner = null)
        {
            return abilityBook.AddAbility(owner, config, resourceOwner);
        }

        public bool CastOwnedAbility(ISkillUnit owner, int skillId, ISkillUnit targetUnit = null)
        {
            return abilityBook.Cast(owner, skillId, targetUnit);
        }

        public bool Cast(SkillCastRequest request)
        {
            return castSystem.Cast(request);
        }

        public bool Interrupt(ISkillUnit owner, int skillId)
        {
            return castSystem.Interrupt(owner, skillId);
        }

        public bool AddModifier(ISkillUnit caster, ISkillUnit target, int modifierId, float duration, int sourceSkillId = 0)
        {
            return modifierManager.AddModifier(caster, target, modifierId, duration, sourceSkillId);
        }

        public int PurgeDebuffs(ISkillUnit unit)
        {
            return modifierManager.RemoveModifiers(unit, true, true);
        }

        public int RemoveAllModifiers(ISkillUnit unit)
        {
            return modifierManager.RemoveModifiers(unit, false, false);
        }

        public void HandleTriggerEvent(SkillTriggerEvent triggerEvent)
        {
            modifierManager.HandleTriggerEvent(triggerEvent);
        }

        public void ExecuteActionGroup(int actionGroupId, SkillContext context)
        {
            actionSystem.ExecuteActionGroup(actionGroupId, context);
        }

        public float GetCooldownLeft(ISkillUnit owner, int skillId)
        {
            return castSystem.GetCooldownLeft(owner, skillId);
        }

        public bool HasState(ISkillUnit unit, SkillUnitState state)
        {
            return modifierManager.HasState(unit, state);
        }

        public float GetModifierProperty(ISkillUnit unit, SkillModifierPropertyType propertyType)
        {
            return modifierManager.GetProperty(unit, propertyType);
        }

        public void FireEvent(SkillMessageTopic topic)
        {
            SkillEventData eventData = new SkillEventData();
            eventData.Topic = topic;
            FireEvent(eventData);
        }

        public void FireEvent(SkillEventData eventData)
        {
            eventDispatcher.Publish(eventData);
        }
    }
}