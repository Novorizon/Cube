using System;
using System.Collections.Generic;
using Game.Ability.Configuration;
using Game.Framework;
using UnityEngine;

namespace Game
{
    /// <summary>Explicit project resource integration; no directory scanning or implicit generated-data input.</summary>
    internal sealed class TdJsonAbilityManifestProvider : IAbilityDefinitionProvider
    {
        private readonly string manifestPath;
        internal TdJsonAbilityManifestProvider(string manifestPath) { this.manifestPath = manifestPath; }
        public string ProviderName => "Ability JSON manifest: " + manifestPath;
        public AbilityDefinitionSourceType SourceType => AbilityDefinitionSourceType.Json;

        public AbilityDefinitionBundle Load()
        {
            AbilityDefinitionBundle result = new AbilityDefinitionBundle();
            TextAsset manifestAsset = ResourceManager.Instance.LoadAsset<TextAsset>(manifestPath);
            if (manifestAsset == null)
            {
                AddError(result, "Manifest asset could not be loaded: " + manifestPath);
                return result;
            }
            if (!AbilityJsonSchemaValidator.TryDeserialize(manifestAsset.text, out Manifest manifest, out string error))
            {
                AddError(result, "Manifest JSON is invalid: " + error);
                return result;
            }
            if (manifest == null || manifest.schemaVersion != 1)
            {
                AddError(result, "Manifest schemaVersion must be 1.");
                return result;
            }
            HashSet<string> loaded = new HashSet<string>(StringComparer.Ordinal);
            foreach (Source source in manifest.sources ?? Array.Empty<Source>())
            {
                if (source == null || !source.enabled) continue;
                if (!IsHandAuthoredAssetPath(source.assetPath))
                {
                    AddError(result, "Enabled source requires a full hand-authored Assets path outside generated Assets/Data/Json: " + source.assetPath);
                    continue;
                }
                if (!loaded.Add(source.assetPath))
                {
                    AddError(result, "Manifest repeats enabled source: " + source.assetPath);
                    continue;
                }
                TextAsset asset = ResourceManager.Instance.LoadAsset<TextAsset>(source.assetPath);
                if (asset == null)
                {
                    AddError(result, "Enabled source could not be loaded: " + source.assetPath);
                    continue;
                }
                AbilityDefinitionBundle bundle = new JsonAbilityDefinitionProvider(asset.text, source.assetPath).Load();
                result.Validation.Merge(bundle.Validation);
                result.Abilities.AddRange(bundle.Abilities);
                result.Modifiers.AddRange(bundle.Modifiers);
                result.Projectiles.AddRange(bundle.Projectiles);
            }
            return result;
        }

        internal static bool IsHandAuthoredAssetPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Assets/Data/Json/", StringComparison.Ordinal) && path.IndexOf("..", StringComparison.Ordinal) < 0 && path.IndexOf('\\') < 0;
        }

        private void AddError(AbilityDefinitionBundle bundle, string message)
        {
            bundle.Validation.Add(AbilityValidationSeverity.Error, "ABILITYMANIFEST001", message, new AbilityConfigSource { SourceType = "Json", Path = manifestPath });
        }

        [Serializable]
        private sealed class Manifest
        {
            public Manifest() { }
            // Zero is deliberately invalid: authoring must explicitly select schemaVersion 1.
            public int schemaVersion = 0;
            public Source[] sources = Array.Empty<Source>();
        }

        [Serializable]
        private sealed class Source
        {
            public Source() { }
            public bool enabled = false;
            public string assetPath = string.Empty;
        }
    }
}
