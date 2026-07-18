using System;
using System.Collections.Generic;

namespace Game.Ability.Configuration
{
    public enum AbilityDefinitionSourceType
    {
        Unknown = 0,
        Excel = 1,
        Json = 2,
        Code = 3
    }

    [Serializable]
    public sealed class AbilityDefinitionOrigin
    {
        public AbilityDefinitionSourceType SourceType;
        public string ProviderName;
        public string SourcePath;
        public string StableId;
        public string Namespace;

        public AbilityConfigSource ToConfigSource()
        {
            return new AbilityConfigSource
            {
                SourceType = SourceType.ToString(),
                Path = SourcePath
            };
        }

        public override string ToString()
        {
            string id = string.IsNullOrEmpty(StableId) ? string.Empty : "#" + StableId;
            return SourceType + ":" + SourcePath + id;
        }
    }

    public sealed class AbilityDefinitionRegistration
    {
        public AbilityDefinition Definition;
        public AbilityDefinitionOrigin Origin;
    }

    public sealed class ModifierDefinitionRegistration
    {
        public ModifierDefinition Definition;
        public AbilityDefinitionOrigin Origin;
        public bool IsPrivate;
    }

    public sealed class ProjectileDefinitionRegistration
    {
        public ProjectileDefinition Definition;
        public AbilityDefinitionOrigin Origin;
        public bool IsPrivate;
    }

    public sealed class AbilityDefinitionBundle
    {
        public readonly List<AbilityDefinitionRegistration> Abilities = new List<AbilityDefinitionRegistration>();
        public readonly List<ModifierDefinitionRegistration> Modifiers = new List<ModifierDefinitionRegistration>();
        public readonly List<ProjectileDefinitionRegistration> Projectiles = new List<ProjectileDefinitionRegistration>();
        public readonly AbilityValidationReport Validation = new AbilityValidationReport();
    }

    public interface IAbilityDefinitionProvider
    {
        string ProviderName { get; }
        AbilityDefinitionSourceType SourceType { get; }
        AbilityDefinitionBundle Load();
    }

    /// <summary>
    /// Merges definitions from providers without order-based overwrite. Duplicate stable IDs or
    /// names make the registry invalid, and no definitions are applied to AbilitySystem.
    /// </summary>
    public sealed class AbilityDefinitionRegistry
    {
        private readonly Dictionary<string, AbilityDefinitionRegistration> abilitiesByName =
            new Dictionary<string, AbilityDefinitionRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, AbilityDefinitionRegistration> abilitiesByStableId =
            new Dictionary<string, AbilityDefinitionRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, ModifierDefinitionRegistration> modifiersByName =
            new Dictionary<string, ModifierDefinitionRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, ModifierDefinitionRegistration> modifiersByStableId =
            new Dictionary<string, ModifierDefinitionRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProjectileDefinitionRegistration> projectilesByName =
            new Dictionary<string, ProjectileDefinitionRegistration>(StringComparer.Ordinal);
        private readonly Dictionary<string, ProjectileDefinitionRegistration> projectilesByStableId =
            new Dictionary<string, ProjectileDefinitionRegistration>(StringComparer.Ordinal);

        public AbilityValidationReport Validation { get; } = new AbilityValidationReport();
        public bool IsValid => Validation.IsValid;
        public IReadOnlyDictionary<string, AbilityDefinitionRegistration> Abilities => abilitiesByName;
        public IReadOnlyDictionary<string, ModifierDefinitionRegistration> Modifiers => modifiersByName;
        public IReadOnlyDictionary<string, ProjectileDefinitionRegistration> Projectiles => projectilesByName;

        public void LoadProviders(IEnumerable<IAbilityDefinitionProvider> providers)
        {
            if (providers == null)
            {
                Validation.Add(AbilityValidationSeverity.Error, "ABILITYPROVIDER001", "Provider collection is null.");
                return;
            }

            foreach (IAbilityDefinitionProvider provider in providers)
            {
                LoadProvider(provider);
            }

            ValidateMergedDefinitions();
        }

