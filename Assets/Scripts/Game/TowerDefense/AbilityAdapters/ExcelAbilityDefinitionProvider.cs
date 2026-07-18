using System.Collections.Generic;
using Game.Ability;
using Game.Ability.Configuration;

namespace Game
{
    /// <summary>
    /// Adapts the current Luban Skill tables into source-neutral runtime definitions. This is the
    /// only Excel-specific provider; Ability core and the registry do not know generated Game data.
    /// </summary>
    public sealed class ExcelAbilityDefinitionProvider : IAbilityDefinitionProvider
    {
        private readonly ConfigTableReader<SkillConfig> skills;
        private readonly ConfigTableReader<SkillActionConfig> actions;
        private readonly ConfigTableReader<SkillModifierConfig> modifiers;

        public ExcelAbilityDefinitionProvider(
            ConfigTableReader<SkillConfig> skills,
            ConfigTableReader<SkillActionConfig> actions,
            ConfigTableReader<SkillModifierConfig> modifiers)
        {
            this.skills = skills;
            this.actions = actions;
            this.modifiers = modifiers;
        }

        public string ProviderName => "Legacy Skill Excel/Luban";
        public AbilityDefinitionSourceType SourceType => AbilityDefinitionSourceType.Excel;

        public AbilityDefinitionBundle Load()
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            Dictionary<int, List<SkillActionConfig>> actionGroups = AbilityConfigConverter.BuildActionGroups(actions);

            IReadOnlyDictionary<int, SkillModifierConfig> modifierData = modifiers?.GetAll();
            if (modifierData != null)
            {
                foreach (KeyValuePair<int, SkillModifierConfig> pair in modifierData)
                {
                    SkillModifierConfig config = pair.Value;
                    if (config == null)
                    {
                        continue;
                    }

                    ValidateModifierGroups(config, actionGroups, bundle.Validation);
                    bundle.Modifiers.Add(new ModifierDefinitionRegistration
                    {
                        Definition = AbilityConfigConverter.ToModifierDefinition(config, actionGroups),
                        Origin = Origin("Data/Excel/skill_modifier.xlsx", config.Id)
                    });
                }
            }

            IReadOnlyDictionary<int, SkillConfig> skillData = skills?.GetAll();
            if (skillData != null)
            {
                foreach (KeyValuePair<int, SkillConfig> pair in skillData)
                {
                    SkillConfig config = pair.Value;
                    if (config == null || !config.Enable)
                    {
                        continue;
                    }

                    if (config.AbilityActionGroupId > 0 && !actionGroups.ContainsKey(config.AbilityActionGroupId))
                    {
                        bundle.Validation.Add(
                            AbilityValidationSeverity.Error,
                            "ABILITYEXCELPROVIDER001",
                            "Skill " + config.Id + " references missing action group " + config.AbilityActionGroupId + ".",
                            Source("Data/Excel/skill.xlsx"),
                            "skill:" + config.Id + " -> actionGroup:" + config.AbilityActionGroupId);
                    }

                    bundle.Abilities.Add(new AbilityDefinitionRegistration
                    {
                        Definition = AbilityConfigConverter.ToAbilityDefinition(config, actionGroups),
                        Origin = Origin("Data/Excel/skill.xlsx", config.Id)
                    });
                }
            }

            return bundle;
        }

        private static void ValidateModifierGroups(
            SkillModifierConfig config,
            IReadOnlyDictionary<int, List<SkillActionConfig>> actionGroups,
            AbilityValidationReport report)
        {
            ValidateGroup(config.Id, "trigger", config.TriggerActionGroupId, actionGroups, report);
            ValidateGroup(config.Id, "periodic", config.PeriodicActionGroupId, actionGroups, report);
            ValidateGroup(config.Id, "on-created", config.OnCreatedActionGroupId, actionGroups, report);
            ValidateGroup(config.Id, "on-destroy", config.OnDestroyActionGroupId, actionGroups, report);
        }

        private static void ValidateGroup(
            int modifierId,
            string purpose,
            int groupId,
            IReadOnlyDictionary<int, List<SkillActionConfig>> actionGroups,
            AbilityValidationReport report)
        {
            if (groupId > 0 && !actionGroups.ContainsKey(groupId))
            {
                report.Add(
                    AbilityValidationSeverity.Error,
                    "ABILITYEXCELPROVIDER002",
                    "Modifier " + modifierId + " references missing " + purpose + " action group " + groupId + ".",
                    Source("Data/Excel/skill_modifier.xlsx"),
                    "modifier:" + modifierId + " -> actionGroup:" + groupId);
            }
        }

        private static AbilityDefinitionOrigin Origin(string path, int id)
        {
            return new AbilityDefinitionOrigin
            {
                SourceType = AbilityDefinitionSourceType.Excel,
                ProviderName = "Legacy Skill Excel/Luban",
                SourcePath = path,
                StableId = id.ToString()
            };
        }

        private static AbilityConfigSource Source(string path)
        {
            return new AbilityConfigSource
            {
                SourceType = AbilityDefinitionSourceType.Excel.ToString(),
                Path = path
            };
        }
    }
}
