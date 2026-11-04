using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Ability
{
    /// <summary>
    /// Central runtime for definitions, ability instances, modifiers, thinkers, and projectiles.
    /// It is intentionally Game-agnostic: all business state is reached through adapters.
    /// </summary>
    public sealed partial class AbilitySystem
    {
        // Definitions and script factories are registered once, then reused by runtime instances.
        private readonly Dictionary<string, AbilityDefinition> abilityDefinitions = new Dictionary<string, AbilityDefinition>();
        private readonly Dictionary<string, ModifierDefinition> modifierDefinitions = new Dictionary<string, ModifierDefinition>();
        private readonly Dictionary<string, Func<AbilityScript>> abilityFactories = new Dictionary<string, Func<AbilityScript>>();
        private readonly Dictionary<string, Func<ModifierScript>> modifierFactories = new Dictionary<string, Func<ModifierScript>>();
        // Runtime containers are owned by the engine so removal can clean up modifiers automatically.
        private readonly Dictionary<int, List<Ability>> abilitiesByUnit = new Dictionary<int, List<Ability>>();
        private readonly List<Modifier> modifiers = new List<Modifier>();
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly List<Thinker> thinkers = new List<Thinker>();

        public IWorld World { get; private set; }
        public IPresentation Presentation { get; private set; }
        public bool IsClearingRuntime { get; private set; }
        public IReadOnlyList<Modifier> Modifiers => modifiers;
        public IReadOnlyList<Projectile> Projectiles => projectiles;
        public IReadOnlyList<Thinker> Thinkers => thinkers;

        public AbilityRuntimeSnapshot CreateRuntimeSnapshot()
        {
            using (BeginExecution())
            {
                if (!TryConsumeExecutionBudget("RuntimeSnapshot")) return new AbilityRuntimeSnapshot();
                long generation = RuntimeGeneration;
                AbilityRuntimeSnapshot snapshot = CreateRuntimeSnapshotCore();
                return generation == RuntimeGeneration ? snapshot : new AbilityRuntimeSnapshot();
            }
        }

        private AbilityRuntimeSnapshot CreateRuntimeSnapshotCore()
        {
            AbilityRuntimeSnapshot snapshot = new AbilityRuntimeSnapshot();
            foreach (KeyValuePair<int, List<Ability>> pair in abilitiesByUnit)
            {
                List<Ability> abilities = pair.Value;
                AbilityUnitRuntimeSnapshot unitSnapshot = new AbilityUnitRuntimeSnapshot { EntityId = pair.Key };
                if (abilities != null && abilities.Count > 0 && abilities[0]?.Owner != null)
                {
                    IUnit owner = abilities[0].Owner;
                    unitSnapshot.TeamId = owner.TeamId;
                    unitSnapshot.Position = owner.Position;
                    unitSnapshot.IsAlive = owner.IsAlive;
                }

                if (abilities != null)
                {
                    for (int i = 0; i < abilities.Count; i++)
                    {
                        Ability ability = abilities[i];
                        if (ability == null) continue;
                        unitSnapshot.Abilities.Add(new AbilityInstanceRuntimeSnapshot
                        {
                            Name = ability.Definition?.Name,
                            Level = ability.Level,
                            Phase = ability.Phase,
                            Activated = ability.Activated,
                            ToggleEnabled = ability.ToggleEnabled,
                            CooldownRemaining = ability.CooldownRemaining,
                            Charges = ability.Charges
                        });
                    }
                }

                snapshot.Units.Add(unitSnapshot);
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                Modifier modifier = modifiers[i];
                if (modifier == null) continue;
                ModifierRuntimeSnapshot modifierSnapshot = new ModifierRuntimeSnapshot
                {
                    Name = modifier.Name,
                    ParentEntityId = modifier.Parent != null ? modifier.Parent.EntityId : 0,
                    CasterEntityId = modifier.Caster != null ? modifier.Caster.EntityId : 0,
                    AbilityName = modifier.Ability?.Definition?.Name,
                    Stacks = modifier.StackCount,
                    Duration = modifier.Duration,
                    RemainingTime = modifier.RemainingTime,
                    States = GetRuntimeStates(modifier)
                };
                foreach (ModifierProperty property in Enum.GetValues(typeof(ModifierProperty)))
                {
                    if (property == ModifierProperty.None) continue;
                    float value = modifier.GetProperty(property, new ModifierPropertyContext { Engine = this, Unit = modifier.Parent, Ability = modifier.Ability });
                    if (value != 0f) modifierSnapshot.Properties.Add(new ModifierPropertyRuntimeSnapshot { Property = property, Value = value });
                }
                snapshot.Modifiers.Add(modifierSnapshot);
            }

            for (int i = 0; i < projectiles.Count; i++)
            {
                Projectile projectile = projectiles[i];
                if (projectile == null) continue;
                snapshot.Projectiles.Add(new ProjectileRuntimeSnapshot
                {
                    Name = projectile.Definition?.Name,
                    AbilityName = projectile.Ability?.Definition?.Name,
                    CasterEntityId = projectile.Caster != null ? projectile.Caster.EntityId : 0,
                    TargetEntityId = projectile.Target != null ? projectile.Target.EntityId : 0,
                    Position = projectile.Position,
                    Tracking = projectile.Tracking,
                    Destroyed = projectile.Destroyed
                });
            }

            for (int i = 0; i < thinkers.Count; i++)
            {
                Thinker thinker = thinkers[i];
                if (thinker == null) continue;
                snapshot.Thinkers.Add(new ThinkerRuntimeSnapshot
                {
                    AbilityName = thinker.Ability?.Definition?.Name,
                    CasterEntityId = thinker.Caster != null ? thinker.Caster.EntityId : 0,
                    Position = thinker.Position,
                    Duration = thinker.Duration,
                    Interval = thinker.Interval,
                    Radius = thinker.Radius,
                    Destroyed = thinker.IsDestroyed
                });
            }

            if (Presentation is ITrackedPresentation trackedPresentation)
            {
                trackedPresentation.GetActivePresentationHandles(snapshot.PresentationHandles);
            }

            return snapshot;
        }
        public event Action<RuntimeEvent> EventRaised;

        /// <summary>
        /// Binds engine-neutral services. Calling Initialize also clears all runtime state.
        /// </summary>
        public void Initialize(IWorld world, IPresentation presentation = null)
        {
            if (IsClearingRuntime) throw new InvalidOperationException("Cannot replace runtime services from a cleanup callback.");
            // Release through the old adapters before replacing the service boundary.
            ClearRuntime();
            World = world;
            Presentation = presentation;
            removedUnitIds.Clear();
        }

        /// <summary>
        /// Removes every runtime object and invokes destruction hooks before references are dropped.
        /// </summary>
        public void ClearRuntime()
        {
            if (IsClearingRuntime) return;
            IsClearingRuntime = true;
            RuntimeGeneration++;
            var errors = new List<Exception>();
            try
            {
                Cleanup(() => ResetCastingState(), errors);
                Cleanup(() => ResetCombatState(), errors);
                foreach (int id in registeredUnits.Keys) removedUnitIds.Add(id);
                foreach (int id in abilitiesByUnit.Keys) removedUnitIds.Add(id);
                foreach (Projectile projectile in projectiles.ToArray()) Cleanup(() => RemoveProjectile(projectile), errors);
                foreach (Thinker thinker in thinkers.ToArray()) Cleanup(() => RemoveThinker(thinker), errors);
                foreach (Modifier modifier in modifiers.ToArray()) Cleanup(() => RemoveModifier(modifier), errors);
                var abilities = new List<Ability>();
                foreach (List<Ability> bucket in abilitiesByUnit.Values) abilities.AddRange(bucket);
                foreach (Ability ability in abilities) Cleanup(() => RemoveAbility(ability.Owner, ability.Definition.Name), errors);
            }
            finally
            {
                abilitiesByUnit.Clear();
                modifiers.Clear();
                projectiles.Clear();
                thinkers.Clear();
                registeredUnits.Clear();
                IsClearingRuntime = false;
            }
            if (errors.Count > 0) throw new AggregateException("Runtime was released, but lifecycle callbacks failed.", errors);
        }

        private static void Cleanup(Action action, IList<Exception> errors)
        {
            try { action(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        public void RegisterAbilityDefinition(AbilityDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.Name))
            {
                abilityDefinitions[definition.Name] = definition;
            }
        }

        public void RegisterModifierDefinition(ModifierDefinition definition)
        {
            if (definition != null && !string.IsNullOrEmpty(definition.Name))
            {
                modifierDefinitions[definition.Name] = definition;
            }
        }

        public void RegisterAbilityScript(string abilityName, Func<AbilityScript> factory)
        {
            if (!string.IsNullOrEmpty(abilityName) && factory != null)
            {
                abilityFactories[abilityName] = factory;
            }
        }

        public void RegisterModifierScript(string modifierName, Func<ModifierScript> factory)
        {
            if (!string.IsNullOrEmpty(modifierName) && factory != null)
            {
                modifierFactories[modifierName] = factory;
            }
        }

        public bool TryGetAbilityDefinition(string abilityName, out AbilityDefinition definition)
        {
            return abilityDefinitions.TryGetValue(abilityName, out definition);
        }

        public bool TryGetModifierDefinition(string modifierName, out ModifierDefinition definition)
        {
            return modifierDefinitions.TryGetValue(modifierName, out definition);
        }

        public Ability AddAbility(IUnit owner, string abilityName, int level = 1, IResourceOwner resourceOwner = null)
        {
            using (BeginExecution())
            {
                if (!IsRuntimeAvailable || IsUnitRemoving(owner) || owner == null || string.IsNullOrEmpty(abilityName) || !abilityDefinitions.TryGetValue(abilityName, out AbilityDefinition definition) || !TryConsumeExecutionBudget("AddAbility")) return null;
                if (registeredUnits.TryGetValue(owner.EntityId, out IUnit canonical) && !ReferenceEquals(canonical, owner)) return null;
                registeredUnits[owner.EntityId] = owner;
                Ability existing = FindAbility(owner, abilityName);
                if (existing != null) { existing.SetLevel(level); return existing; }
                long generation = RuntimeGeneration;
                Ability ability = new Ability(this, definition, owner, resourceOwner, CreateAbilityScript(abilityName, definition), level);
                if (generation != RuntimeGeneration || !IsRuntimeAvailable || IsUnitRemoving(owner)) return null;
                if (!abilitiesByUnit.TryGetValue(owner.EntityId, out List<Ability> abilities))
                {
                    abilities = new List<Ability>();
                    abilitiesByUnit.Add(owner.EntityId, abilities);
                }
                abilities.Add(ability);
                try { ability.InitializeRuntime(); }
                catch { RemoveAbility(owner, abilityName); throw; }
                if (!IsAbilityRegistered(ability)) return null;
                RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.AbilityAdded, Ability = ability, Caster = owner });
                return IsAbilityRegistered(ability) ? ability : null;
            }
        }

        public bool RemoveAbility(IUnit owner, string abilityName)
        {
            if (owner == null || string.IsNullOrEmpty(abilityName) || !abilitiesByUnit.TryGetValue(owner.EntityId, out List<Ability> abilities)) return false;
            if (registeredUnits.TryGetValue(owner.EntityId, out IUnit canonical) && !ReferenceEquals(canonical, owner)) return false;
            Ability ability = abilities.Find(value => value.Definition.Name == abilityName);
            if (ability == null) return false;
            abilities.Remove(ability);
            if (abilities.Count == 0) abilitiesByUnit.Remove(owner.EntityId);
            var errors = new List<Exception>();
            Cleanup(() => ability.Remove(), errors);
            Cleanup(() => RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.AbilityRemoved, Ability = ability, Caster = ability.Owner }), errors);
            if (errors.Count > 0) throw new AggregateException("Ability was removed, but lifecycle callbacks failed.", errors);
            return true;
        }

        /// <summary>
        /// Removes all runtime state that keeps a unit alive in the ability engine. Integration
        /// layers call this before releasing or replacing a dynamic business object.
        /// </summary>
        public void RemoveUnit(IUnit unit)
        {
            if (unit != null && registeredUnits.TryGetValue(unit.EntityId, out IUnit canonical) && !ReferenceEquals(canonical, unit)) return;
            if (unit == null || !removedUnitIds.Add(unit.EntityId)) return;
            removingUnitIds.Add(unit.EntityId);
            try
            {
                var errors = new List<Exception>();
                Cleanup(() => ClearQueuedOrdersForUnit(unit), errors);
                Cleanup(() => RemoveCombatUnit(unit), errors);
                Cleanup(() => NotifyAbilitiesUnitRemoved(unit), errors);
                foreach (Projectile value in projectiles.ToArray()) if (ReferencesUnit(value, unit)) Cleanup(() => RemoveProjectile(value), errors);
                foreach (Thinker value in thinkers.ToArray()) if (ReferencesUnit(value, unit)) Cleanup(() => RemoveThinker(value), errors);
                foreach (Modifier value in modifiers.ToArray()) if (ReferencesUnit(value, unit)) Cleanup(() => RemoveModifier(value), errors);
                if (abilitiesByUnit.TryGetValue(unit.EntityId, out List<Ability> abilities))
                {
                    foreach (Ability ability in abilities.ToArray()) Cleanup(() => RemoveAbility(unit, ability.Definition.Name), errors);
                }
                registeredUnits.Remove(unit.EntityId);
                if (errors.Count > 0) throw new AggregateException("Unit was released, but lifecycle callbacks failed.", errors);
            }
            finally { removingUnitIds.Remove(unit.EntityId); }
        }

        public Ability FindAbility(IUnit owner, string abilityName)
        {
            if (owner == null || string.IsNullOrEmpty(abilityName) || !abilitiesByUnit.TryGetValue(owner.EntityId, out List<Ability> abilities))
            {
                return null;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i].Definition.Name == abilityName)
                {
                    return abilities[i];
                }
            }

            return null;
        }

        public IReadOnlyList<Ability> GetAbilities(IUnit owner)
        {
            return owner != null && abilitiesByUnit.TryGetValue(owner.EntityId, out List<Ability> abilities) ? abilities : Array.Empty<Ability>();
        }

        public CastResult IssueOrder(CastOrder order)
        {
            return IssueOrderCore(order);
        }

        public CastResult CastAbility(IUnit caster, string abilityName)
        {
            return IssueOrder(new CastOrder { Caster = caster, AbilityName = abilityName });
        }

        public CastResult CastAbilityOnTarget(IUnit caster, string abilityName, IUnit target)
        {
            return IssueOrder(new CastOrder { Caster = caster, AbilityName = abilityName, Target = target });
        }

        public CastResult CastAbilityOnPosition(IUnit caster, string abilityName, Vector3 position)
        {
            return IssueOrder(new CastOrder { Caster = caster, AbilityName = abilityName, TargetPosition = position, HasTargetPosition = true });
        }

        public void Update(float deltaTime)
        {
            if (!IsRuntimeAvailable || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            long generation = RuntimeGeneration;
            // Objects created by callbacks begin aging on the next update, never retroactively.
            Modifier[] modifierSnapshot = modifiers.ToArray();
            Thinker[] thinkerSnapshot = thinkers.ToArray();
            Projectile[] projectileSnapshot = projectiles.ToArray();
            var abilitySnapshot = new List<Ability>();
            foreach (List<Ability> bucket in abilitiesByUnit.Values) abilitySnapshot.AddRange(bucket);
            foreach (Ability ability in abilitySnapshot)
            {
                if (generation != RuntimeGeneration) return;
                if (IsAbilityRegistered(ability))
                {
                    using (BeginExecution()) ability.Tick(deltaTime);
                }
            }
            foreach (Modifier modifier in modifierSnapshot)
            {
                if (generation != RuntimeGeneration) return;
                if (modifier != null && !modifier.IsDestroyed && modifiers.Contains(modifier)) modifier.Tick(deltaTime);
            }
            foreach (Thinker thinker in thinkerSnapshot)
            {
                if (generation != RuntimeGeneration) return;
                if (thinker == null || !thinkers.Contains(thinker)) continue;
                thinker.Tick(deltaTime);
                if (thinker.IsDestroyed) RemoveThinker(thinker);
            }
            foreach (Projectile projectile in projectileSnapshot)
            {
                if (generation != RuntimeGeneration) return;
                if (projectile == null || !projectiles.Contains(projectile)) continue;
                using (BeginExecution()) projectile.Tick(this, deltaTime);
                if (projectile.Destroyed) RemoveProjectile(projectile);
            }
            if (generation == RuntimeGeneration)
            {
                using (BeginExecution()) TickQueuedOrders();
            }
        }

        public Modifier AddModifier(IUnit caster, IUnit parent, Ability ability, string modifierName, ModifierApplyOptions options = null)
        {
            using (BeginExecution())
            {
                if (!IsRuntimeAvailable || parent == null || IsUnitRemoving(parent) || IsUnitRemoving(caster) || string.IsNullOrEmpty(modifierName) || !modifierDefinitions.TryGetValue(modifierName, out ModifierDefinition definition) || !TryConsumeExecutionBudget("AddModifier")) return null;
                long generation = RuntimeGeneration;
                if ((!parent.IsAlive && definition.RemoveOnDeath) || (HasState(parent, UnitState.Invulnerable) && (definition.Attributes & ModifierAttribute.IgnoreInvulnerable) == 0)) return null;
                if (generation != RuntimeGeneration || !IsRuntimeAvailable || IsUnitRemoving(parent) || IsUnitRemoving(caster) || (!parent.IsAlive && definition.RemoveOnDeath) || !TryConsumeExecutionBudget("AddModifier.Commit")) return null;
                var resolvedOptions = options ?? new ModifierApplyOptions();
                if (float.IsInfinity(resolvedOptions.Duration)) return null;
                if ((definition.Attributes & ModifierAttribute.Multiple) == 0)
                {
                    Modifier existing = FindRefreshableModifier(caster, parent, ability, modifierName, resolvedOptions);
                    if (existing != null) { existing.Refresh(options); return existing.IsDestroyed ? null : existing; }
                }
                Modifier modifier = new Modifier(this, definition, caster, parent, ability, CreateModifierScript(modifierName, definition), resolvedOptions);
                if (generation != RuntimeGeneration || !IsRuntimeAvailable || IsUnitRemoving(parent) || IsUnitRemoving(caster)) return null;
                modifiers.Add(modifier);
                try { modifier.OnRegistered(resolvedOptions); }
                catch { RemoveModifier(modifier); throw; }
                if (modifier.IsDestroyed || !modifiers.Contains(modifier)) return null;
                DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.ModifierAdded, Engine = this, Source = caster, Target = parent, Ability = ability, Modifier = modifier });
                if (modifier.IsDestroyed || !modifiers.Contains(modifier)) return null;
                RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ModifierAdded, Ability = ability, Modifier = modifier, Caster = caster, Target = parent });
                return modifier.IsDestroyed ? null : modifier;
            }
        }

        public Modifier AddModifier(IUnit caster, IUnit parent, string modifierName, float duration = float.NaN)
        {
            return AddModifier(caster, parent, null, modifierName, new ModifierApplyOptions { Duration = duration });
        }

        public bool RemoveModifier(Modifier modifier)
        {
            if (modifier == null || !modifiers.Remove(modifier)) return false;
            try { modifier.Destroy(); }
            finally
            {
                try { if (!IsClearingRuntime) DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.ModifierRemoved, Engine = this, Source = modifier.Caster, Target = modifier.Parent, Ability = modifier.Ability, Modifier = modifier }); }
                finally { RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ModifierRemoved, Ability = modifier.Ability, Modifier = modifier, Caster = modifier.Caster, Target = modifier.Parent }); }
            }
            return true;
        }

        public int Purge(IUnit unit, bool removePositiveBuffs, bool removeDebuffs, bool onlyPurgable)
        {
            using (BeginExecution())
            {
                if (unit == null || !IsRuntimeAvailable || !TryConsumeExecutionBudget("Purge")) return 0;
                int removed = 0;
                foreach (Modifier modifier in modifiers.ToArray())
                {
                    if (modifier.IsDestroyed || !IsSameUnit(modifier.Parent, unit) || !modifiers.Contains(modifier)) continue;
                    bool debuff = modifier.Script.IsDebuff();
                    if ((debuff && !removeDebuffs) || (!debuff && !removePositiveBuffs) || (onlyPurgable && !modifier.Script.IsPurgable())) continue;
                    if (RemoveModifier(modifier)) removed++;
                }
                return removed;
            }
        }

        public bool HasState(IUnit unit, UnitState state)
        {
            using (BeginExecution())
            {
                if (unit == null || state == UnitState.None || !TryConsumeExecutionBudget("HasState")) return false;
                if ((state & UnitState.Invulnerable) != 0 && unit.IsInvulnerable) return true;
                if ((state & UnitState.MagicImmune) != 0 && unit.IsMagicImmune) return true;
                long generation = RuntimeGeneration;
                foreach (Modifier modifier in modifiers.ToArray())
                {
                    if (generation != RuntimeGeneration) return false;
                    if (modifier.IsDestroyed || !IsSameUnit(modifier.Parent, unit)) continue;
                    bool matches = modifier.CheckState(state);
                    if (generation != RuntimeGeneration) return false;
                    if (matches) return true;
                }
                return false;
            }
        }

        public float GetModifierProperty(IUnit unit, ModifierProperty property, ModifierPropertyContext context)
        {
            using (BeginExecution())
            {
                if (unit == null || property == ModifierProperty.None || !TryConsumeExecutionBudget("GetModifierProperty")) return 0f;
                float total = 0f;
                long generation = RuntimeGeneration;
                foreach (Modifier modifier in modifiers.ToArray())
                {
                    if (generation != RuntimeGeneration) return 0f;
                    if (modifier.IsDestroyed || !IsSameUnit(modifier.Parent, unit)) continue;
                    var propertyContext = new ModifierPropertyContext { Engine = this, Unit = unit, Ability = context?.Ability, DamageInfo = CombatSnapshots.Clone(context?.DamageInfo), DamageResult = CombatSnapshots.Clone(context?.DamageResult) };
                    float value = modifier.GetProperty(property, propertyContext);
                    if (generation != RuntimeGeneration) return 0f;
                    if (!float.IsNaN(value) && !float.IsInfinity(value)) total += value;
                }
                return float.IsNaN(total) || float.IsInfinity(total) ? 0f : total;
            }
        }

        public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
        {
            if (results == null || World == null)
            {
                return;
            }

            // World returns broad candidates; TargetQuery applies canonical ability rules.
            List<IUnit> candidates = new List<IUnit>();
            World.FindUnits(center, radius, query, candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                IUnit unit = candidates[i];
                if (unit != null && (query == null || query.IsValid(unit)) && !results.Contains(unit))
                {
                    results.Add(unit);
                }
            }
        }





        public Projectile CreateProjectile(ProjectileRequest request)
        {
            using (BeginExecution())
            {
                if (!IsRuntimeAvailable || request?.Definition == null || !IsFinite(request.Origin) || !IsFinite(request.Direction) || IsUnitRemoving(request.Caster) || IsUnitRemoving(request.Source) || IsUnitRemoving(request.Target) || !TryConsumeExecutionBudget("CreateProjectile")) return null;
                Projectile projectile = new Projectile(request);
                projectiles.Add(projectile);
                try { projectile.OnRegistered(Presentation); }
                catch { RemoveProjectile(projectile); throw; }
                if (!projectiles.Contains(projectile)) return null;
                RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ProjectileCreated, Projectile = projectile, Ability = request.Ability, Caster = request.Caster, Target = request.Target, Position = request.Origin });
                return projectiles.Contains(projectile) ? projectile : null;
            }
        }

        public bool RemoveProjectile(Projectile projectile)
        {
            if (projectile == null || !projectiles.Remove(projectile)) return false;
            try { projectile.Destroy(); }
            finally { RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ProjectileDestroyed, Projectile = projectile, Ability = projectile.Ability, Caster = projectile.Caster, Position = projectile.Position }); }
            return true;
        }

        public Projectile CreateTrackingProjectile(Ability ability, IUnit caster, IUnit target, ProjectileDefinition definition)
        {
            return caster == null || target == null || definition == null ? null : CreateProjectile(new ProjectileRequest { Definition = definition, Ability = ability, Caster = caster, Source = caster, Target = target, Origin = caster.Position, Tracking = true });
        }

        public Projectile CreateLinearProjectile(Ability ability, IUnit caster, Vector3 origin, Vector3 direction, ProjectileDefinition definition)
        {
            return definition == null ? null : CreateProjectile(new ProjectileRequest { Definition = definition, Ability = ability, Caster = caster, Source = caster, Origin = origin, Direction = direction, Tracking = false });
        }

        public Thinker CreateThinker(ThinkerRequest request)
        {
            using (BeginExecution())
            {
                if (!IsRuntimeAvailable || request == null || !IsFinite(request.Position) || !CombatSnapshots.IsFinite(request.Duration) || !CombatSnapshots.IsFinite(request.Interval) || !CombatSnapshots.IsFinite(request.Radius) || request.Interval < 0f || request.Radius < 0f || IsUnitRemoving(request.Caster) || !TryConsumeExecutionBudget("CreateThinker")) return null;
                Thinker thinker = new Thinker(this, request);
                thinkers.Add(thinker);
                try { thinker.OnRegistered(); }
                catch { RemoveThinker(thinker); throw; }
                if (thinker.IsDestroyed || !thinkers.Contains(thinker)) return null;
                RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ThinkerCreated, Thinker = thinker, Ability = thinker.Ability, Caster = thinker.Caster, Position = thinker.Position });
                return thinker.IsDestroyed ? null : thinker;
            }
        }

        public bool RemoveThinker(Thinker thinker)
        {
            if (thinker == null || !thinkers.Remove(thinker))
            {
                return false;
            }

            try { thinker.Destroy(); }
            finally { RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ThinkerDestroyed, Thinker = thinker, Ability = thinker.Ability, Caster = thinker.Caster, Position = thinker.Position }); }
            return true;
        }

        internal void OnProjectileHit(Projectile projectile, IUnit target, Vector3 position)
        {
            if (projectile == null || projectile.Destroyed || !projectiles.Contains(projectile)) return;
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.ProjectileHit, Engine = this, Source = projectile.Caster, Target = target, Ability = projectile.Ability, Projectile = projectile, Position = position });
            if (projectile.Destroyed || !projectiles.Contains(projectile)) return;
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ProjectileHit, Ability = projectile.Ability, Projectile = projectile, Caster = projectile.Caster, Target = target, Position = position });
        }

        internal void RefreshAura(Modifier aura)
        {
            if (!IsRuntimeAvailable || aura == null || aura.IsDestroyed || !modifiers.Contains(aura) || aura.Definition == null || string.IsNullOrEmpty(aura.Definition.AuraModifierName))
            {
                return;
            }

            // Aura effects are represented as short-lived child modifiers refreshed by the source.
            ModifierDefinition definition = aura.Definition;
            TargetQuery query = new TargetQuery { Engine = this, Caster = aura.Parent, Team = definition.AuraTargetTeam, Types = definition.AuraTargetType, Flags = definition.AuraTargetFlags };
            List<IUnit> units = new List<IUnit>();
            FindUnits(aura.Parent.Position, definition.AuraRadius, query, units);
            long generation = RuntimeGeneration;
            for (int i = 0; i < units.Count; i++)
            {
                if (!IsRuntimeAvailable || generation != RuntimeGeneration || aura.IsDestroyed || !modifiers.Contains(aura)) break;
                AddModifier(aura.Parent, units[i], aura.Ability, definition.AuraModifierName, new ModifierApplyOptions { Duration = Mathf.Max(0.1f, definition.AuraDuration), IsAura = true, SourceModifier = aura, Level = aura.Level, IncrementStacksOnRefresh = definition.AuraIncrementsStacks });
            }
        }

        internal void DispatchModifierEvent(ModifierEvent modifierEvent)
        {
            using (BeginExecution())
            {
                if (modifierEvent == null || !IsRuntimeAvailable || !TryConsumeExecutionBudget("DispatchModifierEvent")) return;
                long generation = RuntimeGeneration;
                foreach (Modifier modifier in modifiers.ToArray())
                {
                    if (generation != RuntimeGeneration || !TryConsumeExecutionBudget("ModifierEvent.Listener")) break;
                    if (modifier != null && !modifier.IsDestroyed && modifiers.Contains(modifier)) modifier.Script.OnEvent(CombatSnapshots.CloneEvent(modifierEvent));
                }
            }
        }

        internal void RaiseCastFailed(Ability ability, CastOrder order, CastResult result)
        {
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.CastFailed, CastId = result != null ? result.CastId : 0, Ability = ability, Caster = order != null ? order.Caster : null, Target = order != null ? order.Target : null, Position = order != null && order.HasTargetPosition ? order.TargetPosition : Vector3.zero, FailureReason = result != null ? result.FailureReason : CastFailureReason.None, Message = result != null ? result.Message : null });
        }

        internal void RaiseEvent(RuntimeEvent runtimeEvent)
        {
            using (BeginExecution())
            {
                Action<RuntimeEvent> listeners = EventRaised;
                if (runtimeEvent == null || listeners == null) return;
                bool diagnostic = runtimeEvent.EventType == RuntimeEventType.ExecutionAborted;
                long generation = RuntimeGeneration;
                foreach (Action<RuntimeEvent> listener in listeners.GetInvocationList())
                {
                    if (generation != RuntimeGeneration) break;
                    if (!diagnostic && !TryConsumeExecutionBudget("RuntimeEvent.Listener")) break;
                    listener(new RuntimeEvent { EventType = runtimeEvent.EventType, CastId = runtimeEvent.CastId, AttackId = runtimeEvent.AttackId, Ability = runtimeEvent.Ability, Modifier = runtimeEvent.Modifier, Projectile = runtimeEvent.Projectile, Thinker = runtimeEvent.Thinker, Caster = runtimeEvent.Caster, Target = runtimeEvent.Target, Position = runtimeEvent.Position, Value = runtimeEvent.Value, FailureReason = runtimeEvent.FailureReason, Message = runtimeEvent.Message });
                }
            }
        }

        private static UnitState GetRuntimeStates(Modifier modifier)
        {
            UnitState result = UnitState.None;
            foreach (UnitState state in Enum.GetValues(typeof(UnitState)))
            {
                if (state != UnitState.None && modifier.CheckState(state)) result |= state;
            }
            return result;
        }

        private AbilityScript CreateAbilityScript(string abilityName, AbilityDefinition definition)
        {
            if (abilityFactories.TryGetValue(abilityName, out Func<AbilityScript> factory))
            {
                AbilityScript script = factory();
                if (script != null)
                {
                    return script;
                }
            }

            return definition != null && definition.Actions.Count > 0 ? new ConfiguredAbilityScript() : new DefaultAbilityScript();
        }

        private ModifierScript CreateModifierScript(string modifierName, ModifierDefinition definition)
        {
            if (modifierFactories.TryGetValue(modifierName, out Func<ModifierScript> factory))
            {
                ModifierScript script = factory();
                if (script != null)
                {
                    return script;
                }
            }

            return definition != null && HasConfiguredModifierActions(definition) ? new ConfiguredModifierScript() : new DefaultModifierScript();
        }

        private Modifier FindRefreshableModifier(IUnit caster, IUnit parent, Ability ability, string modifierName, ModifierApplyOptions options)
        {
            foreach (Modifier modifier in modifiers)
            {
                if (modifier.IsDestroyed || !IsSameUnit(modifier.Parent, parent) || modifier.Name != modifierName) continue;
                if (modifier.IsAuraApplied != options.IsAura || (options.IsAura && !ReferenceEquals(modifier.SourceAura, options.SourceModifier))) continue;
                bool sameCaster = ReferenceEquals(caster, modifier.Caster) || IsSameUnit(caster, modifier.Caster);
                if (sameCaster && ReferenceEquals(ability, modifier.Ability)) return modifier;
            }
            return null;
        }

        private static bool HasConfiguredModifierActions(ModifierDefinition definition)
        {
            return definition.OnCreatedActions.Count > 0 || definition.OnRefreshActions.Count > 0 || definition.OnDestroyActions.Count > 0 || definition.IntervalActions.Count > 0 || definition.TriggerActions.Count > 0;
        }

        private bool IsAbilityRegistered(Ability ability)
        {
            return ability != null && ability.Owner != null && abilitiesByUnit.TryGetValue(ability.Owner.EntityId, out List<Ability> abilities) && abilities.Contains(ability);
        }

        private static bool ReferencesUnit(Projectile projectile, IUnit unit)
        {
            return IsSameUnit(projectile.Caster, unit) ||
                   IsSameUnit(projectile.Source, unit) ||
                   IsSameUnit(projectile.Target, unit) ||
                   IsSameUnit(projectile.Ability != null ? projectile.Ability.Owner : null, unit);
        }

        private static bool ReferencesUnit(Thinker thinker, IUnit unit)
        {
            return IsSameUnit(thinker.Caster, unit) ||
                   IsSameUnit(thinker.Ability != null ? thinker.Ability.Owner : null, unit);
        }

        private static bool ReferencesUnit(Modifier modifier, IUnit unit)
        {
            if (IsSameUnit(modifier.Caster, unit) ||
                IsSameUnit(modifier.Parent, unit) ||
                IsSameUnit(modifier.Ability != null ? modifier.Ability.Owner : null, unit))
            {
                return true;
            }

            Modifier sourceAura = modifier.SourceAura;
            return sourceAura != null &&
                   (IsSameUnit(sourceAura.Caster, unit) || IsSameUnit(sourceAura.Parent, unit));
        }

        private static bool IsSameUnit(IUnit left, IUnit right)
        {
            return left != null && right != null &&
                   (ReferenceEquals(left, right) || left.EntityId == right.EntityId);
        }

        private void NotifyAbilitiesUnitRemoved(IUnit unit)
        {
            List<Ability> snapshot = new List<Ability>();
            foreach (List<Ability> abilities in abilitiesByUnit.Values)
            {
                snapshot.AddRange(abilities);
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                Ability ability = snapshot[i];
                if (ability != null && !IsSameUnit(ability.Owner, unit))
                {
                    ability.OnUnitRemoved(unit);
                }
            }
        }
    }
}

