using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    public sealed class SkillCastSystem
    {
        private readonly Dictionary<int, SkillConfigData> configMap = new Dictionary<int, SkillConfigData>();
        private readonly Dictionary<long, SkillRuntime> runtimeMap = new Dictionary<long, SkillRuntime>();
        private readonly SkillManager skillManager;

        public SkillCastSystem(SkillManager skillManager)
        {
            this.skillManager = skillManager;
        }

        public void Initialize()
        {
            configMap.Clear();
            runtimeMap.Clear();
        }

        public void RegisterConfig(SkillConfigData config)
        {
            if (config == null)
            {
                return;
            }

            configMap[config.Id] = config;
        }

        public void Update(float deltaTime)
        {
            UpdateCasting(deltaTime);
            UpdateCooldown(deltaTime);
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
                skillManager.FireEvent(CreateEventData(SkillMessageTopic.CastStarted, context));
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
            skillManager.FireEvent(SkillMessageTopic.CastInterrupted);
            return true;
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

        private bool TryPrepareCast(SkillCastRequest request, out SkillConfigData config, out SkillRuntime runtime, out SkillContext context)
        {
            config = null;
            runtime = null;
            context = null;

            if (request == null || request.Caster == null)
            {
                skillManager.FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            if (!configMap.TryGetValue(request.SkillId, out config))
            {
                Debug.LogWarning($"Cast skill failed. Missing config. skillId: {request.SkillId}");
                skillManager.FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            if (!CanCast(config, request))
            {
                skillManager.FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            runtime = GetRuntime(request.Caster.RuntimeId, config.Id);
            context = BuildContext(config, runtime, request);

            if (!SkillTargetSystem.BuildTargets(context))
            {
                Debug.LogWarning($"Cast skill failed. Invalid target. skillId: {config.Id}");
                skillManager.FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            return true;
        }

        private bool ExecutePreparedCast(SkillConfigData config, SkillRuntime runtime, SkillContext context, SkillCastRequest request)
        {
            if (!PayCost(config, request))
            {
                skillManager.FireEvent(SkillMessageTopic.CastFailed);
                return false;
            }

            skillManager.ExecuteActionGroup(config.AbilityActionGroupId, context);

            if ((config.Behavior & SkillAbilityBehavior.Channel) != 0 && config.ChannelTime > 0f)
            {
                runtime.IsChanneling = true;
                runtime.ChannelTimeLeft = config.ChannelTime;
            }

            if (config.Cooldown > 0f)
            {
                runtime.CooldownLeft = config.Cooldown;
                skillManager.FireEvent(CreateEventData(SkillMessageTopic.CooldownStarted, context));
            }

            skillManager.FireEvent(CreateEventData(SkillMessageTopic.CastSucceeded, context));
            return true;
        }

        private SkillContext BuildContext(SkillConfigData config, SkillRuntime runtime, SkillCastRequest request)
        {
            SkillContext context = new SkillContext();
            context.SkillId = config.Id;
            context.Config = config;
            context.Runtime = runtime;
            context.Caster = request.Caster;
            context.TargetUnit = request.TargetUnit;
            context.TargetPosition = request.TargetPosition;
            context.World = skillManager.World;
            context.EffectService = skillManager.EffectService;
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

            if (skillManager.HasState(request.Caster, SkillUnitState.Stunned) || skillManager.HasState(request.Caster, SkillUnitState.Silenced))
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
                        skillManager.FireEvent(SkillMessageTopic.ChannelFinished);
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
                    skillManager.FireEvent(SkillMessageTopic.CooldownFinished);
                }
            }
        }

        private static SkillEventData CreateEventData(SkillMessageTopic topic, SkillContext context)
        {
            SkillEventData eventData = new SkillEventData();
            eventData.Topic = topic;
            eventData.SkillId = context != null ? context.SkillId : 0;
            eventData.Caster = context != null ? context.Caster : null;
            eventData.Target = context != null ? context.TargetUnit : null;
            eventData.Position = context != null ? context.TargetPosition : Vector3.zero;
            return eventData;
        }
    }
}
