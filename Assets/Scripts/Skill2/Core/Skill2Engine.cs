using System;
using System.Collections.Generic;
using UnityEngine;

namespace Skill2
{
    public sealed class Skill2Engine
    {
        private readonly Dictionary<string, SkillAbilityDefinition> abilityDefinitions = new Dictionary<string, SkillAbilityDefinition>();
        private readonly Dictionary<string, SkillModifierDefinition> modifierDefinitions = new Dictionary<string, SkillModifierDefinition>();
        private readonly Dictionary<string, Func<SkillAbilityScript>> abilityFactories = new Dictionary<string, Func<SkillAbilityScript>>();
        private readonly Dictionary<string, Func<SkillModifierScript>> modifierFactories = new Dictionary<string, Func<SkillModifierScript>>();
        private readonly Dictionary<int, List<SkillAbility>> abilitiesByUnit = new Dictionary<int, List<SkillAbility>>();
        private readonly List<SkillModifier> modifiers = new List<SkillModifier>();
        private readonly List<SkillProjectile> projectiles = new List<SkillProjectile>();
        private readonly List<SkillThinker> thinkers = new List<SkillThinker>();

        public ISkill2World World { get; private set; }
        public ISkill2Presentation Presentation { get; private set; }
        public IReadOnlyList<SkillModifier> Modifiers => modifiers;
        public IReadOnlyList<SkillProjectile> Projectiles => projectiles;
        public IReadOnlyList<SkillThinker> Thinkers => thinkers;
        public event Action<SkillEvent> EventRaised;

        public void Initialize(ISkill2World world, ISkill2Presentation presentation = null)
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

            foreach (List<SkillAbility> abilities in abilitiesByUnit.Values)
            {
                for (int i = 0; i < abilities.Count; i++)
                {
                    abilities[i].Remove();
                }
            }

            abilitiesByUnit.Clear();
        }

