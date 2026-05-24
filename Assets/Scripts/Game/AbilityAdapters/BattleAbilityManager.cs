using System;
using System.Collections.Generic;
using Game.Framework;
using Ability;
using UnityEngine;
using RuntimeAbility = Ability.Ability;

namespace Game
{
    public sealed class BattleAbilityManager : Singleton<BattleAbilityManager>
    {
        private const float GlobalSearchRadius = 9999f;

        private readonly Dictionary<int, TdUnit> npcUnits = new Dictionary<int, TdUnit>();
        private readonly Dictionary<int, TdUnit> towerUnits = new Dictionary<int, TdUnit>();
        private readonly List<IUnit> searchResults = new List<IUnit>();

        private TdUnit baseUnit;
        private bool initialized;

        public AbilitySystem Engine { get; } = new AbilitySystem();

        public bool Initialize()
        {
            npcUnits.Clear();
            towerUnits.Clear();
            baseUnit = null;

            Engine.Initialize(new TdWorld(this), new TdPresentation());
            RegisterDefinitions();

            initialized = true;
            Debug.Log("BattleAbilityManager initialized.");
            return true;
        }

        public void Release()
        {
            Engine.ClearRuntime();
            npcUnits.Clear();
            towerUnits.Clear();
            baseUnit = null;
            initialized = false;
        }

        public void Update(float deltaTime)
        {
            if (!initialized)
            {
                return;
            }

            Engine.Update(deltaTime);
        }

        public TdUnit GetUnit(Npc npc)
        {
            if (npc == null)
            {
                return null;
            }

            int entityId = npc.GetInstanceID();
            if (!npcUnits.TryGetValue(entityId, out TdUnit unit))
            {
                unit = new TdUnit(npc);
                npcUnits.Add(entityId, unit);
            }

            return unit;
        }

        public TdUnit GetUnit(Tower tower)
        {
            if (tower == null)
            {
                return null;
            }

            int entityId = tower.GetInstanceID();
            if (!towerUnits.TryGetValue(entityId, out TdUnit unit))
            {
                unit = new TdUnit(tower);
                towerUnits.Add(entityId, unit);
            }

            return unit;
        }

        public TdUnit GetBaseUnit()
        {
            if (baseUnit == null)
            {
                baseUnit = TdUnit.CreateBaseUnit();
            }

            return baseUnit;
        }

        public RuntimeAbility AddAbilityToNpc(Npc npc, int skillId, int level = 1)
        {
            return AddAbility(GetUnit(npc), skillId, level);
        }

        public RuntimeAbility AddAbilityToTower(Tower tower, int skillId, int level = 1)
        {
            return AddAbility(GetUnit(tower), skillId, level);
        }

        public RuntimeAbility AddAbilityToBase(int skillId, int level = 1)
        {
            return AddAbility(GetBaseUnit(), skillId, level);
        }

        public RuntimeAbility AddAbility(IUnit owner, int skillId, int level = 1)
        {
            if (!initialized || owner == null)
            {
                return null;
            }

            if (!DataManager.Instance.Skill.TryGet(skillId, out SkillConfig config) || config == null || !config.Enable)
            {
                Debug.LogWarning($"Ability add ability failed. Missing skill config: {skillId}");
                return null;
            }

            IResourceOwner resourceOwner = config.CostResourceId > 0 ? new TdResourceOwner(config.CostResourceId) : null;
            return Engine.AddAbility(owner, AbilityConfigConverter.AbilityName(skillId), level, resourceOwner);
        }

        public CastResult CastAbility(IUnit caster, int skillId)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId);
            if (ability == null)
            {
                return CastResult.Fail(CastFailureReason.MissingAbility);
            }

