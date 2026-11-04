using Game.Ability.Configuration;
using NUnit.Framework;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Configuration")]
    public sealed class AbilityJsonSchemaValidationTests
    {
        [TestCase("{\"abilities\":[{\"name\":\"bolt\",\"cooldwon\":{\"baseValue\":1}}]}")]
        [TestCase("{\"abilities\":[{\"cooldown\":{\"values\":[1],\"value\":2}}]}")]
        [TestCase("{\"schemaVersion\":1,\"schemaVersion\":1}")]
        [TestCase("{\"schemaVersion\":\"1\"}")]
        [TestCase("{\"schemaVersion\":1.5}")]
        [TestCase("{\"schemaVersion\":2147483648}")]
        [TestCase("{\"schemaVersion\":null}")]
        [TestCase("{\"abilities\":[{\"cooldown\":{\"baseValue\":1e100}}]}")]
        [TestCase("{\"abilities\":[{\"name\":\"bad\\q\"}]}")]
        [TestCase("{\"schemaVersion\":01}")]
        [TestCase("{\"schemaVersion\":1,}")]
        [TestCase("{} {}")]
        [TestCase("null")]
        public void SchemaRejectsTyposDuplicatesWrongTypesAndMalformedJson(string json)
        {
            Assert.That(AbilityJsonSchemaValidator.TryValidate(json, typeof(JsonAbilityDocument), out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void ValidDocumentSupportsNestedValuesEscapedFieldNamesAndNullableReferences()
        {
            const string json = "{\"schemaVersion\":1,\"namespace\":null,\"abilities\":[{\"na\\u006de\":\"mage\\nbolt\",\"maxLevel\":2,\"charges\":{\"maxCharges\":2,\"restoreTime\":1.5,\"startFull\":false},\"cooldown\":{\"values\":[1e0,2.5]}}]}";
            Assert.That(AbilityJsonSchemaValidator.TryValidate(json, typeof(JsonAbilityDocument), out string error), Is.True, error);
        }

        [Test]
        public void DeserializationPreservesDefaultsAndHonorsExplicitNullsAndFalse()
        {
            const string json = "{\"abilities\":[{\"name\":\"cast\",\"charges\":null}],\"modifiers\":[{\"name\":\"mark\",\"isPurgable\":false}],\"projectiles\":[{\"name\":\"bolt\"}]}";
            Assert.That(AbilityJsonSchemaValidator.TryDeserialize(json, out JsonAbilityDocument document, out string error), Is.True, error);
            Assert.That(document.schemaVersion, Is.EqualTo(1));
            Assert.That(document.abilities[0].maxLevel, Is.EqualTo(1));
            Assert.That(document.abilities[0].charges, Is.Null);
            Assert.That(document.modifiers[0].removeOnDeath, Is.True);
            Assert.That(document.modifiers[0].isPurgable, Is.False);
            Assert.That(document.projectiles[0].maxLifetime, Is.EqualTo(10f));
            Assert.That(document.projectiles[0].deleteOnHit, Is.True);
        }

        [Test]
        public void ProviderKeepsInvalidMaximumsForFinalValidationInsteadOfClamping()
        {
            const string json = "{\"abilities\":[{\"name\":\"cast\",\"behavior\":4,\"maxLevel\":0}],\"modifiers\":[{\"name\":\"mark\",\"maxStack\":0}]}";
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new JsonAbilityDefinitionProvider(json, "invalid.json"));
            Assert.That(registry.IsValid, Is.False);
            Assert.That(registry.Abilities["cast"].Definition.MaxLevel, Is.EqualTo(0));
            Assert.That(registry.Modifiers["mark"].Definition.MaxStack, Is.EqualTo(0));
        }

        [Test]
        public void EscapedDuplicateFieldIsStillRejected()
        {
            const string json = "{\"namespace\":\"a\",\"na\\u006despace\":\"b\"}";
            Assert.That(AbilityJsonSchemaValidator.TryValidate(json, typeof(JsonAbilityDocument), out _), Is.False);
        }

        [Test]
        public void ProviderReportsUnknownFieldInsteadOfDroppingIt()
        {
            AbilityDefinitionBundle bundle = new JsonAbilityDefinitionProvider("{\"abilties\":[]}", "bad.json").Load();
            Assert.That(bundle.Validation.IsValid, Is.False);
            Assert.That(bundle.Validation.Issues[0].Code, Is.EqualTo("ABILITYJSON012"));
        }
    }
}
