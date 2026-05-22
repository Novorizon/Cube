using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    public sealed class SkillManager : Singleton<SkillManager>
    {
        private readonly Dictionary<int, SkillConfigData> configMap = new Dictionary<int, SkillConfigData>();
        private readonly Dictionary<int, SkillActionGroup> actionGroupMap = new Dictionary<int, SkillActionGroup>();
        private readonly Dictionary<long, SkillRuntime> runtimeMap = new Dictionary<long, SkillRuntime>();
        private readonly SkillActionExecutor actionExecutor = new SkillActionExecutor();
        private readonly SkillModifierManager modifierManager = new SkillModifierManager();
        private readonly SkillAbilityBook abilityBook = new SkillAbilityBook();

        private ISkillWorld world;
        private ISkillEffectService effectService;
        private SkillEventData lastEventData;
        private bool initialized;

        public ISkillWorld World => world;
        public ISkillEffectService EffectService => effectService;
        public SkillModifierManager ModifierManager => modifierManager;
        public SkillAbilityBook AbilityBook => abilityBook;
        public SkillEventData LastEventData => lastEventData;

        public void Initialize(ISkillWorld world, ISkillEffectService effectService)
        {
            this.world = world;
            this.effectService = effectService;

            configMap.Clear();
            actionGroupMap.Clear();
            runtimeMap.Clear();
            abilityBook.Clear();

            RegisterBuiltinActions();
            modifierManager.Initialize(this);

            initialized = true;
            Debug.Log("SkillManager initialized.");
        }

        public void Update(float deltaTime)
        {
            if (!initialized)
            {
                return;
            }

            UpdateCasting(deltaTime);
            UpdateCooldown(deltaTime);
            modifierManager.Update(deltaTime);
        }

        public void RegisterConfig(SkillConfigData config)
        {
            if (config == null)
            {
                return;
            }

            configMap[config.Id] = config;
        }

        public void RegisterAction(SkillActionData actionData)
        {
            if (actionData == null)
            {
                return;
            }

            if (!actionGroupMap.TryGetValue(actionData.GroupId, out SkillActionGroup group))
            {
                group = new SkillActionGroup();
                actionGroupMap.Add(actionData.GroupId, group);
            }

            group.Add(actionData);
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
            if (!TryPrepareCast(request, out SkillConfigData config, out SkillRuntime runtime, out SkillContext context))
            {
                return false;
            }

            if (config.CastPoint > 0f)
            {
                runtime.IsCasting = true;
                runtime.CastPointLeft = config.CastPoint;
                runtime.PendingRequest = request;
                FireEvent(CreateEventData(SkillMessageTopic.CastStarted, context));
                return true;
            }

            return ExecutePreparedCast(config, runtime, context, request);
        }

        public bool Interrupt(ISkillUnit owner, int skillId)
        {
            if (owner == null)
            {
                return false;
            }

            SkillRuntime runtime = GetRuntime(owner.RuntimeId, skillId);

            if (!runtime.IsCasting && !runtime.IsChanneling)
            {
                return false;
            }

            runtime.IsCasting = false;
            runtime.CastPointLeft = 0f;
            runtime.IsChanneling = false;
            runtime.ChannelTimeLeft = 0f;
            runtime.PendingRequest = null;
            FireEvent(SkillMessageTopic.CastInterrupted);
            return true;
        }

        public bool AddModifier(ISkillUnit caster, ISkillUnit target, int modifierId, float duration)
        {
            return modifierManager.AddModifier(caster, target, modifierId, duration);
        }

        public int PurgeDebuffs(ISkillUnit unit)
        {
            return modifierManager.RemoveModifiers(unit, true, true);
        }

        public int RemoveAllModifiers(ISkillUnit unit)
        {
            return modifierManager.RemoveModifiers(unit, false, false);
        }

        public void ExecuteActionGroup(int actionGroupId, SkillContext context)
        {
            if (actionGroupId <= 0 || context == null)
            {
                return;
            }

            if (!actionGroupMap.TryGetValue(actionGroupId, out SkillActionGroup group))
            {
                return;
            }

            IReadOnlyList<SkillActionData> actions = group.Actions;

            for (int i = 0; i < actions.Count; i++)
            {
                actionExecutor.Execute(actions[i], context, this);
                FireEvent(CreateEventData(SkillMessageTopic.ActionExecuted, context, actions[i]));
            }
        }

        public float GetCooldownLeft(ISkillUnit owner, int skillId)
        {
            if (owner == null)
            {
                return 0f;
            }

            SkillRuntime runtime = GetRuntime(owner.RuntimeId, skillId);
            return runtime.CooldownLeft;
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
            lastEventData = eventData;
            Messager.Instance.Notify(eventData.Topic);
        }

        private bool TryPrepareCast(SkillCastRequest request, out SkillConfigData config, out SkillRuntime runtime, out SkillContext context)
        {
            config = null;
            runtime = null;
            context = null;

            if (request == null || request.Caster == null)
            {
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            if (!configMap.TryGetValue(request.SkillId, out config))
            {
                Debug.LogWarning($"Cast skill failed. Missing config. skillId: {request.SkillId}");
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            if (!CanCast(config, request))
            {
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            runtime = GetRuntime(request.Caster.RuntimeId, config.Id);
            context = BuildContext(config, runtime, request);

            if (!SkillTargetSystem.BuildTargets(context))
            {
                Debug.LogWarning($"Cast skill failed. Invalid target. skillId: {config.Id}");
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            return true;
        }

        private bool ExecutePreparedCast(SkillConfigData config, SkillRuntime runtime, SkillContext context, SkillCastRequest request)
        {
            if (!PayCost(config, request))
            {
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            ExecuteActionGroup(config.AbilityActionGroupId, context);

            if ((config.Behavior & SkillAbilityBehavior.Channel) != 0 && config.ChannelTime > 0f)
            {
                runtime.IsChanneling = true;
                runtime.ChannelTimeLeft = config.ChannelTime;
            }

            if (config.Cooldown > 0f)
            {
                runtime.CooldownLeft = config.Cooldown;
                FireEvent(CreateEventData(SkillMessageTopic.CooldownStarted, context));
            }

            FireEvent(CreateEventData(SkillMessageTopic.CastSucceeded, context));
            return true;
        }

        private SkillContext BuildContext(SkillConfigData config, SkillRuntime runtime, SkillCastRequest request)
        {
            SkillContext context = new SkillContext();
            context.Config = config;
            context.Runtime = runtime;
            context.Caster = request.Caster;
            context.TargetUnit = request.TargetUnit;
            context.TargetPosition = request.TargetPosition;
            context.World = world;
            context.EffectService = effectService;
            return context;
        }

        private bool CanCast(SkillConfigData config, SkillCastRequest request)
        {
            if (config == null || !config.Enable)
            {
                return false;
            }

            if ((config.Behavior & SkillAbilityBehavior.Passive) != 0)
            {
                return false;
            }

            if (HasState(request.Caster, SkillUnitState.Stunned) || HasState(request.Caster, SkillUnitState.Silenced))
            {
                return false;
            }

            SkillRuntime runtime = GetRuntime(request.Caster.RuntimeId, config.Id);

            if (runtime.CooldownLeft > 0f || runtime.IsCasting || runtime.IsChanneling)
            {
                return false;
            }

            if (config.CostResourceId > 0 && config.CostCount > 0)
            {
                if (request.ResourceOwner == null)
                {
                    return false;
                }

                if (!request.ResourceOwner.HasResource(config.CostResourceId, config.CostCount))
                {
                    return false;
                }
            }

            return true;
        }

        private bool PayCost(SkillConfigData config, SkillCastRequest request)
        {
            if (config.CostResourceId <= 0 || config.CostCount <= 0)
            {
                return true;
            }

            if (request.ResourceOwner == null)
            {
                return false;
            }

            return request.ResourceOwner.TryConsumeResource(config.CostResourceId, config.CostCount);
        }

        private SkillRuntime GetRuntime(int ownerRuntimeId, int skillId)
        {
            long key = GetRuntimeKey(ownerRuntimeId, skillId);

            if (!runtimeMap.TryGetValue(key, out SkillRuntime runtime))
            {
                runtime = new SkillRuntime(ownerRuntimeId, skillId);
                runtimeMap.Add(key, runtime);
            }

            return runtime;
        }

        private static long GetRuntimeKey(int ownerRuntimeId, int skillId)
        {
            return ((long)ownerRuntimeId << 32) ^ (uint)skillId;
        }

        private void UpdateCasting(float deltaTime)
        {
            foreach (SkillRuntime runtime in runtimeMap.Values)
            {
                if (runtime.IsCasting)
                {
                    runtime.CastPointLeft -= deltaTime;

                    if (runtime.CastPointLeft <= 0f && runtime.PendingRequest != null)
                    {
                        SkillCastRequest request = runtime.PendingRequest;
                        runtime.IsCasting = false;
                        runtime.CastPointLeft = 0f;
                        runtime.PendingRequest = null;

                        if (TryPrepareCast(request, out SkillConfigData config, out SkillRuntime preparedRuntime, out SkillContext context))
                        {
                            ExecutePreparedCast(config, preparedRuntime, context, request);
                        }
                    }
                }

                if (runtime.IsChanneling)
                {
                    runtime.ChannelTimeLeft -= deltaTime;

                    if (runtime.ChannelTimeLeft <= 0f)
                    {
                        runtime.IsChanneling = false;
                        runtime.ChannelTimeLeft = 0f;
                        FireEvent(SkillMessageTopic.ChannelFinished);
                    }
                }
            }
        }

        private void UpdateCooldown(float deltaTime)
        {
            foreach (SkillRuntime runtime in runtimeMap.Values)
            {
                if (runtime.CooldownLeft <= 0f)
                {
                    continue;
                }

                runtime.CooldownLeft -= deltaTime;

                if (runtime.CooldownLeft <= 0f)
                {
                    runtime.CooldownLeft = 0f;
                    FireEvent(SkillMessageTopic.CooldownFinished);
                }
            }
        }

        private SkillEventData CreateEventData(SkillMessageTopic topic, SkillContext context, SkillActionData actionData = null)
        {
            SkillEventData eventData = new SkillEventData();
            eventData.Topic = topic;
            eventData.SkillId = context != null && context.Config != null ? context.Config.Id : 0;
            eventData.ActionId = actionData != null ? actionData.Id : 0;
            eventData.Caster = context != null ? context.Caster : null;
            eventData.Target = context != null ? context.TargetUnit : null;
            eventData.Position = context != null ? context.TargetPosition : Vector3.zero;
            eventData.Value = actionData != null ? actionData.Value : 0f;
            return eventData;
        }

        private void RegisterBuiltinActions()
        {
            actionExecutor.Register(new DamageAction());
            actionExecutor.Register(new HealAction());
            actionExecutor.Register(new ApplyModifierAction());
            actionExecutor.Register(new FireEventAction());
        }
    }
}
