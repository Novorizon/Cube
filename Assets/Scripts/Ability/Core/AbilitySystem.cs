using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Ability
{
    public sealed class AbilitySystem
    {
        private readonly Dictionary<string, AbilityDefinition> abilityDefinitions = new Dictionary<string, AbilityDefinition>();
        private readonly Dictionary<string, ModifierDefinition> modifierDefinitions = new Dictionary<string, ModifierDefinition>();
        private readonly Dictionary<string, Func<AbilityScript>> abilityFactories = new Dictionary<string, Func<AbilityScript>>();
        private readonly Dictionary<string, Func<ModifierScript>> modifierFactories = new Dictionary<string, Func<ModifierScript>>();
        private readonly Dictionary<int, List<Ability>> abilitiesByUnit = new Dictionary<int, List<Ability>>();
        private readonly List<Modifier> modifiers = new List<Modifier>();
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly List<Thinker> thinkers = new List<Thinker>();

        public IWorld World { get; private set; }
        public IPresentation Presentation { get; private set; }
        public IReadOnlyList<Modifier> Modifiers => modifiers;
        public IReadOnlyList<Projectile> Projectiles => projectiles;
        public IReadOnlyList<Thinker> Thinkers => thinkers;
        public event Action<RuntimeEvent> EventRaised;

        public void Initialize(IWorld world, IPresentation presentation = null)
        {
            World = world;
            Presentation = presentation;
            ClearRuntime();
        }

        public void ClearRuntime()
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                modifiers[i].Destroy();
            }

            modifiers.Clear();
            projectiles.Clear();

            for (int i = thinkers.Count - 1; i >= 0; i--)
            {
                thinkers[i].Destroy();
            }

            thinkers.Clear();

            foreach (List<Ability> abilities in abilitiesByUnit.Values)
            {
                for (int i = 0; i < abilities.Count; i++)
                {
                    abilities[i].Remove();
                }
            }

            abilitiesByUnit.Clear();
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
            if (owner == null || string.IsNullOrEmpty(abilityName) || !abilityDefinitions.TryGetValue(abilityName, out AbilityDefinition definition))
            {
                return null;
            }

            Ability existing = FindAbility(owner, abilityName);
            if (existing != null)
            {
                existing.SetLevel(level);
                return existing;
            }

            Ability ability = new Ability(this, definition, owner, resourceOwner, CreateAbilityScript(abilityName, definition), level);
            if (!abilitiesByUnit.TryGetValue(owner.EntityId, out List<Ability> abilities))
            {
                abilities = new List<Ability>();
                abilitiesByUnit.Add(owner.EntityId, abilities);
            }

            abilities.Add(ability);
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.AbilityAdded, Ability = ability, Caster = owner });
            return ability;
        }

        public bool RemoveAbility(IUnit owner, string abilityName)
        {
            if (owner == null || string.IsNullOrEmpty(abilityName) || !abilitiesByUnit.TryGetValue(owner.EntityId, out List<Ability> abilities))
            {
                return false;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                Ability ability = abilities[i];
                if (ability.Definition.Name != abilityName)
                {
                    continue;
                }

                ability.Remove();
                abilities.RemoveAt(i);
                RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.AbilityRemoved, Ability = ability, Caster = owner });
                return true;
            }

            return false;
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
            if (order == null || order.Caster == null)
            {
                return CastResult.Fail(CastFailureReason.InvalidTarget);
            }

            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.OrderIssued, Engine = this, Source = order.Caster, Target = order.Target, Order = order, Position = order.HasTargetPosition ? order.TargetPosition : order.Caster.Position });

            Ability ability = FindAbility(order.Caster, order.AbilityName);
            if (ability == null)
            {
                CastResult result = CastResult.Fail(CastFailureReason.MissingAbility);
                RaiseCastFailed(null, order, result);
                return result;
            }

            return ability.IssueOrder(order);
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
            List<Ability> abilitySnapshot = new List<Ability>();
            foreach (List<Ability> abilities in abilitiesByUnit.Values)
            {
                abilitySnapshot.AddRange(abilities);
            }

            for (int i = 0; i < abilitySnapshot.Count; i++)
            {
                if (IsAbilityRegistered(abilitySnapshot[i]))
                {
                    abilitySnapshot[i].Tick(deltaTime);
                }
            }

            Modifier[] modifierSnapshot = modifiers.ToArray();
            for (int i = 0; i < modifierSnapshot.Length; i++)
            {
                Modifier modifier = modifierSnapshot[i];
                if (modifier != null && !modifier.IsDestroyed && modifiers.Contains(modifier))
                {
                    modifier.Tick(deltaTime);
                }
            }

            Thinker[] thinkerSnapshot = thinkers.ToArray();
            for (int i = 0; i < thinkerSnapshot.Length; i++)
            {
                Thinker thinker = thinkerSnapshot[i];
                if (thinker == null || !thinkers.Contains(thinker))
                {
                    continue;
                }

                thinker.Tick(deltaTime);
                if (thinker.IsDestroyed)
                {
                    thinkers.Remove(thinker);
                    RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ThinkerDestroyed, Thinker = thinker, Ability = thinker.Ability, Caster = thinker.Caster, Position = thinker.Position });
                }
            }

            Projectile[] projectileSnapshot = projectiles.ToArray();
            for (int i = 0; i < projectileSnapshot.Length; i++)
            {
                Projectile projectile = projectileSnapshot[i];
                if (projectile == null || !projectiles.Contains(projectile))
                {
                    continue;
                }

                projectile.Tick(this, deltaTime);
                if (projectile.Destroyed)
                {
                    projectiles.Remove(projectile);
                    RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ProjectileDestroyed, Projectile = projectile, Ability = projectile.Ability, Caster = projectile.Caster, Position = projectile.Position });
                }
            }
        }

        public Modifier AddModifier(IUnit caster, IUnit parent, Ability ability, string modifierName, ModifierApplyOptions options = null)
        {
            if (parent == null || string.IsNullOrEmpty(modifierName) || !modifierDefinitions.TryGetValue(modifierName, out ModifierDefinition definition))
            {
                return null;
            }

            if ((definition.Attributes & ModifierAttribute.Multiple) == 0)
            {
                Modifier existing = FindRefreshableModifier(caster, parent, ability, modifierName);
                if (existing != null)
                {
                    existing.Refresh(options);
                    return existing;
                }
            }

            Modifier modifier = new Modifier(this, definition, caster, parent, ability, CreateModifierScript(modifierName, definition), options ?? new ModifierApplyOptions());
            modifiers.Add(modifier);
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.ModifierAdded, Engine = this, Source = caster, Target = parent, Ability = ability, Modifier = modifier });
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ModifierAdded, Ability = ability, Modifier = modifier, Caster = caster, Target = parent });
            return modifier;
        }

        public Modifier AddModifier(IUnit caster, IUnit parent, string modifierName, float duration = float.NaN)
        {
            return AddModifier(caster, parent, null, modifierName, new ModifierApplyOptions { Duration = duration });
        }

        public bool RemoveModifier(Modifier modifier)
        {
            if (modifier == null || !modifiers.Remove(modifier))
            {
                return false;
            }

            modifier.Destroy();
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.ModifierRemoved, Engine = this, Source = modifier.Caster, Target = modifier.Parent, Ability = modifier.Ability, Modifier = modifier });
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ModifierRemoved, Ability = modifier.Ability, Modifier = modifier, Caster = modifier.Caster, Target = modifier.Parent });
            return true;
        }

        public int Purge(IUnit unit, bool removePositiveBuffs, bool removeDebuffs, bool onlyPurgable)
        {
            if (unit == null)
            {
                return 0;
            }

            int removed = 0;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                Modifier modifier = modifiers[i];
                if (modifier.Parent != unit)
                {
                    continue;
                }

                bool debuff = modifier.Script.IsDebuff();
                if ((debuff && !removeDebuffs) || (!debuff && !removePositiveBuffs) || (onlyPurgable && !modifier.Script.IsPurgable()))
                {
                    continue;
                }

                if (RemoveModifier(modifier))
                {
                    removed++;
                }
            }

            return removed;
        }

        public bool HasState(IUnit unit, UnitState state)
        {
            if (unit == null || state == UnitState.None)
            {
                return false;
            }

            if (state == UnitState.Invulnerable && unit.IsInvulnerable)
            {
                return true;
            }

            if (state == UnitState.MagicImmune && unit.IsMagicImmune)
            {
                return true;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                Modifier modifier = modifiers[i];
                if (modifier.Parent == unit && modifier.CheckState(state))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetModifierProperty(IUnit unit, ModifierProperty property, ModifierPropertyContext context)
        {
            if (unit == null || property == ModifierProperty.None)
            {
                return 0f;
            }

            ModifierPropertyContext propertyContext = context ?? new ModifierPropertyContext();
            propertyContext.Engine = this;
            propertyContext.Unit = unit;

            float total = 0f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                Modifier modifier = modifiers[i];
                if (modifier.Parent == unit)
                {
                    total += modifier.GetProperty(property, propertyContext);
                }
            }

            return total;
        }

        public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
        {
            if (results == null || World == null)
            {
                return;
            }

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

        public DamageResult ApplyDamage(DamageInfo damageInfo)
        {
            DamageResult result = new DamageResult();
            if (damageInfo == null)
            {
                result.Blocked = true;
                result.BlockReason = "Missing damage info.";
                return result;
            }

            result.Attacker = damageInfo.Attacker;
            result.Victim = damageInfo.Victim;
            result.Ability = damageInfo.Ability;
            result.OriginalAmount = damageInfo.Amount;
            result.DamageType = damageInfo.DamageType;
            result.Flags = damageInfo.Flags;

            if (damageInfo.Victim == null || !damageInfo.Victim.IsAlive)
            {
                result.Blocked = true;
                result.BlockReason = "Invalid victim.";
                return result;
            }

            if (damageInfo.Victim.IsInvulnerable && (damageInfo.Flags & DamageFlags.IgnoreInvulnerable) == 0)
            {
                result.Blocked = true;
                result.BlockReason = "Victim is invulnerable.";
                return result;
            }

            if (damageInfo.DamageType == DamageType.Magical && damageInfo.Victim.IsMagicImmune && (damageInfo.Flags & DamageFlags.PiercesSpellImmunity) == 0)
            {
                result.Blocked = true;
                result.BlockReason = "Victim is magic immune.";
                return result;
            }

            float amount = Mathf.Max(0f, damageInfo.Amount);
            if ((damageInfo.Flags & DamageFlags.NoDamageMultiplier) == 0)
            {
                ModifierPropertyContext propertyContext = new ModifierPropertyContext { Engine = this, Ability = damageInfo.Ability, DamageInfo = damageInfo, DamageResult = result };
                if (damageInfo.Attacker != null)
                {
                    amount *= Mathf.Max(0f, 1f + GetModifierProperty(damageInfo.Attacker, ModifierProperty.DamageOutgoingPercent, propertyContext) / 100f);
                    if (damageInfo.DamageType == DamageType.Magical && (damageInfo.Flags & DamageFlags.NoSpellAmplification) == 0)
                    {
                        amount *= Mathf.Max(0f, 1f + GetModifierProperty(damageInfo.Attacker, ModifierProperty.SpellAmplifyPercent, propertyContext) / 100f);
                    }
                }

                amount *= Mathf.Max(0f, 1f + GetModifierProperty(damageInfo.Victim, ModifierProperty.DamageIncomingPercent, propertyContext) / 100f);
            }

            result.FinalAmount = amount;
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.DamageCalculated, Engine = this, Source = damageInfo.Attacker, Target = damageInfo.Victim, Ability = damageInfo.Ability, DamageInfo = damageInfo, DamageResult = result });
            damageInfo.Victim.ApplyDamage(result);
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.DamageTaken, Engine = this, Source = damageInfo.Attacker, Target = damageInfo.Victim, Ability = damageInfo.Ability, DamageInfo = damageInfo, DamageResult = result });
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.DamageDealt, Engine = this, Source = damageInfo.Attacker, Target = damageInfo.Victim, Ability = damageInfo.Ability, DamageInfo = damageInfo, DamageResult = result });
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.DamageApplied, Ability = damageInfo.Ability, Caster = damageInfo.Attacker, Target = damageInfo.Victim, Value = result.FinalAmount });
            return result;
        }

        public void Heal(HealInfo healInfo)
        {
            if (healInfo == null || healInfo.Target == null || !healInfo.Target.IsAlive)
            {
                return;
            }

            healInfo.Target.Heal(healInfo);
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.Healed, Engine = this, Source = healInfo.Source, Target = healInfo.Target, Ability = healInfo.Ability, HealInfo = healInfo, Value = healInfo.Amount });
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.HealApplied, Ability = healInfo.Ability, Caster = healInfo.Source, Target = healInfo.Target, Value = healInfo.Amount });
        }

        public Projectile CreateProjectile(ProjectileRequest request)
        {
            if (request == null || request.Definition == null)
            {
                return null;
            }

            Projectile projectile = new Projectile(request);
            projectiles.Add(projectile);
            if (!string.IsNullOrEmpty(request.Definition.EffectName) && Presentation != null)
            {
                Presentation.PlayEffect(request.Definition.EffectName, request.Origin);
            }

            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ProjectileCreated, Projectile = projectile, Ability = request.Ability, Caster = request.Caster, Target = request.Target, Position = request.Origin });
            return projectile;
        }

        public Projectile CreateTrackingProjectile(Ability ability, IUnit caster, IUnit target, ProjectileDefinition definition)
        {
            return caster == null || target == null || definition == null ? null : CreateProjectile(new ProjectileRequest { Definition = definition, Ability = ability, Caster = caster, Source = caster, Target = target, Origin = caster.Position, Tracking = true });
        }

        public Projectile CreateLinearProjectile(Ability ability, IUnit caster, Vector3 origin, Vector3 direction, ProjectileDefinition definition)
        {
            return caster == null || definition == null ? null : CreateProjectile(new ProjectileRequest { Definition = definition, Ability = ability, Caster = caster, Source = caster, Origin = origin, Direction = direction, Tracking = false });
        }

        public Thinker CreateThinker(ThinkerRequest request)
        {
            if (request == null)
            {
                return null;
            }

            Thinker thinker = new Thinker(this, request);
            thinkers.Add(thinker);
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ThinkerCreated, Thinker = thinker, Ability = thinker.Ability, Caster = thinker.Caster, Position = thinker.Position });
            return thinker;
        }

        public bool RemoveThinker(Thinker thinker)
        {
            if (thinker == null || !thinkers.Remove(thinker))
            {
                return false;
            }

            thinker.Destroy();
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ThinkerDestroyed, Thinker = thinker, Ability = thinker.Ability, Caster = thinker.Caster, Position = thinker.Position });
            return true;
        }

        internal void OnProjectileHit(Projectile projectile, IUnit target, Vector3 position)
        {
            DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.ProjectileHit, Engine = this, Source = projectile.Caster, Target = target, Ability = projectile.Ability, Projectile = projectile, Position = position });
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ProjectileHit, Ability = projectile.Ability, Projectile = projectile, Caster = projectile.Caster, Target = target, Position = position });
        }

        internal void RefreshAura(Modifier aura)
        {
            if (aura == null || aura.Definition == null || string.IsNullOrEmpty(aura.Definition.AuraModifierName))
            {
                return;
            }

            ModifierDefinition definition = aura.Definition;
            TargetQuery query = new TargetQuery { Caster = aura.Parent, Team = definition.AuraTargetTeam, Types = definition.AuraTargetType, Flags = definition.AuraTargetFlags };
            List<IUnit> units = new List<IUnit>();
            FindUnits(aura.Parent.Position, definition.AuraRadius, query, units);
            for (int i = 0; i < units.Count; i++)
            {
                AddModifier(aura.Parent, units[i], aura.Ability, definition.AuraModifierName, new ModifierApplyOptions { Duration = Mathf.Max(0.1f, definition.AuraDuration), IsAura = true, SourceModifier = aura });
            }
        }

        internal void DispatchModifierEvent(ModifierEvent modifierEvent)
        {
            if (modifierEvent == null)
            {
                return;
            }

            Modifier[] snapshot = modifiers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null && !snapshot[i].IsDestroyed)
                {
                    snapshot[i].Script.OnEvent(modifierEvent);
                }
            }
        }

        internal void RaiseCastFailed(Ability ability, CastOrder order, CastResult result)
        {
            RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.CastFailed, Ability = ability, Caster = order != null ? order.Caster : null, Target = order != null ? order.Target : null, Position = order != null && order.HasTargetPosition ? order.TargetPosition : Vector3.zero, FailureReason = result != null ? result.FailureReason : CastFailureReason.None, Message = result != null ? result.Message : null });
        }

        internal void RaiseEvent(RuntimeEvent runtimeEvent)
        {
            EventRaised?.Invoke(runtimeEvent);
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

        private Modifier FindRefreshableModifier(IUnit caster, IUnit parent, Ability ability, string modifierName)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                Modifier modifier = modifiers[i];
                if (modifier.Parent != parent || modifier.Name != modifierName)
                {
                    continue;
                }

                bool sameCaster = caster == null || modifier.Caster == null || modifier.Caster.EntityId == caster.EntityId;
                bool sameAbility = ability == null || modifier.Ability == null || modifier.Ability == ability;
                if (sameCaster && sameAbility)
                {
                    return modifier;
                }
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
    }
}
