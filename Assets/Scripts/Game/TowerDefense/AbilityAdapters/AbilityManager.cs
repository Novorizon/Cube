using System;
using System.Collections.Generic;
using Game.Framework;
using Game.Ability;
using Game.Ability.Configuration;
using UnityEngine;
using RuntimeAbility = Game.Ability.Ability;

namespace Game
{
    /// <summary>
    /// Game-layer facade for the ability runtime.
    /// It adapts TD managers/config tables to Game.Ability and is the intended business entry point.
    /// </summary>
    public sealed class AbilityManager : Singleton<AbilityManager>
    {
        private const float GlobalSearchRadius = 9999f;

        // Keep stable adapters per live Game object so modifiers can compare units by EntityId.
        private readonly Dictionary<int, TdUnit> npcUnits = new Dictionary<int, TdUnit>();
        private readonly Dictionary<int, TdUnit> towerUnits = new Dictionary<int, TdUnit>();
        private readonly List<IUnit> searchResults = new List<IUnit>();
        private readonly HashSet<int> activeNpcObjectIds = new HashSet<int>();
        private readonly HashSet<int> activeTowerObjectIds = new HashSet<int>();
        private readonly List<int> staleObjectIds = new List<int>();
        private readonly List<IAbilityDefinitionProvider> additionalDefinitionProviders = new List<IAbilityDefinitionProvider>();

        private TdUnit baseUnit;
        private int nextRuntimeEntityId;
        private bool lifecycleEventsSubscribed;
        private bool initialized;

        public AbilitySystem Engine { get; } = new AbilitySystem();
        public AbilityDefinitionRegistry DefinitionRegistry { get; private set; }
        public bool IsInitialized => initialized;

        public void GetBindingDebugSnapshot(IList<AbilityBindingDebugInfo> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (KeyValuePair<int, TdUnit> pair in npcUnits)
            {
                TdUnit unit = pair.Value;
                results.Add(new AbilityBindingDebugInfo
                {
                    Kind = TdUnitKind.Npc,
                    BusinessObjectId = pair.Key,
                    RuntimeEntityId = unit != null ? unit.EntityId : 0,
                    DisplayName = unit?.Npc != null ? unit.Npc.name : "<released NPC>",
                    IsValid = unit != null && unit.IsValidBinding
                });
            }

            foreach (KeyValuePair<int, TdUnit> pair in towerUnits)
            {
                TdUnit unit = pair.Value;
                results.Add(new AbilityBindingDebugInfo
                {
                    Kind = TdUnitKind.Tower,
                    BusinessObjectId = pair.Key,
                    RuntimeEntityId = unit != null ? unit.EntityId : 0,
                    DisplayName = unit?.Tower != null ? unit.Tower.name : "<released Tower>",
                    IsValid = unit != null && unit.IsValidBinding
                });
            }

            if (baseUnit != null)
            {
                results.Add(new AbilityBindingDebugInfo
                {
                    Kind = TdUnitKind.Base,
                    BusinessObjectId = 0,
                    RuntimeEntityId = baseUnit.EntityId,
                    DisplayName = "Base",
                    IsValid = baseUnit.IsValidBinding
                });
            }
        }

        public bool Initialize()
        {
            UnsubscribeFromLifecycleEvents();
            npcUnits.Clear();
            towerUnits.Clear();
            activeNpcObjectIds.Clear();
            activeTowerObjectIds.Clear();
            staleObjectIds.Clear();
            baseUnit = null;
            nextRuntimeEntityId = 1;

            // Core runtime only sees interfaces; all project-specific lookup stays in adapters.
            Engine.Initialize(new TdWorld(this), new TdPresentation());
            if (!RegisterDefinitions())
            {
                initialized = false;
                Debug.LogError("AbilityManager initialization stopped because definition providers are invalid.");
                return false;
            }

            initialized = true;
            SubscribeToLifecycleEvents();
            SynchronizeUnitBindings();
            Debug.Log("AbilityManager initialized.");
            return true;
        }

        /// <summary>
        /// Registers an optional provider before Initialize. JSON sources stay outside generated
        /// Luban output and are merged through the same collision-safe definition registry.
        /// </summary>
        public bool AddDefinitionProvider(IAbilityDefinitionProvider provider)
        {
            if (provider == null || initialized)
            {
                return false;
            }

            additionalDefinitionProviders.Add(provider);
            return true;
        }