        public void LoadProvider(IAbilityDefinitionProvider provider)
        {
            if (provider == null)
            {
                Validation.Add(AbilityValidationSeverity.Error, "ABILITYPROVIDER002", "Ability definition provider is null.");
                return;
            }

            AbilityDefinitionBundle bundle;
            try
            {
                bundle = provider.Load();
            }
            catch (Exception exception)
            {
                Validation.Add(
                    AbilityValidationSeverity.Error,
                    "ABILITYPROVIDER003",
                    "Provider " + provider.ProviderName + " failed: " + exception.Message + ".",
                    new AbilityConfigSource { SourceType = provider.SourceType.ToString() });
                return;
            }

            if (bundle == null)
            {
                Validation.Add(AbilityValidationSeverity.Error, "ABILITYPROVIDER004", "Provider " + provider.ProviderName + " returned no bundle.");
                return;
            }

            Validation.Merge(bundle.Validation);
            for (int i = 0; i < bundle.Abilities.Count; i++) AddAbility(bundle.Abilities[i]);
            for (int i = 0; i < bundle.Modifiers.Count; i++) AddModifier(bundle.Modifiers[i]);
            for (int i = 0; i < bundle.Projectiles.Count; i++) AddProjectile(bundle.Projectiles[i]);
        }

        public bool ApplyTo(AbilitySystem engine)
        {
            if (engine == null)
            {
                Validation.Add(AbilityValidationSeverity.Error, "ABILITYPROVIDER005", "Cannot apply definitions to a null AbilitySystem.");
                return false;
            }

            if (!IsValid)
            {
                return false;
            }

            foreach (AbilityDefinitionRegistration registration in abilitiesByName.Values)
            {
                engine.RegisterAbilityDefinition(registration.Definition);
            }

            foreach (ModifierDefinitionRegistration registration in modifiersByName.Values)
            {
                engine.RegisterModifierDefinition(registration.Definition);
            }

            return true;
        }

        public bool TryGetAbilityOrigin(string name, out AbilityDefinitionOrigin origin)
        {
            if (abilitiesByName.TryGetValue(name, out AbilityDefinitionRegistration registration))
            {
                origin = registration.Origin;
                return true;
            }

            origin = null;
            return false;
        }

        private void AddAbility(AbilityDefinitionRegistration registration)
        {
            string name = registration?.Definition?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                AddInvalidRegistration("ability", registration?.Origin);
                return;
            }

            if (abilitiesByName.TryGetValue(name, out AbilityDefinitionRegistration existing))
            {
                AddCollision("ability name", name, existing.Origin, registration.Origin);
                return;
            }

            if (!TryAddStableId("ability", registration.Origin, registration, abilitiesByStableId))
            {
                return;
            }

            abilitiesByName.Add(name, registration);
        }

        private void AddModifier(ModifierDefinitionRegistration registration)
        {
            string name = registration?.Definition?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                AddInvalidRegistration("modifier", registration?.Origin);
                return;
            }

            if (modifiersByName.TryGetValue(name, out ModifierDefinitionRegistration existing))
            {
                AddCollision("modifier name", name, existing.Origin, registration.Origin);
                return;
            }

            if (!TryAddStableId("modifier", registration.Origin, registration, modifiersByStableId))
            {
                return;
            }

