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

        private ISkillWorld world;
        private ISkillEffectService effectService;
        private bool initialized;

        public ISkillWorld World
        {
            get
            {
                return world;
            }
        }

        public ISkillEffectService EffectService
        {
            get
            {
                return effectService;
            }
        }

        public SkillModifierManager ModifierManager
        {
            get
            {
                return modifierManager;
            }
        }

        public void Initialize(ISkillWorld world, ISkillEffectService effectService)
        {
            this.world = world;
            this.effectService = effectService;

            configMap.Clear();
            actionGroupMap.Clear();
            runtimeMap.Clear();

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

        public bool Cast(SkillCastRequest request)
        {
            if (request == null || request.Caster == null)
            {
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            if (!configMap.TryGetValue(request.SkillId, out SkillConfigData config))
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

            SkillRuntime runtime = GetRuntime(request.Caster.RuntimeId, config.Id);
            SkillContext context = BuildContext(config, runtime, request);

            if (!SkillTargetSystem.BuildTargets(context))
            {
                Debug.LogWarning($"Cast skill failed. Invalid target. skillId: {config.Id}");
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            if (!PayCost(config, request))
            {
                FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            ExecuteActionGroup(config.AbilityActionGroupId, context);

            if (config.Cooldown > 0f)
            {
                runtime.CooldownLeft = config.Cooldown;
                FireEvent(SkillMessageTopic.CooldownStarted);
            }

            FireEvent(SkillMessageTopic.CastSucceeded);
            return true;
        }

        public bool AddModifier(ISkillUnit caster, ISkillUnit target, int modifierId, float duration)
        {
            return modifierManager.AddModifier(caster, target, modifierId, duration);
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
                FireEvent(SkillMessageTopic.ActionExecuted);
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
            Messager.Instance.Notify(topic);
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

            if (HasState(request.Caster, SkillUnitState.Stunned) || HasState(request.Caster, SkillUnitState.Silenced))
            {
                return false;
            }

            SkillRuntime runtime = GetRuntime(request.Caster.RuntimeId, config.Id);

            if (runtime.CooldownLeft > 0f)
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

        private void RegisterBuiltinActions()
        {
            actionExecutor.Register(new DamageAction());
            actionExecutor.Register(new HealAction());
            actionExecutor.Register(new ApplyModifierAction());
            actionExecutor.Register(new FireEventAction());
        }
    }
}