        public void Release()
        {
            UnsubscribeFromLifecycleEvents();
            ClearBattleRuntime();
            initialized = false;
        }

        public void Update(float deltaTime)
        {
            if (!initialized)
            {
                return;
            }

            SynchronizeUnitBindings();
            Engine.Update(deltaTime);
        }

        public TdUnit GetUnit(Npc npc)
        {
            if (npc == null)
            {
                return null;
            }

            int entityId = npc.GetInstanceID();
            if (npcUnits.TryGetValue(entityId, out TdUnit unit) && unit.Npc != npc)
            {
                UnregisterNpcObject(entityId);
                unit = null;
            }

            if (unit == null)
            {
                unit = new TdUnit(AllocateRuntimeEntityId(), npc);
                npcUnits[entityId] = unit;
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
            if (towerUnits.TryGetValue(entityId, out TdUnit unit) && unit.Tower != tower)
            {
                UnregisterTowerObject(entityId);
                unit = null;
            }

            if (unit == null)
            {
                unit = new TdUnit(AllocateRuntimeEntityId(), tower);
                towerUnits[entityId] = unit;
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

        public bool UnregisterUnit(Npc npc)
        {
            return npc != null && UnregisterNpcObject(npc.GetInstanceID());
        }

        public bool UnregisterUnit(Tower tower)
        {
            return tower != null && UnregisterTowerObject(tower.GetInstanceID());
        }

        public bool UnregisterBaseUnit()
        {
            if (baseUnit == null)
            {
                return false;
            }

            Engine.RemoveUnit(baseUnit);
            baseUnit = null;
            return true;
        }

        public RuntimeAbility AddAbilityToNpc(Npc npc, int skillId, int level = 1)
        {
            return AddAbility(GetUnit(npc), skillId, level);
        }

        public RuntimeAbility AddAbilityToTower(Tower tower, int skillId, int level = 0)
        {
            int resolvedLevel = level > 0 ? level : tower != null ? tower.Level : 1;
            return AddAbility(GetUnit(tower), skillId, resolvedLevel);
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

            // Current config still uses Skill ids. They are translated to ability names at the edge.
            IResourceOwner resourceOwner = config.CostResourceId > 0 ? new TdResourceOwner(config.CostResourceId) : null;
            return Engine.AddAbility(owner, AbilityConfigConverter.AbilityName(skillId), level, resourceOwner);
        }

        public CastResult CastAbility(IUnit caster, int skillId, int level = 1)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId, level);
            if (ability == null)
            {
                return CastResult.Fail(CastFailureReason.MissingAbility);
            }

            return Engine.CastAbility(caster, ability.Definition.Name);
        }

        public CastResult CastAbilityOnTarget(IUnit caster, int skillId, IUnit target, int level = 1)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId, level);
            if (ability == null)
            {
                return CastResult.Fail(CastFailureReason.MissingAbility);
            }

            return Engine.CastAbilityOnTarget(caster, ability.Definition.Name, target);
        }

