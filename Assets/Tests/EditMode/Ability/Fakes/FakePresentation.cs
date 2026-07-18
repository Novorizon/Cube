using System.Collections.Generic;
using Game.Ability;
using UnityEngine;

namespace Game.Tests.Ability
{
    public sealed class FakePresentation : IPresentation, ITrackedPresentation
    {
        public sealed class PresentationEvent
        {
            public string Name;
            public Vector3 Position;
            public IUnit Target;
        }

        private readonly List<PresentationEvent> effects = new List<PresentationEvent>();
        private readonly List<PresentationEvent> sounds = new List<PresentationEvent>();
        private readonly List<FakePresentationHandle> persistentHandles = new List<FakePresentationHandle>();

        public IReadOnlyList<PresentationEvent> Effects => effects;
        public IReadOnlyList<PresentationEvent> Sounds => sounds;

        public void PlayEffect(string effectName, Vector3 position)
        {
            effects.Add(new PresentationEvent { Name = effectName, Position = position });
        }

        public void PlayEffect(string effectName, IUnit target)
        {
            effects.Add(new PresentationEvent
            {
                Name = effectName,
                Position = target != null ? target.Position : Vector3.zero,
                Target = target
            });
        }

        public IPresentationHandle PlayPersistentEffect(string effectName, IUnit target)
        {
            FakePresentationHandle handle = new FakePresentationHandle(effectName, target != null ? target.EntityId : 0);
            persistentHandles.Add(handle);
            return handle;
        }

        public void GetActivePresentationHandles(IList<PresentationHandleInfo> results)
        {
            if (results == null) return;
            for (int i = 0; i < persistentHandles.Count; i++)
            {
                FakePresentationHandle handle = persistentHandles[i];
                if (!handle.IsActive) continue;
                results.Add(new PresentationHandleInfo
                {
                    EffectName = handle.EffectName,
                    TargetEntityId = handle.TargetEntityId,
                    IsActive = true
                });
            }
        }

        public void PlaySound(string soundName, Vector3 position)
        {
            sounds.Add(new PresentationEvent { Name = soundName, Position = position });
        }

        private sealed class FakePresentationHandle : IPresentationHandle
        {
            public FakePresentationHandle(string effectName, int targetEntityId)
            {
                EffectName = effectName;
                TargetEntityId = targetEntityId;
                IsActive = true;
            }

            public string EffectName { get; }
            public int TargetEntityId { get; }
            public bool IsActive { get; private set; }
            public void Stop() => IsActive = false;
        }
    }
}
