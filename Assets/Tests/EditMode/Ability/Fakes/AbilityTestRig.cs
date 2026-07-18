using Game.Ability;
using UnityEngine;

namespace Game.Tests.Ability
{
    public sealed class AbilityTestRig
    {
        private int nextEntityId = 1;

        public AbilityTestRig()
        {
            World = new FakeWorld();
            Presentation = new FakePresentation();
            Engine = new AbilitySystem();
            Engine.Initialize(World, Presentation);
        }

        public AbilitySystem Engine { get; }
        public FakeWorld World { get; }
        public FakePresentation Presentation { get; }

        public FakeUnit AddUnit(int teamId, Vector3 position, UnitType unitType = UnitType.Basic)
        {
            FakeUnit unit = new FakeUnit(nextEntityId++, teamId, position, unitType);
            World.AddUnit(unit);
            return unit;
        }

        public Game.Ability.Ability RegisterAndAddAbility(
            FakeUnit owner,
            AbilityDefinition definition,
            IResourceOwner resourceOwner = null,
            int level = 1)
        {
            Engine.RegisterAbilityDefinition(definition);
            return Engine.AddAbility(owner, definition.Name, level, resourceOwner);
        }

        public void Tick(float deltaTime)
        {
            World.Advance(deltaTime);
            Engine.Update(deltaTime);
        }
    }
}
