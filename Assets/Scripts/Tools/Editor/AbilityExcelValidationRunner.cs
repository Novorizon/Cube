#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Game.Ability;
using Game.Ability.Configuration;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class AbilityExcelValidationRunner
{
    private static readonly string[] UnconnectedAbilityTables =
    {
        "AbilityConfig.xlsx",
        "AbilityAction.xlsx",
        "AbilityModifier.xlsx",
        "AbilityModifierProperty.xlsx",
        "AbilityProjectile.xlsx",
        "AbilitySpecialValue.xlsx"
    };

    [MenuItem("Luban/Validate Ability Excel")]
    public static void ValidateFromMenu()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string excelDir = Path.Combine(projectRoot, "Data", "Excel");
        AbilityValidationReport report = Validate(excelDir, projectRoot);
        LogReport(report);
    }

    public static AbilityValidationReport Validate(string excelDir, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(excelDir) || !Directory.Exists(excelDir))
        {
            AbilityValidationReport missing = new AbilityValidationReport();
            missing.Add(AbilityValidationSeverity.Error, "ABILITYEXCEL001", "Ability Excel directory does not exist: " + excelDir + ".");
            return missing;
        }

        AbilityConfigCatalog catalog = LoadCatalog(excelDir);

        AbilityValidationReport report = AbilityConfigurationValidator.Validate(
            catalog,
            new AbilityValidationOptions
            {
                AssetExists = asset => AssetExists(projectRoot, asset)
            });

        for (int i = 0; i < UnconnectedAbilityTables.Length; i++)
        {
            string fileName = UnconnectedAbilityTables[i];
            string path = Path.Combine(excelDir, fileName);
            if (File.Exists(path) && CountDataRows(path) > 0)
            {
                report.Add(
                    AbilityValidationSeverity.Error,
                    "ABILITYEXCEL002",
                    fileName + " contains data, but the current runtime provider only consumes skill*.xlsx. Move active data to the connected source or implement its provider before generation.",
                    Source(fileName, 0));
            }
        }

        return report;
    }

    public static AbilityConfigCatalog LoadCatalog(string excelDir)
    {
        if (string.IsNullOrWhiteSpace(excelDir) || !Directory.Exists(excelDir))
        {
            throw new DirectoryNotFoundException("Ability Excel directory does not exist: " + excelDir + ".");
        }

        AbilityConfigCatalog catalog = new AbilityConfigCatalog();
        LoadResources(catalog, excelDir);
        LoadLegacyActions(catalog, excelDir);
        LoadLegacyModifiers(catalog, excelDir);
        LoadLegacyAbilities(catalog, excelDir);
        LoadTowerBindings(catalog, excelDir);
        return catalog;
    }

    public static void LogReport(AbilityValidationReport report)
    {
        if (report == null)
        {
            Debug.LogError("[Ability配置校验] 校验器未返回报告。");
            return;
        }

        for (int i = 0; i < report.Issues.Count; i++)
        {
            AbilityValidationIssue issue = report.Issues[i];
            string location = issue.Source != null ? " | " + issue.Source : string.Empty;
            string chain = string.IsNullOrEmpty(issue.ReferenceChain) ? string.Empty : " | " + issue.ReferenceChain;
            string message = "[Ability配置校验][" + issue.Code + "] " + issue.Message + location + chain;
            switch (issue.Severity)
            {
                case AbilityValidationSeverity.Error:
                    Debug.LogError(message);
                    break;
                case AbilityValidationSeverity.Warning:
                    Debug.LogWarning(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }

        string summary = "[Ability配置校验] " + report.ErrorCount + " 个错误，" + report.WarningCount + " 个警告，" + report.InfoCount + " 条信息。";
        if (report.ErrorCount > 0)
        {
            Debug.LogError(summary + " Luban 生成将被阻止。");
        }
        else if (report.WarningCount > 0)
        {
            Debug.LogWarning(summary);
        }
        else
        {
            Debug.Log(summary);
        }
    }

    private static void LoadLegacyAbilities(AbilityConfigCatalog catalog, string excelDir)
    {
        const string fileName = "skill.xlsx";
        LubanTool.XlsxSheetData sheet = Read(excelDir, fileName);
        for (int row = 4; row <= sheet.GetMaxRow(); row++)
        {
            int id = Int(sheet, row, "id");
            if (id == 0)
            {
                continue;
            }

            int behavior = Int(sheet, row, "behavior");
            int targetTeam = Int(sheet, row, "targetTeam");
            int intrinsicModifierId = Int(sheet, row, "intrinsicModifierId");
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = AbilityConfigName(id),
                DisplayName = Text(sheet, row, "name"),
                Description = Text(sheet, row, "description"),
                Icon = Text(sheet, row, "iconLocation"),
                Behavior = MapLegacyBehavior(behavior),
                TargetTeam = MapLegacyTargetTeam(targetTeam),
                TargetType = UnitType.All,
                CastRange = LevelValue.Constant(Float(sheet, row, "castRange")),
                AoeRadius = LevelValue.Constant(Float(sheet, row, "aoeRadius")),
                CastPoint = LevelValue.Constant(Float(sheet, row, "castPoint")),
                ChannelTime = LevelValue.Constant(Float(sheet, row, "channelTime")),
                Cooldown = LevelValue.Constant(Float(sheet, row, "cooldown")),
                ManaCost = LevelValue.Constant(Float(sheet, row, "costCount")),
                IntrinsicModifierName = intrinsicModifierId > 0 ? ModifierConfigName(intrinsicModifierId) : null
            };

            catalog.Abilities.Add(new AbilityConfigRecord
            {
                Id = id,
                Enabled = Bool(sheet, row, "enable", true),
                Definition = definition,
                ActionGroupId = Int(sheet, row, "abilityActionGroupId"),
                IntrinsicModifierId = intrinsicModifierId,
                CostResourceId = Int(sheet, row, "costResourceId"),
                RawBehavior = behavior,
                RawTargetTeam = targetTeam,
                Source = Source(fileName, row)
            });
        }
    }

    private static void LoadLegacyActions(AbilityConfigCatalog catalog, string excelDir)
    {
        const string fileName = "skill_action.xlsx";
        LubanTool.XlsxSheetData sheet = Read(excelDir, fileName);
        for (int row = 4; row <= sheet.GetMaxRow(); row++)
        {
            int id = Int(sheet, row, "id");
            if (id == 0)
            {
                continue;
            }

            int rawActionType = Int(sheet, row, "actionType");
            int rawTargetType = Int(sheet, row, "targetType");
            int modifierId = Int(sheet, row, "modifierId");
            string effect = Text(sheet, row, "effectLocation");
            string sound = Text(sheet, row, "soundLocation");
            ActionType actionType = MapLegacyActionType(rawActionType);
            if (actionType == ActionType.None)
            {
                if (!string.IsNullOrEmpty(effect)) actionType = ActionType.PlayEffect;
                else if (!string.IsNullOrEmpty(sound)) actionType = ActionType.PlaySound;
            }

            ActionDefinition definition = new ActionDefinition
            {
                ActionType = actionType,
                Target = MapLegacyActionTarget(rawTargetType),
                Value = LevelValue.Constant(Float(sheet, row, "value")),
                Duration = LevelValue.Constant(FloatOrNaN(sheet, row, "duration")),
                ModifierName = modifierId > 0 ? ModifierConfigName(modifierId) : null,
                DamageType = MapLegacyDamageType(Int(sheet, row, "damageType")),
                EffectName = effect,
                SoundName = sound
            };

            catalog.Actions.Add(new AbilityActionConfigRecord
            {
                Id = id,
                GroupId = Int(sheet, row, "groupId"),
                Order = Int(sheet, row, "order"),
                Definition = definition,
                ModifierId = modifierId,
                RawActionType = rawActionType,
                RawTargetType = rawTargetType,
                Source = Source(fileName, row)
            });
        }
    }

    private static void LoadLegacyModifiers(AbilityConfigCatalog catalog, string excelDir)
    {
        const string fileName = "skill_modifier.xlsx";
        LubanTool.XlsxSheetData sheet = Read(excelDir, fileName);
        for (int row = 4; row <= sheet.GetMaxRow(); row++)
        {
            int id = Int(sheet, row, "id");
            if (id == 0)
            {
                continue;
            }

            int propertyType = Int(sheet, row, "propertyType");
            int state = Int(sheet, row, "state");
            int triggerEvent = Int(sheet, row, "triggerEventType");
            ModifierDefinition definition = new ModifierDefinition
            {
                Name = ModifierConfigName(id),
                DisplayName = Text(sheet, row, "name"),
                IsDebuff = Bool(sheet, row, "isDebuff"),
                IsHidden = Bool(sheet, row, "isHidden"),
                IsPurgable = Bool(sheet, row, "isPurgable", true),
                RemoveOnDeath = Bool(sheet, row, "removeOnDeath", true),
                Duration = Float(sheet, row, "duration"),
                Interval = Float(sheet, row, "interval"),
                MaxStack = Math.Max(1, Int(sheet, row, "maxStack")),
                States = MapLegacyState(state),
                TriggerEventType = MapLegacyTriggerEvent(triggerEvent)
            };
            ModifierProperty property = MapLegacyProperty(propertyType);
            if (property != ModifierProperty.None)
            {
                definition.Properties[property] = Float(sheet, row, "propertyValue");
            }

            catalog.Modifiers.Add(new AbilityModifierConfigRecord
            {
                Id = id,
                Definition = definition,
                TriggerActionGroupId = Int(sheet, row, "triggerActionGroupId"),
                PeriodicActionGroupId = Int(sheet, row, "periodicActionGroupId"),
                OnCreatedActionGroupId = Int(sheet, row, "onCreatedActionGroupId"),
                OnDestroyActionGroupId = Int(sheet, row, "onDestroyActionGroupId"),
                RawPropertyType = propertyType,
                RawState = state,
                RawTriggerEventType = triggerEvent,
                EffectName = Text(sheet, row, "effectLocation"),
                Source = Source(fileName, row)
            });
        }
    }

    private static void LoadResources(AbilityConfigCatalog catalog, string excelDir)
    {
        // Legacy skill.costResourceId is consumed by BattleItemManager/TdResourceOwner, so item
        // IDs are the active resource domain. World resource IDs are also accepted for future
        // adapters that expose them through IResourceOwner.
        LoadIds(catalog.ResourceIds, Read(excelDir, "item.xlsx"));
        LoadIds(catalog.ResourceIds, Read(excelDir, "resource.xlsx"));
    }

    private static void LoadIds(ISet<int> target, LubanTool.XlsxSheetData sheet)
    {
        for (int row = 4; row <= sheet.GetMaxRow(); row++)
        {
            int id = Int(sheet, row, "id");
            if (id > 0)
            {
                target.Add(id);
            }
        }
    }

    private static void LoadTowerBindings(AbilityConfigCatalog catalog, string excelDir)
    {
        const string fileName = "tower_level.xlsx";
        LubanTool.XlsxSheetData sheet = Read(excelDir, fileName);
        for (int row = 4; row <= sheet.GetMaxRow(); row++)
        {
            int skillId = Int(sheet, row, "skillId");
            if (skillId <= 0 || !Bool(sheet, row, "enable", true))
            {
                continue;
            }

            catalog.UsedAbilityIds.Add(skillId);
            catalog.TowerBindings.Add(new TowerAbilityBindingRecord
            {
                TowerId = Int(sheet, row, "towerId"),
                Level = Int(sheet, row, "level"),
                AbilityId = skillId,
                AttackInterval = Float(sheet, row, "attackInterval"),
                Source = Source(fileName, row)
            });
        }
    }

    private static LubanTool.XlsxSheetData Read(string excelDir, string fileName)
    {
        string path = Path.Combine(excelDir, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Required ability validation source was not found.", path);
        }

        try
        {
            return LubanTool.XlsxReader.ReadFirstSheet(path);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("Failed to read ability validation source: " + path + ".", exception);
        }
    }

    private static int CountDataRows(string path)
    {
        LubanTool.XlsxSheetData sheet;
        try
        {
            sheet = LubanTool.XlsxReader.ReadFirstSheet(path);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException("Failed to inspect ability table: " + path + ".", exception);
        }
        int count = 0;
        for (int row = 4; row <= sheet.GetMaxRow(); row++)
        {
            if (Int(sheet, row, "id") != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static Dictionary<string, int> Header(LubanTool.XlsxSheetData sheet)
    {
        Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int maxColumn = sheet.GetMaxColumn(1);
        for (int column = 1; column <= maxColumn; column++)
        {
            string name = sheet.GetCell(1, column)?.Trim();
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("#", StringComparison.Ordinal) && !result.ContainsKey(name))
            {
                result.Add(name, column);
            }
        }
        return result;
    }

    private static string Text(LubanTool.XlsxSheetData sheet, int row, string field)
    {
        Dictionary<string, int> header = Header(sheet);
        return header.TryGetValue(field, out int column) ? sheet.GetCell(row, column)?.Trim() ?? string.Empty : string.Empty;
    }

    private static int Int(LubanTool.XlsxSheetData sheet, int row, string field)
    {
        return int.TryParse(Text(sheet, row, field), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
    }

    private static float Float(LubanTool.XlsxSheetData sheet, int row, string field)
    {
        return float.TryParse(Text(sheet, row, field), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
    }

    private static float FloatOrNaN(LubanTool.XlsxSheetData sheet, int row, string field)
    {
        string text = Text(sheet, row, field);
        return string.IsNullOrWhiteSpace(text) ? float.NaN : Float(sheet, row, field);
    }

    private static bool Bool(LubanTool.XlsxSheetData sheet, int row, string field, bool defaultValue = false)
    {
        string text = Text(sheet, row, field);
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        if (bool.TryParse(text, out bool value)) return value;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) return number != 0;
        return defaultValue;
    }

    private static AbilityConfigSource Source(string fileName, int row)
    {
        return new AbilityConfigSource
        {
            SourceType = "Excel",
            Path = "Data/Excel/" + fileName,
            Sheet = "Sheet1",
            Row = row
        };
    }

    private static bool AssetExists(string projectRoot, string asset)
    {
        string normalized = asset.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return File.Exists(Path.Combine(projectRoot, normalized));
    }

    private static string AbilityConfigName(int id) => "ability_" + id;
    private static string ModifierConfigName(int id) => "modifier_" + id;

    private static AbilityBehavior MapLegacyBehavior(int value)
    {
        AbilityBehavior result = AbilityBehavior.None;
        if ((value & 1) != 0) result |= AbilityBehavior.NoTarget;
        if ((value & 2) != 0) result |= AbilityBehavior.UnitTarget;
        if ((value & 4) != 0) result |= AbilityBehavior.PointTarget;
        if ((value & 8) != 0) result |= AbilityBehavior.Passive;
        if ((value & 16) != 0) result |= AbilityBehavior.Toggle;
        if ((value & 32) != 0) result |= AbilityBehavior.Channelled;
        if ((value & 64) != 0) result |= AbilityBehavior.Aoe;
        return result;
    }

    private static TargetTeam MapLegacyTargetTeam(int value)
    {
        if (value == 1) return TargetTeam.Friendly;
        if (value == 2) return TargetTeam.Enemy;
        if (value == 3) return TargetTeam.Both;
        return TargetTeam.None;
    }

    private static ActionType MapLegacyActionType(int value)
    {
        if (value == 1) return ActionType.Damage;
        if (value == 2) return ActionType.Heal;
        if (value == 3) return ActionType.AddModifier;
        return ActionType.None;
    }

    private static ActionTarget MapLegacyActionTarget(int value)
    {
        if (value == 1) return ActionTarget.Caster;
        if (value == 2) return ActionTarget.PrimaryTarget;
        if (value == 3) return ActionTarget.Point;
        return ActionTarget.ContextTargets;
    }

    private static DamageType MapLegacyDamageType(int value)
    {
        if (value == 1) return DamageType.Physical;
        if (value == 2) return DamageType.Magical;
        if (value == 3) return DamageType.Pure;
        return DamageType.None;
    }

    private static ModifierProperty MapLegacyProperty(int value)
    {
        if (value == 1) return ModifierProperty.MoveSpeedBonusPercent;
        if (value == 2) return ModifierProperty.AttackSpeedBonus;
        if (value == 3) return ModifierProperty.DamageOutgoingPercent;
        if (value == 4) return ModifierProperty.ArmorBonus;
        if (value == 5) return ModifierProperty.DamageIncomingPercent;
        if (value == 6) return ModifierProperty.DamageOutgoingPercent;
        return ModifierProperty.None;
    }

    private static UnitState MapLegacyState(int value)
    {
        if (value == 1) return UnitState.Stunned;
        if (value == 2) return UnitState.Silenced;
        if (value == 3) return UnitState.Rooted;
        if (value == 4) return UnitState.Invulnerable;
        return UnitState.None;
    }

    private static ModifierEventType MapLegacyTriggerEvent(int value)
    {
        if (value == 1) return ModifierEventType.AbilityExecuted;
        if (value == 2) return ModifierEventType.DamageDealt;
        if (value == 3) return ModifierEventType.DamageTaken;
        if (value == 4) return ModifierEventType.Healed;
        if (value == 5) return ModifierEventType.AttackLanded;
        if (value == 6) return ModifierEventType.Death;
        return ModifierEventType.None;
    }
}

#endif