        public void RegisterAbilityDefinition(SkillAbilityDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Name))
            {
                return;
            }

            abilityDefinitions[definition.Name] = definition;
        }

        public void RegisterModifierDefinition(SkillModifierDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Name))
            {
                return;
            }

            modifierDefinitions[definition.Name] = definition;
        }

        public void RegisterAbilityScript(string abilityName, Func<SkillAbilityScript> factory)
        {
            if (string.IsNullOrEmpty(abilityName) || factory == null)
            {
                return;
            }

            abilityFactories[abilityName] = factory;
        }

        public void RegisterModifierScript(string modifierName, Func<SkillModifierScript> factory)
        {
            if (string.IsNullOrEmpty(modifierName) || factory == null)
            {
                return;
            }

            modifierFactories[modifierName] = factory;
        }

        public bool TryGetAbilityDefinition(string abilityName, out SkillAbilityDefinition definition)
        {
            return abilityDefinitions.TryGetValue(abilityName, out definition);
        }

        public bool TryGetModifierDefinition(string modifierName, out SkillModifierDefinition definition)
        {
            return modifierDefinitions.TryGetValue(modifierName, out definition);
        }

        public SkillAbility AddAbility(ISkill2Unit owner, string abilityName, int level = 1, ISkill2ResourceOwner resourceOwner = null)
        {
            if (owner == null || string.IsNullOrEmpty(abilityName))
            {
                return null;
            }

            if (!abilityDefinitions.TryGetValue(abilityName, out SkillAbilityDefinition definition))
            {
                return null;
            }

            SkillAbility existing = FindAbility(owner, abilityName);
            if (existing != null)
            {
                existing.SetLevel(level);
                return existing;
            }

            SkillAbilityScript script = CreateAbilityScript(abilityName, definition);
            SkillAbility ability = new SkillAbility(this, definition, owner, resourceOwner, script, level);

            if (!abilitiesByUnit.TryGetValue(owner.EntityId, out List<SkillAbility> abilities))
            {
                abilities = new List<SkillAbility>();
                abilitiesByUnit.Add(owner.EntityId, abilities);
            }

            abilities.Add(ability);
            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.AbilityAdded,
                Ability = ability,
                Caster = owner
            });
            return ability;
        }

        public bool RemoveAbility(ISkill2Unit owner, string abilityName)
        {
            if (owner == null || string.IsNullOrEmpty(abilityName))
            {
                return false;
            }

            if (!abilitiesByUnit.TryGetValue(owner.EntityId, out List<SkillAbility> abilities))
            {
                return false;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                SkillAbility ability = abilities[i];
                if (ability.Definition.Name != abilityName)
                {
                    continue;
                }

                ability.Remove();
                abilities.RemoveAt(i);
                RaiseEvent(new SkillEvent
                {
                    EventType = SkillEventType.AbilityRemoved,
                    Ability = ability,
                    Caster = owner
                });
                return true;
            }

            return false;
        }

        public SkillAbility FindAbility(ISkill2Unit owner, string abilityName)
        {
            if (owner == null || string.IsNullOrEmpty(abilityName))
            {
                return null;
            }

            if (!abilitiesByUnit.TryGetValue(owner.EntityId, out List<SkillAbility> abilities))
            {
                return null;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                SkillAbility ability = abilities[i];
                if (ability.Definition.Name == abilityName)
                {
                    return ability;
                }
            }

            return null;
        }

        public IReadOnlyList<SkillAbility> GetAbilities(ISkill2Unit owner)
        {
            if (owner == null || !abilitiesByUnit.TryGetValue(owner.EntityId, out List<SkillAbility> abilities))
            {
                return Array.Empty<SkillAbility>();
            }

            return abilities;
        }

        public SkillCastResult IssueOrder(SkillCastOrder order)
        {
            if (order == null || order.Caster == null)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.InvalidTarget);
            }

            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.OrderIssued,
                Engine = this,
                Source = order.Caster,
                Target = order.Target,
                Order = order,
                Position = order.HasTargetPosition ? order.TargetPosition : order.Caster.Position
            });

            SkillAbility ability = FindAbility(order.Caster, order.AbilityName);
            if (ability == null)
            {
                SkillCastResult result = SkillCastResult.Fail(SkillCastFailureReason.MissingAbility);
                RaiseCastFailed(null, order, result);
                return result;
            }

            return ability.IssueOrder(order);
        }

        public SkillCastResult CastAbility(ISkill2Unit caster, string abilityName)
        {
            return IssueOrder(new SkillCastOrder
            {
                Caster = caster,
                AbilityName = abilityName
            });
        }

        public SkillCastResult CastAbilityOnTarget(ISkill2Unit caster, string abilityName, ISkill2Unit target)
        {
            return IssueOrder(new SkillCastOrder
            {
                Caster = caster,
                AbilityName = abilityName,
                Target = target
            });
        }

        public SkillCastResult CastAbilityOnPosition(ISkill2Unit caster, string abilityName, Vector3 position)
        {
            return IssueOrder(new SkillCastOrder
            {
                Caster = caster,
                AbilityName = abilityName,
                TargetPosition = position,
                HasTargetPosition = true
            });
        }

        public void Update(float deltaTime)
        {
            List<SkillAbility> abilitySnapshot = new List<SkillAbility>();
            foreach (List<SkillAbility> abilities in abilitiesByUnit.Values)
            {
                for (int i = 0; i < abilities.Count; i++)
                {
                    abilitySnapshot.Add(abilities[i]);
                }
            }

            for (int i = 0; i < abilitySnapshot.Count; i++)
            {
                SkillAbility ability = abilitySnapshot[i];
                if (ability != null && IsAbilityRegistered(ability))
                {
                    ability.Tick(deltaTime);
                }
            }

            SkillModifier[] modifierSnapshot = modifiers.ToArray();
            for (int i = 0; i < modifierSnapshot.Length; i++)
            {
                SkillModifier modifier = modifierSnapshot[i];
                if (modifier != null && !modifier.IsDestroyed && modifiers.Contains(modifier))
                {
                    modifier.Tick(deltaTime);
                }
            }

            SkillThinker[] thinkerSnapshot = thinkers.ToArray();
            for (int i = 0; i < thinkerSnapshot.Length; i++)
            {
                SkillThinker thinker = thinkerSnapshot[i];
                if (thinker == null || !thinkers.Contains(thinker))
                {
                    continue;
                }

                thinker.Tick(deltaTime);
                if (thinker.IsDestroyed)
                {
                    thinkers.Remove(thinker);
                    RaiseEvent(new SkillEvent
                    {
                        EventType = SkillEventType.ThinkerDestroyed,
                        Thinker = thinker,
                        Ability = thinker.Ability,
                        Caster = thinker.Caster,
                        Position = thinker.Position
                    });
                }
            }

            SkillProjectile[] projectileSnapshot = projectiles.ToArray();
            for (int i = 0; i < projectileSnapshot.Length; i++)
            {
                SkillProjectile projectile = projectileSnapshot[i];
                if (projectile == null || !projectiles.Contains(projectile))
                {
                    continue;
                }

                projectile.Tick(this, deltaTime);

                if (projectile.Destroyed)
                {
                    projectiles.Remove(projectile);
                    RaiseEvent(new SkillEvent
                    {
                        EventType = SkillEventType.ProjectileDestroyed,
                        Projectile = projectile,
                        Ability = projectile.Ability,
                        Caster = projectile.Caster,
                        Position = projectile.Position
                    });
                }
            }
        }

        public SkillModifier AddModifier(ISkill2Unit caster, ISkill2Unit parent, SkillAbility ability, string modifierName, SkillModifierApplyOptions options = null)
        {
            if (parent == null || string.IsNullOrEmpty(modifierName))
            {
                return null;
            }

            if (!modifierDefinitions.TryGetValue(modifierName, out SkillModifierDefinition definition))
            {
                return null;
            }

            if ((definition.Attributes & SkillModifierAttribute.Multiple) == 0)
            {
                SkillModifier existing = FindRefreshableModifier(caster, parent, ability, modifierName);
                if (existing != null)
                {
                    existing.Refresh(options);
                    return existing;
                }
            }

            SkillModifierScript script = CreateModifierScript(modifierName);
            SkillModifier modifier = new SkillModifier(this, definition, caster, parent, ability, script, options ?? new SkillModifierApplyOptions());
            modifiers.Add(modifier);

            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.ModifierAdded,
                Engine = this,
                Source = caster,
                Target = parent,
                Ability = ability,
                Modifier = modifier
            });

            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ModifierAdded,
                Ability = ability,
                Modifier = modifier,
                Caster = caster,
                Target = parent
            });

            return modifier;
        }

        public SkillModifier AddModifier(ISkill2Unit caster, ISkill2Unit parent, string modifierName, float duration = float.NaN)
        {
            SkillModifierApplyOptions options = new SkillModifierApplyOptions();
            options.Duration = duration;
            return AddModifier(caster, parent, null, modifierName, options);
        }

        public bool RemoveModifier(SkillModifier modifier)
        {
            if (modifier == null)
            {
                return false;
            }

            int index = modifiers.IndexOf(modifier);
            if (index < 0)
            {
                return false;
            }

            modifiers.RemoveAt(index);
            modifier.Destroy();

            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.ModifierRemoved,
                Engine = this,
                Source = modifier.Caster,
                Target = modifier.Parent,
                Ability = modifier.Ability,
                Modifier = modifier
            });

            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ModifierRemoved,
                Ability = modifier.Ability,
                Modifier = modifier,
                Caster = modifier.Caster,
                Target = modifier.Parent
            });

            return true;
        }

        public int Purge(ISkill2Unit unit, bool removePositiveBuffs, bool removeDebuffs, bool onlyPurgable)
        {
            if (unit == null)
            {
                return 0;
            }

            int removed = 0;
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                SkillModifier modifier = modifiers[i];
                if (modifier.Parent != unit)
                {
                    continue;
                }

                bool debuff = modifier.Script.IsDebuff();
                if (debuff && !removeDebuffs)
                {
                    continue;
                }

                if (!debuff && !removePositiveBuffs)
                {
                    continue;
                }

                if (onlyPurgable && !modifier.Script.IsPurgable())
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

        public bool HasState(ISkill2Unit unit, SkillUnitState state)
        {
            if (unit == null || state == SkillUnitState.None)
            {
                return false;
            }

            if (state == SkillUnitState.Invulnerable && unit.IsInvulnerable)
            {
                return true;
            }

            if (state == SkillUnitState.MagicImmune && unit.IsMagicImmune)
            {
                return true;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifier modifier = modifiers[i];
                if (modifier.Parent == unit && modifier.CheckState(state))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetModifierProperty(ISkill2Unit unit, SkillModifierProperty property, SkillModifierPropertyContext context)
        {
            if (unit == null || property == SkillModifierProperty.None)
            {
                return 0f;
            }

            SkillModifierPropertyContext propertyContext = context ?? new SkillModifierPropertyContext();
            propertyContext.Engine = this;
            propertyContext.Unit = unit;

            float total = 0f;
            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifier modifier = modifiers[i];
                if (modifier.Parent != unit)
                {
                    continue;
                }

                total += modifier.GetProperty(property, propertyContext);
            }

            return total;
        }

        public void FindUnits(Vector3 center, float radius, SkillTargetQuery query, IList<ISkill2Unit> results)
        {
            if (results == null)
            {
                return;
            }

            if (World == null)
            {
                return;
            }

            List<ISkill2Unit> candidates = new List<ISkill2Unit>();
            World.FindUnits(center, radius, query, candidates);

            for (int i = 0; i < candidates.Count; i++)
            {
                ISkill2Unit unit = candidates[i];
                if (unit == null)
                {
                    continue;
                }

                if (query != null && !query.IsValid(unit))
                {
                    continue;
                }

                if (!results.Contains(unit))
                {
                    results.Add(unit);
                }
            }
        }

        public SkillDamageResult ApplyDamage(SkillDamageInfo damageInfo)
        {
            SkillDamageResult result = new SkillDamageResult();
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

            if (damageInfo.Victim.IsInvulnerable && (damageInfo.Flags & SkillDamageFlags.IgnoreInvulnerable) == 0)
            {
                result.Blocked = true;
                result.BlockReason = "Victim is invulnerable.";
                return result;
            }

            if (damageInfo.DamageType == SkillDamageType.Magical && damageInfo.Victim.IsMagicImmune && (damageInfo.Flags & SkillDamageFlags.PiercesSpellImmunity) == 0)
            {
                result.Blocked = true;
                result.BlockReason = "Victim is magic immune.";
                return result;
            }

            float amount = Mathf.Max(0f, damageInfo.Amount);
            if ((damageInfo.Flags & SkillDamageFlags.NoDamageMultiplier) == 0)
            {
                SkillModifierPropertyContext propertyContext = new SkillModifierPropertyContext
                {
                    Engine = this,
                    Ability = damageInfo.Ability,
                    DamageInfo = damageInfo,
                    DamageResult = result
                };

                if (damageInfo.Attacker != null)
                {
                    float outgoing = GetModifierProperty(damageInfo.Attacker, SkillModifierProperty.DamageOutgoingPercent, propertyContext);
                    amount *= Mathf.Max(0f, 1f + outgoing / 100f);

                    if (damageInfo.DamageType == SkillDamageType.Magical && (damageInfo.Flags & SkillDamageFlags.NoSpellAmplification) == 0)
                    {
                        float spellAmp = GetModifierProperty(damageInfo.Attacker, SkillModifierProperty.SpellAmplifyPercent, propertyContext);
                        amount *= Mathf.Max(0f, 1f + spellAmp / 100f);
                    }
                }

                float incoming = GetModifierProperty(damageInfo.Victim, SkillModifierProperty.DamageIncomingPercent, propertyContext);
                amount *= Mathf.Max(0f, 1f + incoming / 100f);
            }

            result.FinalAmount = amount;

            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.DamageCalculated,
                Engine = this,
                Source = damageInfo.Attacker,
                Target = damageInfo.Victim,
                Ability = damageInfo.Ability,
                DamageInfo = damageInfo,
                DamageResult = result
            });

            damageInfo.Victim.ApplyDamage(result);

            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.DamageTaken,
                Engine = this,
                Source = damageInfo.Attacker,
                Target = damageInfo.Victim,
                Ability = damageInfo.Ability,
                DamageInfo = damageInfo,
                DamageResult = result
            });

            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.DamageDealt,
                Engine = this,
                Source = damageInfo.Attacker,
                Target = damageInfo.Victim,
                Ability = damageInfo.Ability,
                DamageInfo = damageInfo,
                DamageResult = result
            });

            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.DamageApplied,
                Ability = damageInfo.Ability,
                Caster = damageInfo.Attacker,
                Target = damageInfo.Victim,
                Value = result.FinalAmount
            });

            return result;
        }

        public void Heal(SkillHealInfo healInfo)
        {
            if (healInfo == null || healInfo.Target == null || !healInfo.Target.IsAlive)
            {
                return;
            }

            healInfo.Target.Heal(healInfo);
            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.Healed,
                Engine = this,
                Source = healInfo.Source,
                Target = healInfo.Target,
                Ability = healInfo.Ability,
                HealInfo = healInfo,
                Value = healInfo.Amount
            });

            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.HealApplied,
                Ability = healInfo.Ability,
                Caster = healInfo.Source,
                Target = healInfo.Target,
                Value = healInfo.Amount
            });
        }

        public SkillProjectile CreateProjectile(SkillProjectileRequest request)
        {
            if (request == null || request.Definition == null)
            {
                return null;
            }

            SkillProjectile projectile = new SkillProjectile(request);
            projectiles.Add(projectile);

            if (!string.IsNullOrEmpty(request.Definition.EffectName) && Presentation != null)
            {
                Presentation.PlayEffect(request.Definition.EffectName, request.Origin);
            }

            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ProjectileCreated,
                Projectile = projectile,
                Ability = request.Ability,
                Caster = request.Caster,
                Target = request.Target,
                Position = request.Origin
            });
            return projectile;
        }

        public SkillProjectile CreateTrackingProjectile(SkillAbility ability, ISkill2Unit caster, ISkill2Unit target, SkillProjectileDefinition definition)
        {
            if (caster == null || target == null || definition == null)
            {
                return null;
            }

            return CreateProjectile(new SkillProjectileRequest
            {
                Definition = definition,
                Ability = ability,
                Caster = caster,
                Source = caster,
                Target = target,
                Origin = caster.Position,
                Tracking = true
            });
        }

        public SkillProjectile CreateLinearProjectile(SkillAbility ability, ISkill2Unit caster, Vector3 origin, Vector3 direction, SkillProjectileDefinition definition)
        {
            if (caster == null || definition == null)
            {
                return null;
            }

            return CreateProjectile(new SkillProjectileRequest
            {
                Definition = definition,
                Ability = ability,
                Caster = caster,
                Source = caster,
                Origin = origin,
                Direction = direction,
                Tracking = false
            });
        }

        public SkillThinker CreateThinker(SkillThinkerRequest request)
        {
            if (request == null)
            {
                return null;
            }

            SkillThinker thinker = new SkillThinker(this, request);
            thinkers.Add(thinker);
            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ThinkerCreated,
                Thinker = thinker,
                Ability = thinker.Ability,
                Caster = thinker.Caster,
                Position = thinker.Position
            });
            return thinker;
        }

        public bool RemoveThinker(SkillThinker thinker)
        {
            if (thinker == null)
            {
                return false;
            }

            if (!thinkers.Remove(thinker))
            {
                return false;
            }

            thinker.Destroy();
            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ThinkerDestroyed,
                Thinker = thinker,
                Ability = thinker.Ability,
                Caster = thinker.Caster,
                Position = thinker.Position
            });
            return true;
        }

        internal void OnProjectileHit(SkillProjectile projectile, ISkill2Unit target, Vector3 position)
        {
            DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.ProjectileHit,
                Engine = this,
                Source = projectile.Caster,
                Target = target,
                Ability = projectile.Ability,
                Projectile = projectile,
                Position = position
            });

            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ProjectileHit,
                Ability = projectile.Ability,
                Projectile = projectile,
                Caster = projectile.Caster,
                Target = target,
                Position = position
            });
        }

        internal void RefreshAura(SkillModifier aura)
        {
            if (aura == null || aura.Definition == null || World == null)
            {
                return;
            }

            SkillModifierDefinition definition = aura.Definition;
            SkillTargetQuery query = new SkillTargetQuery
            {
                Caster = aura.Parent,
                Team = definition.AuraTargetTeam,
                Types = definition.AuraTargetType,
                Flags = definition.AuraTargetFlags
            };

            List<ISkill2Unit> units = new List<ISkill2Unit>();
            FindUnits(aura.Parent.Position, definition.AuraRadius, query, units);

            for (int i = 0; i < units.Count; i++)
            {
                ISkill2Unit unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                SkillModifierApplyOptions options = new SkillModifierApplyOptions
                {
                    Duration = Mathf.Max(0.1f, definition.AuraDuration),
                    IsAura = true,
                    SourceModifier = aura
                };

                AddModifier(aura.Parent, unit, aura.Ability, definition.AuraModifierName, options);
            }
        }

        internal void DispatchModifierEvent(SkillModifierEvent modifierEvent)
        {
            if (modifierEvent == null)
            {
                return;
            }

            SkillModifier[] snapshot = modifiers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                SkillModifier modifier = snapshot[i];
                if (modifier != null && !modifier.IsDestroyed)
                {
                    modifier.Script.OnEvent(modifierEvent);
                }
            }
        }

        internal void RaiseCastFailed(SkillAbility ability, SkillCastOrder order, SkillCastResult result)
        {
            RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.CastFailed,
                Ability = ability,
                Caster = order != null ? order.Caster : null,
                Target = order != null ? order.Target : null,
                Position = order != null && order.HasTargetPosition ? order.TargetPosition : Vector3.zero,
                FailureReason = result != null ? result.FailureReason : SkillCastFailureReason.None,
                Message = result != null ? result.Message : null
            });
        }

        internal void RaiseEvent(SkillEvent skillEvent)
        {
            EventRaised?.Invoke(skillEvent);
        }

        private SkillAbilityScript CreateAbilityScript(string abilityName, SkillAbilityDefinition definition)
        {
            if (abilityFactories.TryGetValue(abilityName, out Func<SkillAbilityScript> factory))
            {
                SkillAbilityScript script = factory();
                if (script != null)
                {
                    return script;
                }
            }

            if (definition != null && definition.Actions.Count > 0)
            {
                return new ConfiguredSkillAbilityScript();
            }

            return new DefaultSkillAbilityScript();
        }

        private SkillModifierScript CreateModifierScript(string modifierName)
        {
            if (modifierFactories.TryGetValue(modifierName, out Func<SkillModifierScript> factory))
            {
                SkillModifierScript script = factory();
                if (script != null)
                {
                    return script;
                }
            }

            return new DefaultSkillModifierScript();
        }

        private SkillModifier FindRefreshableModifier(ISkill2Unit caster, ISkill2Unit parent, SkillAbility ability, string modifierName)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                SkillModifier modifier = modifiers[i];
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

        private bool IsAbilityRegistered(SkillAbility ability)
        {
            if (ability == null || ability.Owner == null)
            {
                return false;
            }

            if (!abilitiesByUnit.TryGetValue(ability.Owner.EntityId, out List<SkillAbility> abilities))
            {
                return false;
            }

            return abilities.Contains(ability);
        }
    }
}
