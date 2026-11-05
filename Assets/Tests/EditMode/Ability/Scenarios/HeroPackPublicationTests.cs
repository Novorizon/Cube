using System;
using System.IO;
using System.Linq;
using Game.Ability.Configuration;
using NUnit.Framework;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Scenarios")]
    public sealed class HeroPackPublicationTests
    {
        [Test]
        public void EveryHeroFile_PublishesIndependentlyWithoutCustomScriptFactories()
        {
            string[] files = HeroFiles();
            Assert.That(files.Length, Is.EqualTo(7), "The seven hero files must be delivered together.");
            foreach (string file in files)
            {
                AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
                registry.LoadProvider(new JsonAbilityDefinitionProvider(File.ReadAllText(file), file));
                Assert.That(registry.IsValid, Is.True, file);
                Assert.That(registry.Abilities.Count > 0, Is.True, file);
                Assert.That(registry.Abilities.Values.All(value => !value.RequiresCustomScript), Is.True, file);
                Assert.That(registry.Modifiers.Values.All(value => !value.RequiresCustomScript), Is.True, file);
                Assert.That(registry.ApplyTo(new Game.Ability.AbilitySystem()), Is.True, file);
            }
        }

        [Test]
        public void HeroFiles_ComposeWithExistingJsonSkillsWithoutNameOrStableIdCollisions()
        {
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            foreach (string file in HeroFiles())
                registry.LoadProvider(new JsonAbilityDefinitionProvider(File.ReadAllText(file), file));
            string existing = Path.Combine(FindSourceRoot(), "Data", "AbilityJsonSources", "Validation", "composed_skills.json");
            registry.LoadProvider(new JsonAbilityDefinitionProvider(File.ReadAllText(existing), existing));

            Assert.That(registry.IsValid, Is.True);
            Assert.That(registry.Abilities.Count, Is.EqualTo(23));
            Assert.That(registry.ApplyTo(new Game.Ability.AbilitySystem()), Is.True);
        }

        [Test]
        public void EveryHeroSkill_HasMultipleLevelsAndAnExecutableDataPath()
        {
            foreach (string file in HeroFiles())
            {
                AbilityDefinitionBundle bundle = new JsonAbilityDefinitionProvider(File.ReadAllText(file), file).Load();
                Assert.That(bundle.Validation.IsValid, Is.True, file);
                foreach (AbilityDefinitionRegistration registration in bundle.Abilities)
                {
                    var definition = registration.Definition;
                    Assert.That(definition.MaxLevel > 1, Is.True, definition.Name);
                    Assert.That(definition.Actions.Count > 0 || !string.IsNullOrEmpty(definition.IntrinsicModifierName), Is.True, definition.Name);
                    Assert.That(!string.IsNullOrWhiteSpace(definition.Description), Is.True, definition.Name);
                }
            }
        }

        private static string[] HeroFiles()
        {
            return Directory.GetFiles(Path.Combine(FindSourceRoot(), "Data", "AbilityJsonSources", "Heroes"), "*.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static string FindSourceRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (DirectoryInfo directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                    if (Directory.Exists(Path.Combine(directory.FullName, "Data", "AbilityJsonSources", "Heroes"))) return directory.FullName;
            throw new DirectoryNotFoundException("The hero fixtures require Data/AbilityJsonSources/Heroes from the source verification bundle.");
        }
    }
}