            return Engine.CastAbility(caster, ability.Definition.Name);
        }

        public CastResult CastAbilityOnTarget(IUnit caster, int skillId, IUnit target)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId);
            if (ability == null)
            {
                return CastResult.Fail(CastFailureReason.MissingAbility);
            }

            return Engine.CastAbilityOnTarget(caster, ability.Definition.Name, target);
        }

        public CastResult CastAbilityOnPosition(IUnit caster, int skillId, Vector3 position)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId);
            if (ability == null)
            {
                return CastResult.Fail(CastFailureReason.MissingAbility);
            }

            return Engine.CastAbilityOnPosition(caster, ability.Definition.Name, position);
        }

        public CastResult CastNpcAbilityAtBestTarget(Npc npc, int skillId)
        {
            return CastAbilityAtBestTarget(GetUnit(npc), skillId);
        }

        public CastResult CastTowerAbilityAtBestTarget(Tower tower, int skillId)
        {
            return CastAbilityAtBestTarget(GetUnit(tower), skillId);
        }

        public CastResult CastBaseAbilityAtBestTarget(int skillId)
        {
            return CastAbilityAtBestTarget(GetBaseUnit(), skillId);
        }

        public CastResult CastAbilityAtBestTarget(IUnit caster, int skillId)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId);
            if (ability == null)
            {
                return CastResult.Fail(CastFailureReason.MissingAbility);
            }

            AbilityDefinition definition = ability.Definition;
            AbilityBehavior behavior = definition.Behavior;

            if ((behavior & AbilityBehavior.Passive) != 0)
            {
                return CastResult.Fail(CastFailureReason.AbilityPassive);
            }

            if ((behavior & AbilityBehavior.UnitTarget) != 0)
            {
                IUnit target = FindNearestTarget(caster, definition);
                if (target == null)
                {
                    return CastResult.Fail(CastFailureReason.InvalidTarget, "No valid target.");
                }

                return Engine.CastAbilityOnTarget(caster, definition.Name, target);
            }

            if ((behavior & AbilityBehavior.PointTarget) != 0)
            {
                IUnit target = FindNearestTarget(caster, definition);
                Vector3 position = target != null ? target.Position : caster.Position;
                return Engine.CastAbilityOnPosition(caster, definition.Name, position);
            }

            return Engine.CastAbility(caster, definition.Name);
        }

        public bool HasState(Npc npc, UnitState state)
        {
            return Engine.HasState(GetUnit(npc), state);
        }

        public bool HasState(Tower tower, UnitState state)
        {
            return Engine.HasState(GetUnit(tower), state);
        }

        public bool IsStunned(Npc npc)
        {
            return HasState(npc, UnitState.Stunned);
        }

        public bool IsStunned(Tower tower)
        {
            return HasState(tower, UnitState.Stunned);
        }

        public bool IsRooted(Npc npc)
        {
            return HasState(npc, UnitState.Rooted);
        }

        public bool IsCommandRestricted(Npc npc)
        {
            return HasState(npc, UnitState.CommandRestricted);
        }

        public bool IsActionRestricted(Tower tower)
        {
            TdUnit unit = GetUnit(tower);
            return Engine.HasState(unit, UnitState.Stunned) ||
                   Engine.HasState(unit, UnitState.CommandRestricted);
        }

        public bool IsMovementRestricted(Npc npc)
        {
            TdUnit unit = GetUnit(npc);
            return Engine.HasState(unit, UnitState.Stunned) ||
                   Engine.HasState(unit, UnitState.Rooted) ||
                   Engine.HasState(unit, UnitState.CommandRestricted);
        }

        public DamageResult ApplyTowerAttackDamage(Tower tower, Npc target, float damage)
        {
            return Engine.ApplyDamage(new DamageInfo
            {
                Engine = Engine,
                Attacker = GetUnit(tower),
                Victim = GetUnit(target),
                Ability = null,
                Amount = damage,
                DamageType = DamageType.Physical,
                Flags = DamageFlags.None
            });
        }

        public DamageResult ApplyNpcAttackDamageToBase(Npc npc, float damage)
        {
            return Engine.ApplyDamage(new DamageInfo
            {
                Engine = Engine,
                Attacker = GetUnit(npc),
                Victim = GetBaseUnit(),
                Ability = null,
                Amount = damage,
                DamageType = DamageType.Physical,
                Flags = DamageFlags.None
            });
        }

        public float GetMoveSpeedMultiplier(Npc npc)
        {
            TdUnit unit = GetUnit(npc);
            float bonus = Engine.GetModifierProperty(unit, ModifierProperty.MoveSpeedBonusPercent, null);
            return Mathf.Max(0f, 1f + bonus / 100f);
        }

        public float GetAttackIntervalMultiplier(Tower tower)
        {
            TdUnit unit = GetUnit(tower);
            float attackSpeedBonus = Engine.GetModifierProperty(unit, ModifierProperty.AttackSpeedBonus, null);
            return 1f / Mathf.Max(0.01f, 1f + attackSpeedBonus / 100f);
        }

        public void RegisterAbilityScript(int skillId, Func<AbilityScript> factory)
        {
            Engine.RegisterAbilityScript(AbilityConfigConverter.AbilityName(skillId), factory);
        }

        public void RegisterModifierScript(int modifierId, Func<ModifierScript> factory)
        {
            Engine.RegisterModifierScript(AbilityConfigConverter.ModifierName(modifierId), factory);
        }

        public bool TryGetAbilityDefinition(int skillId, out AbilityDefinition definition)
        {
            return Engine.TryGetAbilityDefinition(AbilityConfigConverter.AbilityName(skillId), out definition);
        }

        public bool TryGetModifierDefinition(int modifierId, out ModifierDefinition definition)
        {
            return Engine.TryGetModifierDefinition(AbilityConfigConverter.ModifierName(modifierId), out definition);
        }

        public Modifier AddModifierToNpc(Npc npc, int modifierId, float duration = float.NaN)
        {
            TdUnit unit = GetUnit(npc);
            return Engine.AddModifier(unit, unit, AbilityConfigConverter.ModifierName(modifierId), duration);
        }

        public Modifier AddModifierToTower(Tower tower, int modifierId, float duration = float.NaN)
        {
            TdUnit unit = GetUnit(tower);
            return Engine.AddModifier(unit, unit, AbilityConfigConverter.ModifierName(modifierId), duration);
        }

        public Modifier AddModifierToBase(int modifierId, float duration = float.NaN)
        {
            TdUnit unit = GetBaseUnit();
            return Engine.AddModifier(unit, unit, AbilityConfigConverter.ModifierName(modifierId), duration);
        }

        private RuntimeAbility EnsureAbility(IUnit caster, int skillId)
        {
            if (!initialized || caster == null)
            {
                return null;
            }

            string abilityName = AbilityConfigConverter.AbilityName(skillId);
            RuntimeAbility ability = Engine.FindAbility(caster, abilityName);
            if (ability != null)
            {
                return ability;
            }

            return AddAbility(caster, skillId);
        }

        private IUnit FindNearestTarget(IUnit caster, AbilityDefinition definition)
        {
            if (caster == null || definition == null)
            {
                return null;
            }

            TargetQuery query = new TargetQuery
            {
                Caster = caster,
                Team = definition.TargetTeam,
                Types = definition.TargetType,
                Flags = definition.TargetFlags
            };

            float radius = definition.CastRange != null ? definition.CastRange.GetValue(1) : 0f;
            if (radius <= 0f)
            {
                radius = GlobalSearchRadius;
            }

            searchResults.Clear();
            Engine.FindUnits(caster.Position, radius, query, searchResults);

            IUnit nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < searchResults.Count; i++)
            {
                IUnit unit = searchResults[i];
                bool excludeSelf = (definition.TargetFlags & TargetFlags.ExcludeSelf) != 0 ||
                                   definition.TargetTeam == TargetTeam.Enemy;
                if (unit == null || (excludeSelf && unit.EntityId == caster.EntityId))
                {
                    continue;
                }

                float sqrDistance = (unit.Position - caster.Position).sqrMagnitude;
                if (sqrDistance < nearestDistance)
                {
                    nearest = unit;
                    nearestDistance = sqrDistance;
                }
            }

            searchResults.Clear();
            return nearest;
        }

        private void RegisterDefinitions()
        {
            Dictionary<int, List<SkillActionConfig>> actionGroups = AbilityConfigConverter.BuildActionGroups(DataManager.Instance.SkillAction);

            if (DataManager.Instance.SkillModifier != null && DataManager.Instance.SkillModifier.GetAll() != null)
            {
                foreach (KeyValuePair<int, SkillModifierConfig> pair in DataManager.Instance.SkillModifier.GetAll())
                {
                    ModifierDefinition definition = AbilityConfigConverter.ToModifierDefinition(pair.Value, actionGroups);
                    Engine.RegisterModifierDefinition(definition);
                }
            }

            if (DataManager.Instance.Skill != null && DataManager.Instance.Skill.GetAll() != null)
            {
                foreach (KeyValuePair<int, SkillConfig> pair in DataManager.Instance.Skill.GetAll())
                {
                    SkillConfig config = pair.Value;
                    if (config == null || !config.Enable)
                    {
                        continue;
                    }

                    AbilityDefinition definition = AbilityConfigConverter.ToAbilityDefinition(config, actionGroups);
                    Engine.RegisterAbilityDefinition(definition);
                }
            }
        }
    }
}
