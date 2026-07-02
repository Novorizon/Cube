using System.Collections.Generic;
using Game.Ability;

namespace Game
{
    /// <summary>
    /// Converts existing TD skill tables into Game.Ability definitions.
    /// Keep enum mapping explicit because table numbers are data contracts, not runtime enum values.
    /// </summary>
    public static class AbilityConfigConverter
    {
        public static string AbilityName(int skillId)
        {
            return $"skill_{skillId}";
        }

        public static string ModifierName(int modifierId)
        {
            return $"modifier_{modifierId}";
        }

        public static Dictionary<int, List<SkillActionConfig>> BuildActionGroups(ConfigTableReader<SkillActionConfig> reader)
        {
            Dictionary<int, List<SkillActionConfig>> groups = new Dictionary<int, List<SkillActionConfig>>();
            if (reader == null || reader.GetAll() == null)
            {
                return groups;
            }

            foreach (KeyValuePair<int, SkillActionConfig> pair in reader.GetAll())
            {
                SkillActionConfig config = pair.Value;
                if (config == null || config.GroupId <= 0)
                {
                    continue;
                }

                if (!groups.TryGetValue(config.GroupId, out List<SkillActionConfig> list))
                {
                    list = new List<SkillActionConfig>();
                    groups.Add(config.GroupId, list);
                }

                list.Add(config);
            }

            foreach (List<SkillActionConfig> list in groups.Values)
            {
                // Preserve designer-authored action order inside each group.
                list.Sort(CompareActionOrder);
            }

            return groups;
        }

        public static AbilityDefinition ToAbilityDefinition(SkillConfig config, IReadOnlyDictionary<int, List<SkillActionConfig>> actionGroups)
        {
            if (config == null)
            {
                return null;
            }

            // Config data is copied into engine-owned definition objects; no Game classes leak inward.
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = AbilityName(config.Id),
                DisplayName = LocalizedConfigText.SkillName(config.Id),
                Description = LocalizedConfigText.SkillDescription(config.Id),
                Icon = config.IconLocation,
                Behavior = MapBehavior(config.Behavior),
                TargetTeam = MapTargetTeam(config.TargetTeam),
                TargetType = UnitType.All,
                TargetFlags = TargetFlags.None,
                CastRange = LevelValue.Constant(config.CastRange),
                AoeRadius = LevelValue.Constant(config.AoeRadius),
                CastPoint = LevelValue.Constant(config.CastPoint),
                ChannelTime = LevelValue.Constant(config.ChannelTime),
                Cooldown = LevelValue.Constant(config.Cooldown),
                ManaCost = LevelValue.Constant(config.CostCount),
                IntrinsicModifierName = config.IntrinsicModifierId > 0 ? ModifierName(config.IntrinsicModifierId) : null
            };

            AddActions(definition.Actions, config.AbilityActionGroupId, actionGroups);
            return definition;
        }

        public static ModifierDefinition ToModifierDefinition(SkillModifierConfig config, IReadOnlyDictionary<int, List<SkillActionConfig>> actionGroups)
        {
            if (config == null)
            {
                return null;
            }

            ModifierDefinition definition = new ModifierDefinition
            {
                Name = ModifierName(config.Id),
                DisplayName = LocalizationManager.GetOrFallback($"modifier.{config.Id}.name", config.Name),
                IsHidden = config.IsHidden,
                IsDebuff = config.IsDebuff,
                IsPurgable = config.IsPurgable,
                RemoveOnDeath = config.RemoveOnDeath,
                Duration = config.Duration,
                Interval = config.Interval,
                MaxStack = config.MaxStack > 0 ? config.MaxStack : 1,
                States = MapState(config.State),
                TriggerEventType = MapTriggerEvent(config.TriggerEventType)
            };

            ModifierProperty property = MapProperty(config.PropertyType);
            if (property != ModifierProperty.None)
            {
                definition.Properties[property] = config.PropertyValue;
            }

            AddActions(definition.IntervalActions, config.PeriodicActionGroupId, actionGroups);
            AddActions(definition.OnCreatedActions, config.OnCreatedActionGroupId, actionGroups);
            AddActions(definition.OnDestroyActions, config.OnDestroyActionGroupId, actionGroups);
            AddActions(definition.TriggerActions, config.TriggerActionGroupId, actionGroups);

            if (!config.IsHidden && !string.IsNullOrEmpty(config.EffectLocation))
            {
                definition.OnCreatedActions.Add(new ActionDefinition
                {
                    ActionType = ActionType.PlayEffect,
                    Target = ActionTarget.PrimaryTarget,
                    EffectName = config.EffectLocation
                });
            }

            return definition;
        }

        private static void AddActions(IList<ActionDefinition> target, int actionGroupId, IReadOnlyDictionary<int, List<SkillActionConfig>> actionGroups)
        {
            if (target == null || actionGroupId <= 0 || actionGroups == null)
            {
                return;
            }

            if (!actionGroups.TryGetValue(actionGroupId, out List<SkillActionConfig> configs))
            {
                return;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                ActionDefinition action = ToActionDefinition(configs[i]);
                if (action != null)
                {
                    target.Add(action);
                }
            }
        }