        public CastResult CastAbilityOnPosition(IUnit caster, int skillId, Vector3 position, int level = 1)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId, level);
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
            return CastAbilityAtBestTarget(GetUnit(tower), skillId, tower != null ? tower.Level : 1);
        }

        public CastResult CastTowerAbilityOnTarget(Tower tower, int skillId, Npc target)
        {
            return CastAbilityOnTarget(GetUnit(tower), skillId, GetUnit(target), tower != null ? tower.Level : 1);
        }

        public CastResult CastBaseAbilityAtBestTarget(int skillId)
        {
            return CastAbilityAtBestTarget(GetBaseUnit(), skillId);
        }

        public bool TryGetBaseAbilityCooldown(int skillId, out float remaining, out float duration)
        {
            remaining = 0f;
            duration = 0f;

            RuntimeAbility ability = EnsureAbility(GetBaseUnit(), skillId);
            if (ability == null)
            {
                return false;
            }

            remaining = Mathf.Max(0f, ability.CooldownRemaining);
            duration = Mathf.Max(0f, ability.GetCooldown());
            return true;
        }

        public CastResult CastAbilityAtBestTarget(IUnit caster, int skillId, int level = 1)
        {
            RuntimeAbility ability = EnsureAbility(caster, skillId, level);
            if (ability == null)
            {
                return CastResult.Fail(CastFailureReason.MissingAbility);
            }

            // Convenience helper for existing AI/TD logic. Explicit casts should use Engine/target APIs.
            AbilityDefinition definition = ability.Definition;
            AbilityBehavior behavior = definition.Behavior;

            if ((behavior & AbilityBehavior.Passive) != 0)
            {
                return CastResult.Fail(CastFailureReason.AbilityPassive);
            }

            if ((behavior & AbilityBehavior.UnitTarget) != 0)
            {
                IUnit target = FindNearestTarget(caster, ability);
                if (target == null)
                {
                    return CastResult.Fail(CastFailureReason.InvalidTarget, "No valid target.");
                }

                return Engine.CastAbilityOnTarget(caster, definition.Name, target);
            }

            if ((behavior & AbilityBehavior.PointTarget) != 0)
            {
                IUnit target = FindNearestTarget(caster, ability);
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
            // Route attack damage through the same modifier-aware pipeline as ability damage.
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

        private RuntimeAbility EnsureAbility(IUnit caster, int skillId, int level = 1)
        {
            if (!initialized || caster == null)
            {
                return null;
            }

            // Existing business callers can cast by skill id without manually adding the ability first.
            string abilityName = AbilityConfigConverter.AbilityName(skillId);
            RuntimeAbility ability = Engine.FindAbility(caster, abilityName);
            if (ability != null)
            {
                ability.SetLevel(Mathf.Max(1, level));
                return ability;
            }

            return AddAbility(caster, skillId, Mathf.Max(1, level));
        }

        private int AllocateRuntimeEntityId()
        {
            return nextRuntimeEntityId++;
        }

        private void SubscribeToLifecycleEvents()
        {
            if (lifecycleEventsSubscribed)
            {
                return;
            }

            NpcManager.Instance.NpcRegistered += OnNpcRegistered;
            NpcManager.Instance.NpcUnregistered += OnNpcUnregistered;
            TowerManager.Instance.TowerRegistered += OnTowerRegistered;
            TowerManager.Instance.TowerUnregistered += OnTowerUnregistered;
            BaseManager.Instance.BaseLoaded += OnBaseLoaded;
            BaseManager.Instance.BaseRemoving += OnBaseRemoving;
            BattleFlowManager.Instance.BattleCompleted += OnBattleCompleted;
            lifecycleEventsSubscribed = true;
        }

        private void UnsubscribeFromLifecycleEvents()
        {
            if (!lifecycleEventsSubscribed)
            {
                return;
            }

            NpcManager.Instance.NpcRegistered -= OnNpcRegistered;
            NpcManager.Instance.NpcUnregistered -= OnNpcUnregistered;
            TowerManager.Instance.TowerRegistered -= OnTowerRegistered;
            TowerManager.Instance.TowerUnregistered -= OnTowerUnregistered;
            BaseManager.Instance.BaseLoaded -= OnBaseLoaded;
            BaseManager.Instance.BaseRemoving -= OnBaseRemoving;
            BattleFlowManager.Instance.BattleCompleted -= OnBattleCompleted;
            lifecycleEventsSubscribed = false;
        }

        private void OnNpcRegistered(Npc npc)
        {
            GetUnit(npc);
        }

        private void OnNpcUnregistered(Npc npc)
        {
            UnregisterUnit(npc);
        }

        private void OnTowerRegistered(Tower tower)
        {
            GetUnit(tower);
        }

        private void OnTowerUnregistered(Tower tower)
        {
            UnregisterUnit(tower);
        }

        private void OnBaseLoaded()
        {
            GetBaseUnit();
        }

        private void OnBaseRemoving()
        {
            UnregisterBaseUnit();
        }

        private void OnBattleCompleted(BattleEndedMessage message)
        {
            ClearBattleRuntime();
        }

        private void ClearBattleRuntime()
        {
            Engine.ClearRuntime();
            npcUnits.Clear();
            towerUnits.Clear();
            searchResults.Clear();
            activeNpcObjectIds.Clear();
            activeTowerObjectIds.Clear();
            staleObjectIds.Clear();
            baseUnit = null;
            nextRuntimeEntityId = 1;
        }

        private void SynchronizeUnitBindings()
        {
            SynchronizeNpcBindings();
            SynchronizeTowerBindings();

            if (BaseManager.Instance.HasBaseObject)
            {
                GetBaseUnit();
            }
            else if (baseUnit != null)
            {
                UnregisterBaseUnit();
            }
        }

        private void SynchronizeNpcBindings()
        {
            activeNpcObjectIds.Clear();
            IReadOnlyList<Npc> activeNpcs = NpcManager.Instance.ActiveNpcs;
            if (activeNpcs != null)
            {
                for (int i = 0; i < activeNpcs.Count; i++)
                {
                    Npc npc = activeNpcs[i];
                    if (npc == null)
                    {
                        continue;
                    }

                    int objectId = npc.GetInstanceID();
                    activeNpcObjectIds.Add(objectId);
                    GetUnit(npc);
                }
            }

            staleObjectIds.Clear();
            foreach (KeyValuePair<int, TdUnit> pair in npcUnits)
            {
                if (!activeNpcObjectIds.Contains(pair.Key) || pair.Value == null || !pair.Value.IsValidBinding)
                {
                    staleObjectIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleObjectIds.Count; i++)
            {
                UnregisterNpcObject(staleObjectIds[i]);
            }
        }

        private void SynchronizeTowerBindings()
        {
            activeTowerObjectIds.Clear();
            IReadOnlyList<Tower> activeTowers = TowerManager.Instance.ActiveTowers;
            if (activeTowers != null)
            {
                for (int i = 0; i < activeTowers.Count; i++)
                {
                    Tower tower = activeTowers[i];
                    if (tower == null)
                    {
                        continue;
                    }

                    int objectId = tower.GetInstanceID();
                    activeTowerObjectIds.Add(objectId);
                    GetUnit(tower);
                }
            }

            staleObjectIds.Clear();
            foreach (KeyValuePair<int, TdUnit> pair in towerUnits)
            {
                if (!activeTowerObjectIds.Contains(pair.Key) || pair.Value == null || !pair.Value.IsValidBinding)
                {
                    staleObjectIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < staleObjectIds.Count; i++)
            {
                UnregisterTowerObject(staleObjectIds[i]);
            }
        }

        private bool UnregisterNpcObject(int objectId)
        {
            if (!npcUnits.TryGetValue(objectId, out TdUnit unit))
            {
                return false;
            }

            Engine.RemoveUnit(unit);
            npcUnits.Remove(objectId);
            return true;
        }

        private bool UnregisterTowerObject(int objectId)
        {
            if (!towerUnits.TryGetValue(objectId, out TdUnit unit))
            {
                return false;
            }

            Engine.RemoveUnit(unit);
            towerUnits.Remove(objectId);
            return true;
        }

        private IUnit FindNearestTarget(IUnit caster, RuntimeAbility ability)
        {
            if (caster == null || ability == null)
            {
                return null;
            }

            AbilityDefinition definition = ability.Definition;

            TargetQuery query = new TargetQuery
            {
                Engine = Engine,
                Caster = caster,
                Team = definition.TargetTeam,
                Types = definition.TargetType,
                Flags = definition.TargetFlags
            };

            float radius = Mathf.Max(
                0f,
                ability.GetCastRange() + Engine.GetModifierProperty(caster, ModifierProperty.CastRangeBonus, null));
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

        private bool RegisterDefinitions()
        {
            List<IAbilityDefinitionProvider> providers = new List<IAbilityDefinitionProvider>
            {
                new ExcelAbilityDefinitionProvider(
                    DataManager.Instance.Skill,
                    DataManager.Instance.SkillAction,
                    DataManager.Instance.SkillModifier)
            };
            providers.AddRange(additionalDefinitionProviders);

            DefinitionRegistry = new AbilityDefinitionRegistry();
            DefinitionRegistry.LoadProviders(providers);
            for (int i = 0; i < DefinitionRegistry.Validation.Issues.Count; i++)
            {
                AbilityValidationIssue issue = DefinitionRegistry.Validation.Issues[i];
                string message = "[Ability Provider][" + issue.Code + "] " + issue.Message +
                                 (issue.Source != null ? " | " + issue.Source : string.Empty) +
                                 (!string.IsNullOrEmpty(issue.ReferenceChain) ? " | " + issue.ReferenceChain : string.Empty);
                if (issue.Severity == AbilityValidationSeverity.Error) Debug.LogError(message);
                else if (issue.Severity == AbilityValidationSeverity.Warning) Debug.LogWarning(message);
                else Debug.Log(message);
            }

            return DefinitionRegistry.ApplyTo(Engine);
        }
    }
}
