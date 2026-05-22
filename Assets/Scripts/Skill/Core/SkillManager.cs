using Game.Framework;
using UnityEngine;

namespace Game.Skill
{
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

        public ISkillWorld World => world;
        public ISkillEffectService EffectService => effectService;
        public SkillEventDispatcher EventDispatcher => eventDispatcher;
        public SkillModifierManager ModifierManager => modifierManager;
        public SkillAbilityBook AbilityBook => abilityBook;
        public SkillActionSystem ActionSystem => actionSystem;
        public SkillCastSystem CastSystem => castSystem;

        public SkillManager()
        {
            actionSystem = new SkillActionSystem(this);
            modifierManager = new SkillModifierManager();
            abilityBook = new SkillAbilityBook(this);
            castSystem = new SkillCastSystem(this);
        }

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