        private static ActionDefinition ToActionDefinition(SkillActionConfig config)
        {
            if (config == null)
            {
                return null;
            }

            ActionDefinition action = new ActionDefinition
            {
                ActionType = MapActionType(config.ActionType),
                Target = MapActionTarget(config.TargetType),
                Value = LevelValue.Constant(config.Value),
                Duration = LevelValue.Constant(config.Duration > 0f || config.Duration < 0f ? config.Duration : float.NaN),
                ModifierName = config.ModifierId > 0 ? ModifierName(config.ModifierId) : null,
                DamageType = MapDamageType(config.DamageType),
                EffectName = config.EffectLocation,
                SoundName = config.SoundLocation
            };

            // Effect-only or sound-only rows are valid presentation actions.
            if (action.ActionType == ActionType.None)
            {
                if (!string.IsNullOrEmpty(config.EffectLocation))
                {
                    action.ActionType = ActionType.PlayEffect;
                }
                else if (!string.IsNullOrEmpty(config.SoundLocation))
                {
                    action.ActionType = ActionType.PlaySound;
                }
            }

            return action.ActionType != ActionType.None ? action : null;
        }

        private static AbilityBehavior MapBehavior(int value)
        {
            AbilityBehavior result = AbilityBehavior.None;

            if ((value & 1) != 0)
            {
                result |= AbilityBehavior.NoTarget;
            }

            if ((value & 2) != 0)
            {
                result |= AbilityBehavior.UnitTarget;
            }

            if ((value & 4) != 0)
            {
                result |= AbilityBehavior.PointTarget;
            }

            if ((value & 8) != 0)
            {
                result |= AbilityBehavior.Passive;
            }

            if ((value & 16) != 0)
            {
                result |= AbilityBehavior.Toggle;
            }

            if ((value & 32) != 0)
            {
                result |= AbilityBehavior.Channelled;
            }

            if ((value & 64) != 0)
            {
                result |= AbilityBehavior.Aoe;
            }

            return result != AbilityBehavior.None ? result : AbilityBehavior.NoTarget;
        }

        private static TargetTeam MapTargetTeam(int value)
        {
            switch (value)
            {
                case 1:
                    return TargetTeam.Friendly;

                case 2:
                    return TargetTeam.Enemy;

                case 3:
                    return TargetTeam.Both;

                default:
                    return TargetTeam.Enemy;
            }
        }

        private static ActionType MapActionType(int value)
        {
            switch (value)
            {
                case 1:
                    return ActionType.Damage;

                case 2:
                    return ActionType.Heal;

                case 3:
                    return ActionType.AddModifier;

                case 4:
                    return ActionType.None;

                default:
                    return ActionType.None;
            }
        }

        private static ActionTarget MapActionTarget(int value)
        {
            switch (value)
            {
                case 1:
                    return ActionTarget.Caster;

                case 2:
                    return ActionTarget.PrimaryTarget;

                case 3:
                    return ActionTarget.Point;

                case 4:
                case 5:
                    return ActionTarget.ContextTargets;

                default:
                    return ActionTarget.ContextTargets;
            }
        }

        private static DamageType MapDamageType(int value)
        {
            switch (value)
            {
                case 1:
                    return DamageType.Physical;

                case 2:
                    return DamageType.Magical;

                case 3:
                    return DamageType.Pure;

                default:
                    return DamageType.None;
            }
        }

        private static ModifierProperty MapProperty(int value)
        {
            switch (value)
            {
                case 1:
                    return ModifierProperty.MoveSpeedBonusPercent;

                case 2:
                    return ModifierProperty.AttackSpeedBonus;

                case 3:
                    return ModifierProperty.DamageOutgoingPercent;

                case 4:
                    return ModifierProperty.ArmorBonus;

                case 5:
                    return ModifierProperty.DamageIncomingPercent;

                case 6:
                    return ModifierProperty.DamageOutgoingPercent;

                default:
                    return ModifierProperty.None;
            }
        }

        private static UnitState MapState(int value)
        {
            switch (value)
            {
                case 1:
                    return UnitState.Stunned;

                case 2:
                    return UnitState.Silenced;

                case 3:
                    return UnitState.Rooted;

                case 4:
                    return UnitState.Invulnerable;

                default:
                    return UnitState.None;
            }
        }

        private static ModifierEventType MapTriggerEvent(int value)
        {
            switch (value)
            {
                case 1:
                    return ModifierEventType.AbilityExecuted;

                case 2:
                    return ModifierEventType.DamageDealt;

                case 3:
                    return ModifierEventType.DamageTaken;

                case 4:
                    return ModifierEventType.Healed;

                case 5:
                    return ModifierEventType.AttackLanded;

                case 6:
                    return ModifierEventType.Death;

                default:
                    return ModifierEventType.None;
            }
        }

        private static int CompareActionOrder(SkillActionConfig left, SkillActionConfig right)
        {
            int orderCompare = left.Order.CompareTo(right.Order);
            return orderCompare != 0 ? orderCompare : left.Id.CompareTo(right.Id);
        }
    }
}
