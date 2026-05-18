using System.Collections;
using UnityEngine;

namespace Game.Framework
{
    public class EffectManager : Singleton<ResourceManager>
    {
        public static EffectManager Instance { get; private set; }

        private const string EffectRootPath = "Effects/";

        public void Initialize()
        {
        }

        public GameObject PlayEffect(string effectName, Vector3 position)
        {
            GameObject prefab = Resources.Load<GameObject>(EffectRootPath + effectName);
            if (prefab == null)
            {
                Debug.LogWarning($"Effect prefab not found: {EffectRootPath + effectName}");
                return null;
            }

            GameObject effect = GameObject. Instantiate(prefab, position, Quaternion.identity);
            DestroyEffectWhenFinished(effect);
            return effect;
        }

        public GameObject PlayPersistentEffect(string effectName, Vector3 position, Transform parent = null)
        {
            GameObject prefab = Resources.Load<GameObject>(EffectRootPath + effectName);
            if (prefab == null)
            {
                Debug.LogWarning($"Effect prefab not found: {EffectRootPath + effectName}");
                return null;
            }

            GameObject effect = Instantiate(prefab, position, Quaternion.identity, parent);
            return effect;
        }

        public void PlayBowAttack(Vector3 startPosition, Vector3 targetPosition)
        {
            PlayProjectileWithHit("BowAttackEffect", "BowHitEffect", startPosition, targetPosition);
        }

        public void PlayIceAttack(Vector3 startPosition, Vector3 targetPosition)
        {
            PlayProjectileWithHit("IceAttackEffect", "IceHitEffect", startPosition, targetPosition);
        }

        public void PlayNpcDeath(Vector3 position)
        {
            PlayEffect("NpcDeathBurstEffect", position);
        }

        public void PlayBaseDestroyed(Vector3 position)
        {
            PlayEffect("BaseDestroyedEffect", position);
        }

        private void PlayProjectileWithHit(string projectileEffectName, string hitEffectName, Vector3 startPosition, Vector3 targetPosition)
        {
            GameObject projectile = PlayPersistentEffect(projectileEffectName, startPosition);
            if (projectile == null)
            {
                PlayEffect(hitEffectName, targetPosition);
                return;
            }

            VisualProjectileEffect visualProjectileEffect = projectile.GetComponent<VisualProjectileEffect>();
            if (visualProjectileEffect == null)
            {
                GameObject.Destroy(projectile);
                PlayEffect(hitEffectName, targetPosition);
                return;
            }

            visualProjectileEffect.Play(startPosition, targetPosition);
            StartCoroutine(PlayHitAfterDelay(hitEffectName, targetPosition, visualProjectileEffect.Duration));
        }

        private IEnumerator PlayHitAfterDelay(string hitEffectName, Vector3 targetPosition, float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayEffect(hitEffectName, targetPosition);
        }

        private void DestroyEffectWhenFinished(GameObject effect)
        {
            ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>();
            float maxTime = 0.5f;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem.MainModule main = particleSystems[i].main;
                float time = main.duration + main.startLifetime.constantMax;
                if (time > maxTime)
                {
                    maxTime = time;
                }
            }

          GameObject.  Destroy(effect, maxTime + 0.5f);
        }
    }

}