            ValidatePrivateName("modifier", name, registration.IsPrivate, registration.Origin);
            modifiersByName.Add(name, registration);
        }

        private void AddProjectile(ProjectileDefinitionRegistration registration)
        {
            string name = registration?.Definition?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                AddInvalidRegistration("projectile", registration?.Origin);
                return;
            }

            if (projectilesByName.TryGetValue(name, out ProjectileDefinitionRegistration existing))
            {
                AddCollision("projectile name", name, existing.Origin, registration.Origin);
                return;
            }

            if (!TryAddStableId("projectile", registration.Origin, registration, projectilesByStableId))
            {
                return;
            }

            ValidatePrivateName("projectile", name, registration.IsPrivate, registration.Origin);
            projectilesByName.Add(name, registration);
        }

        private bool TryAddStableId<T>(string kind, AbilityDefinitionOrigin origin, T registration, IDictionary<string, T> index)
        {
            string stableId = origin?.StableId;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                return true;
            }

            if (index.ContainsKey(stableId))
            {
                AbilityDefinitionOrigin existingOrigin = GetOrigin(index[stableId]);
                AddCollision(kind + " stable ID", stableId, existingOrigin, origin);
                return false;
            }

            index.Add(stableId, registration);
            return true;
        }

        private void ValidateMergedDefinitions()
        {
            foreach (AbilityDefinitionRegistration registration in abilitiesByName.Values)
            {
                AbilityDefinition definition = registration.Definition;
                if (!string.IsNullOrEmpty(definition.IntrinsicModifierName) && !modifiersByName.ContainsKey(definition.IntrinsicModifierName))
                {
                    AddMissingReference("ability", definition.Name, "intrinsic modifier", definition.IntrinsicModifierName, registration.Origin);
                }

                ValidateActions("ability", definition.Name, definition.Actions, registration.Origin);
            }

            foreach (ModifierDefinitionRegistration registration in modifiersByName.Values)
            {
                ModifierDefinition definition = registration.Definition;
                if (!string.IsNullOrEmpty(definition.AuraModifierName) && !modifiersByName.ContainsKey(definition.AuraModifierName))
                {
                    AddMissingReference("modifier", definition.Name, "aura modifier", definition.AuraModifierName, registration.Origin);
                }

                ValidateActions("modifier", definition.Name + ".OnCreated", definition.OnCreatedActions, registration.Origin);
                ValidateActions("modifier", definition.Name + ".OnRefresh", definition.OnRefreshActions, registration.Origin);
                ValidateActions("modifier", definition.Name + ".OnDestroy", definition.OnDestroyActions, registration.Origin);
                ValidateActions("modifier", definition.Name + ".Interval", definition.IntervalActions, registration.Origin);
                ValidateActions("modifier", definition.Name + ".Trigger", definition.TriggerActions, registration.Origin);
            }
        }

        private void ValidateActions(string ownerKind, string ownerName, IReadOnlyList<ActionDefinition> actions, AbilityDefinitionOrigin origin)
        {
            if (actions == null)
            {
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                ActionDefinition action = actions[i];
                if (action == null)
                {
                    Validation.Add(AbilityValidationSeverity.Error, "ABILITYPROVIDER006", ownerKind + " " + ownerName + " contains a null action.", origin?.ToConfigSource());
                    continue;
                }

                if (action.ActionType == ActionType.AddModifier &&
                    (string.IsNullOrEmpty(action.ModifierName) || !modifiersByName.ContainsKey(action.ModifierName)))
                {
                    AddMissingReference(ownerKind, ownerName, "modifier", action.ModifierName, origin);
                }

                bool projectileAction = action.ActionType == ActionType.CreateTrackingProjectile || action.ActionType == ActionType.CreateLinearProjectile;
                if (projectileAction && action.Projectile == null)
                {
                    AddMissingReference(ownerKind, ownerName, "projectile", "<null>", origin);
                }
            }
        }

        private void ValidatePrivateName(string kind, string name, bool isPrivate, AbilityDefinitionOrigin origin)
        {
            if (!isPrivate)
            {
                return;
            }

            string expectedPrefix = string.IsNullOrWhiteSpace(origin?.Namespace) ? null : origin.Namespace + ".";
            if (string.IsNullOrEmpty(expectedPrefix) || !name.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                Validation.Add(
                    AbilityValidationSeverity.Warning,
                    "ABILITYPROVIDER007",
                    "Private " + kind + " " + name + " should use its document namespace prefix " + (expectedPrefix ?? "<missing namespace>") + ".",
                    origin?.ToConfigSource());
            }
        }

        private void AddInvalidRegistration(string kind, AbilityDefinitionOrigin origin)
        {
            Validation.Add(AbilityValidationSeverity.Error, "ABILITYPROVIDER008", "Provider returned a " + kind + " with no internal name.", origin?.ToConfigSource());
        }

        private void AddCollision(string keyKind, string key, AbilityDefinitionOrigin first, AbilityDefinitionOrigin second)
        {
            Validation.Add(
                AbilityValidationSeverity.Error,
                "ABILITYPROVIDER009",
                "Duplicate " + keyKind + " " + key + ". First: " + first + "; second: " + second + ". No load-order override is allowed.",
                second?.ToConfigSource(),
                first + " <-> " + second);
        }

        private void AddMissingReference(string ownerKind, string ownerName, string targetKind, string targetName, AbilityDefinitionOrigin origin)
        {
            Validation.Add(
                AbilityValidationSeverity.Error,
                "ABILITYPROVIDER010",
                ownerKind + " " + ownerName + " references missing " + targetKind + " " + targetName + ".",
                origin?.ToConfigSource(),
                ownerKind + ":" + ownerName + " -> " + targetKind + ":" + targetName);
        }

        private static AbilityDefinitionOrigin GetOrigin<T>(T registration)
        {
            if (registration is AbilityDefinitionRegistration ability) return ability.Origin;
            if (registration is ModifierDefinitionRegistration modifier) return modifier.Origin;
            if (registration is ProjectileDefinitionRegistration projectile) return projectile.Origin;
            return null;
        }
    }
}
