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
    public sealed partial class AbilityManager : Singleton<AbilityManager>
    {
        private const float GlobalSearchRadius = 9999f;

        // Keep stable adapters per live Game object so modifiers can compare units by EntityId.
        private readonly Dictionary<int, TdUnit> npcUnits = new Dictionary<int, TdUnit>();
        private readonly Dictionary<int, TdUnit> towerUnits = new Dictionary<int, TdUnit>();
        private readonly HashSet<int> activeNpcObjectIds = new HashSet<int>();
        private readonly HashSet<int> activeTowerObjectIds = new HashSet<int>();
        private readonly List<int> staleObjectIds = new List<int>();
        private readonly List<IAbilityDefinitionProvider> additionalDefinitionProviders = new List<IAbilityDefinitionProvider>();

        private TdUnit baseUnit;
        private TdPresentation presentation;
        private int nextRuntimeEntityId;
        private bool lifecycleEventsSubscribed;
        private bool initialized;
        private bool battleAvailable;
        private int battleSession;
        private string jsonManifestPath;

        /// <summary>Scripting/debug interop. Gameplay/UI code should use controlled handles and facade operations.</summary>
        public AbilitySystem Engine { get; } = new AbilitySystem();
        public AbilityDefinitionRegistry DefinitionRegistry { get; private set; }
        public bool IsInitialized => initialized;
        public bool IsAvailable => initialized && battleAvailable && !BattleFlowManager.Instance.HasEnded && Engine.IsRuntimeAvailable;

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
            initialized = false;
            battleAvailable = false;
            ClearBattleRuntime();
            npcUnits.Clear();
            towerUnits.Clear();
            activeNpcObjectIds.Clear();
            activeTowerObjectIds.Clear();
            staleObjectIds.Clear();
            baseUnit = null;
            nextRuntimeEntityId = 1;

            // Core runtime only sees interfaces; all project-specific lookup stays in adapters.
            presentation = new TdPresentation();
            Engine.Initialize(new TdWorld(this), presentation);
            if (!RegisterDefinitions())
            {
                initialized = false;
                Debug.LogError("AbilityManager initialization stopped because definition providers are invalid.");
                return false;
            }

            initialized = true;
            battleAvailable = true;
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

        /// <summary>Opt in before Initialize, using the project's configured ResourceManager asset location.</summary>
        public bool ConfigureJsonSources(string manifestAssetPath)
        {
            if (initialized || !TdJsonAbilityManifestProvider.IsHandAuthoredAssetPath(manifestAssetPath)) return false;
            jsonManifestPath = manifestAssetPath;
            return true;
        }

        public void Release()
        {
            UnsubscribeFromLifecycleEvents();
            initialized = false;
            battleAvailable = false;
            ClearBattleRuntime();
        }

        public void Update(float deltaTime)
        {
            if (!IsAvailable)
            {
                return;
            }

            SynchronizeUnitBindings();
            Engine.Update(deltaTime);
            PruneControlledEffects();
            PruneTowerAttacks();
        }

        public TdUnit GetUnit(Npc npc)
        {
            if (!IsAvailable || npc == null)
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
                if (!IsRegisteredNpc(npc)) return null;
                unit = new TdUnit(AllocateRuntimeEntityId(), npc);
                npcUnits[entityId] = unit;
                Engine.RegisterUnit(unit);
            }

            return unit;
        }

        public TdUnit GetUnit(Tower tower)
        {
            if (!IsAvailable || tower == null)
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
                if (!IsRegisteredTower(tower)) return null;
                unit = new TdUnit(AllocateRuntimeEntityId(), tower);
                towerUnits[entityId] = unit;
                Engine.RegisterUnit(unit);
            }

            return unit;
        }

        public TdUnit GetBaseUnit()
        {
            if (!IsAvailable || !BaseManager.Instance.HasBaseObject) return null;
            if (baseUnit == null)
            {
                baseUnit = TdUnit.CreateBaseUnit(AllocateRuntimeEntityId());
                Engine.RegisterUnit(baseUnit);
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

            TdUnit removed = baseUnit;
            baseUnit = null;
            removed.Invalidate();
            try { Engine.RemoveUnit(removed); }
            catch (Exception exception) { Debug.LogException(exception); }
            return true;
        }

        public RuntimeAbility AddAbilityToNpc(Npc npc, int skillId, int level = 1)
        {
            return AddAbility(GetUnit(npc), skillId, level);
        }

        public RuntimeAbility AddAbilityToTower(Tower tower, int skillId, int level = 0)
        {
            int resolvedLevel = level > 0 ? level : 1;
            return AddAbility(GetUnit(tower), skillId, resolvedLevel);
        }

        public RuntimeAbility AddAbilityToBase(int skillId, int level = 1)
        {
            return AddAbility(GetBaseUnit(), skillId, level);
        }

        public RuntimeAbility AddAbility(IUnit owner, int skillId, int level = 1)
        {
            if (!IsAvailable || owner == null)
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
            string name = AbilityConfigConverter.AbilityName(skillId);
            return Engine.FindAbility(owner, name) ?? Engine.AddAbility(owner, name, level, resourceOwner);
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
            return CastAbilityAtBestTarget(GetUnit(tower), skillId);
        }

        public CastResult CastTowerAbilityOnTarget(Tower tower, int skillId, Npc target)
        {
            return CastAbilityOnTarget(GetUnit(tower), skillId, GetUnit(target));
        }

        public CastResult CastBaseAbilityAtBestTarget(int skillId)
        {
            return CastAbilityAtBestTarget(GetBaseUnit(), skillId);
        }

        public bool TryGetBaseAbilityCooldown(int skillId, out float remaining, out float duration)
        {
            remaining = 0f;
            duration = 0f;

            RuntimeAbility ability = IsAvailable && baseUnit != null ? Engine.FindAbility(baseUnit, AbilityConfigConverter.AbilityName(skillId)) : null;
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
            return ability == null ? MissingOwnedAbility() : CastExistingAtBestTarget(ability);
        }

        public bool HasState(Npc npc, UnitState state)
        {
            return IsAvailable && Engine.HasState(FindUnit(npc), state);
        }

        public bool HasState(Tower tower, UnitState state)
        {
            return IsAvailable && Engine.HasState(FindUnit(tower), state);
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
            return IsAttackRestricted(FindUnit(tower));
        }

        public bool IsMovementRestricted(Npc npc)
        {
            TdUnit unit = FindUnit(npc);
            return !IsAvailable || Engine.HasState(unit, UnitState.Stunned) ||
                   Engine.HasState(unit, UnitState.Rooted) ||
                   Engine.HasState(unit, UnitState.OutOfGame) || Engine.HasState(unit, UnitState.CommandRestricted);
        }

        public DamageResult ApplyTowerAttackDamage(Tower tower, Npc target, float damage)
        {
            if (!IsAvailable || IsActionRestricted(tower)) return null;
            // One attack entry emits start, settlement and landed notifications exactly once.
            return Engine.ApplyAttackDamage(new DamageInfo
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
            if (!IsAvailable || IsAttackRestricted(npc)) return null;
            return Engine.ApplyAttackDamage(new DamageInfo
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
            TdUnit unit = FindUnit(npc);
            if (!IsAvailable || unit == null) return 1f;
            float bonus = Engine.GetModifierProperty(unit, ModifierProperty.MoveSpeedBonusPercent, null);
            return Mathf.Max(0f, 1f + bonus / 100f);
        }

        public float GetAttackIntervalMultiplier(Tower tower)
        {
            TdUnit unit = FindUnit(tower);
            if (!IsAvailable || unit == null) return 1f;
            float attackSpeedBonus = Engine.GetModifierProperty(unit, ModifierProperty.AttackSpeedBonus, null);
            return 1f / Mathf.Max(0.01f, 1f + attackSpeedBonus / 100f);
        }

        public void RegisterAbilityScript(int skillId, Func<AbilityScript> factory)
        {
            Engine.RegisterAbilityScript(AbilityConfigConverter.AbilityName(skillId), factory);
        }

        public void RegisterAbilityScript(string internalName, Func<AbilityScript> factory)
        {
            Engine.RegisterAbilityScript(internalName, factory);
        }

        public void RegisterModifierScript(int modifierId, Func<ModifierScript> factory)
        {
            Engine.RegisterModifierScript(AbilityConfigConverter.ModifierName(modifierId), factory);
        }

        public void RegisterModifierScript(string internalName, Func<ModifierScript> factory)
        {
            Engine.RegisterModifierScript(internalName, factory);
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
            if (!IsAvailable) return null;
            TdUnit unit = GetUnit(npc);
            return Engine.AddModifier(unit, unit, AbilityConfigConverter.ModifierName(modifierId), duration);
        }

        public Modifier AddModifierToTower(Tower tower, int modifierId, float duration = float.NaN)
        {
            if (!IsAvailable) return null;
            TdUnit unit = GetUnit(tower);
            return Engine.AddModifier(unit, unit, AbilityConfigConverter.ModifierName(modifierId), duration);
        }

        public Modifier AddModifierToBase(int modifierId, float duration = float.NaN)
        {
            if (!IsAvailable) return null;
            TdUnit unit = GetBaseUnit();
            return Engine.AddModifier(unit, unit, AbilityConfigConverter.ModifierName(modifierId), duration);
        }

        private RuntimeAbility EnsureAbility(IUnit caster, int skillId, int level = 1)
        {
            if (!IsAvailable || caster == null)
            {
                return null;
            }

            // Existing business callers can cast by skill id without manually adding the ability first.
            string abilityName = AbilityConfigConverter.AbilityName(skillId);
            RuntimeAbility ability = Engine.FindAbility(caster, abilityName);
            if (ability != null)
            {
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
            BattleFlowManager.Instance.BattleStarted += OnBattleStarted;
            Engine.EventRaised += OnRuntimeEvent;
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
            BattleFlowManager.Instance.BattleStarted -= OnBattleStarted;
            Engine.EventRaised -= OnRuntimeEvent;
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

        private void OnBattleStarted()
        {
            if (!initialized) return;
            battleAvailable = true;
            SynchronizeUnitBindings();
        }

        private void OnBattleCompleted(BattleEndedMessage message)
        {
            battleAvailable = false;
            ClearBattleRuntime();
        }

        private void ClearBattleRuntime()
        {
            battleSession++;
            controlledEffects.Clear();
            pendingTowerAttacks.Clear();
            nextEffectHandle = 0;
            foreach (TdUnit unit in npcUnits.Values) unit.Invalidate();
            foreach (TdUnit unit in towerUnits.Values) unit.Invalidate();
            baseUnit?.Invalidate();
            try { Engine.ClearRuntime(); }
            catch (Exception exception) { Debug.LogException(exception); }
            presentation?.Clear();
            npcUnits.Clear();
            towerUnits.Clear();
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

            npcUnits.Remove(objectId);
            unit.Invalidate();
            try { Engine.RemoveUnit(unit); }
            catch (Exception exception) { Debug.LogException(exception); }
            return true;
        }

        private bool UnregisterTowerObject(int objectId)
        {
            if (!towerUnits.TryGetValue(objectId, out TdUnit unit))
            {
                return false;
            }

            towerUnits.Remove(objectId);
            unit.Invalidate();
            try { Engine.RemoveUnit(unit); }
            catch (Exception exception) { Debug.LogException(exception); }
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

            // Cast selection can re-enter through custom state/property scripts. Keep candidates local.
            List<IUnit> searchResults = new List<IUnit>();
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
            if (!string.IsNullOrEmpty(jsonManifestPath)) providers.Add(new TdJsonAbilityManifestProvider(jsonManifestPath));

            DefinitionRegistry = new AbilityDefinitionRegistry();
            DefinitionRegistry.LoadProviders(providers);
            bool applied = DefinitionRegistry.ApplyTo(Engine);
            AbilityValidationReport report = DefinitionRegistry.LastApplyValidation ?? DefinitionRegistry.Validation;
            for (int i = 0; i < report.Issues.Count; i++)
            {
                AbilityValidationIssue issue = report.Issues[i];
                string message = "[Ability Provider][" + issue.Code + "] " + issue.Message +
                                 (issue.Source != null ? " | " + issue.Source : string.Empty) +
                                 (!string.IsNullOrEmpty(issue.ReferenceChain) ? " | " + issue.ReferenceChain : string.Empty);
                if (issue.Severity == AbilityValidationSeverity.Error) Debug.LogError(message);
                else if (issue.Severity == AbilityValidationSeverity.Warning) Debug.LogWarning(message);
                else Debug.Log(message);
            }

            return applied;
        }
    }
}

