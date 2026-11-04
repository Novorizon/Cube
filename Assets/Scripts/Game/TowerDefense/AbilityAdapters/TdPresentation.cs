using Game.Framework;
using Game.Ability;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Presentation adapter for effects and sounds requested by the ability runtime.
    /// </summary>
    public sealed class TdPresentation : IPresentation, ITrackedPresentation, IProjectilePresentation
    {
        private static readonly HashSet<string> InvalidEffectWarnings = new HashSet<string>();
        private readonly List<TdPresentationHandle> persistentHandles = new List<TdPresentationHandle>();
        private readonly List<GameObject> oneShotEffects = new List<GameObject>();
        private int lifetime;

        public void PlayEffect(string effectName, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(effectName))
            {
                return;
            }

            if (!effectName.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                if (InvalidEffectWarnings.Add(effectName))
                {
                    Debug.LogWarning($"Ability effect location must be a full asset path. location: {effectName}");
                }

                return;
            }

            LoadOneShotEffectAsync(effectName, position, lifetime).Forget();
        }

        public void PlayEffect(string effectName, IUnit target)
        {
            if (target == null)
            {
                return;
            }

            PlayEffect(effectName, target.Position);
        }

        public IPresentationHandle PlayPersistentEffect(string effectName, IUnit target)
        {
            if (target == null || !IsValidEffect(effectName))
            {
                return null;
            }

            TdPresentationHandle handle = new TdPresentationHandle(effectName, target, OnHandleStopped);
            persistentHandles.Add(handle);
            LoadPersistentEffectAsync(handle, target).Forget();
            return handle;
        }

        public IProjectilePresentationHandle PlayProjectileEffect(string effectName, Projectile projectile)
        {
            if (projectile == null || !IsValidEffect(effectName)) return null;
            TdPresentationHandle handle = new TdPresentationHandle(effectName, projectile.Position, OnHandleStopped);
            persistentHandles.Add(handle);
            LoadPersistentEffectAsync(handle, null).Forget();
            return handle;
        }

        public void Clear()
        {
            lifetime++;
            TdPresentationHandle[] handles = persistentHandles.ToArray();
            persistentHandles.Clear();
            foreach (TdPresentationHandle handle in handles) handle.Stop();
            for (int i = 0; i < oneShotEffects.Count; i++) if (oneShotEffects[i] != null) Object.Destroy(oneShotEffects[i]);
            oneShotEffects.Clear();
        }

        public void GetActivePresentationHandles(IList<PresentationHandleInfo> results)
        {
            if (results == null)
            {
                return;
            }

            for (int i = persistentHandles.Count - 1; i >= 0; i--)
            {
                TdPresentationHandle handle = persistentHandles[i];
                if (handle == null || !handle.IsActive)
                {
                    persistentHandles.RemoveAt(i);
                    continue;
                }

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
            if (string.IsNullOrWhiteSpace(soundName))
            {
                return;
            }

            AudioManager.Instance.PlaySound(soundName, new AudioPlayOptions { Position = position });
        }

        private static bool IsValidEffect(string effectName)
        {
            if (string.IsNullOrWhiteSpace(effectName))
            {
                return false;
            }

            if (effectName.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                return true;
            }

            if (InvalidEffectWarnings.Add(effectName))
            {
                Debug.LogWarning($"Ability effect location must be a full asset path. location: {effectName}");
            }
            return false;
        }

        private static async Task LoadPersistentEffectAsync(TdPresentationHandle handle, IUnit target)
        {
            GameObject effect = await EffectManager.Instance.PlayEffectAsync(handle.EffectName, handle.Position, false);
            if (effect == null)
            {
                handle.Stop();
                return;
            }

            if (!handle.IsActive)
            {
                Object.Destroy(effect);
                return;
            }

            Transform targetTransform = ResolveTransform(target);
            if (targetTransform != null)
            {
                handle.SetPosition(target.Position);
                effect.transform.SetParent(targetTransform, true);
            }
            handle.Attach(effect);
        }

        private async Task LoadOneShotEffectAsync(string effectName, Vector3 position, int expectedLifetime)
        {
            GameObject effect = await EffectManager.Instance.PlayEffectAsync(effectName, position);
            if (effect == null) return;
            if (expectedLifetime != lifetime)
            {
                Object.Destroy(effect);
                return;
            }
            oneShotEffects.RemoveAll(item => item == null);
            oneShotEffects.Add(effect);
        }

        private static Transform ResolveTransform(IUnit target)
        {
            if (!(target is TdUnit unit))
            {
                return null;
            }

            if (unit.Npc != null) return unit.Npc.transform;
            if (unit.Tower != null) return unit.Tower.transform;
            return null;
        }

        private void OnHandleStopped(TdPresentationHandle handle)
        {
            persistentHandles.Remove(handle);
        }

        private sealed class TdPresentationHandle : IPresentationHandle, IProjectilePresentationHandle
        {
            private readonly System.Action<TdPresentationHandle> stopped;
            private GameObject effect;

            public TdPresentationHandle(string effectName, IUnit target, System.Action<TdPresentationHandle> stopped)
            {
                EffectName = effectName;
                TargetEntityId = target != null ? target.EntityId : 0;
                Position = target != null ? target.Position : Vector3.zero;
                IsActive = true;
                this.stopped = stopped;
            }

            public TdPresentationHandle(string effectName, Vector3 position, System.Action<TdPresentationHandle> stopped)
            {
                EffectName = effectName;
                Position = position;
                IsActive = true;
                this.stopped = stopped;
            }

            public string EffectName { get; }
            public int TargetEntityId { get; }
            public bool IsActive { get; private set; }
            public Vector3 Position { get; private set; }

            public void SetPosition(Vector3 position)
            {
                if (!IsActive) return;
                Position = position;
                if (effect != null) effect.transform.position = position;
            }

            public void Attach(GameObject instance)
            {
                if (!IsActive)
                {
                    if (instance != null) Object.Destroy(instance);
                    return;
                }

                effect = instance;
                if (effect != null) effect.transform.position = Position;
            }

            public void Stop()
            {
                if (!IsActive)
                {
                    return;
                }

                IsActive = false;
                if (effect != null)
                {
                    Object.Destroy(effect);
                    effect = null;
                }
                stopped?.Invoke(this);
            }
        }
    }
}

