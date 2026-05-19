using System.Threading.Tasks;
using UnityEngine;

namespace Game.Framework
{
    public class EffectManager : Singleton<EffectManager>
    {
        public void Initialize()
        {
        }

        public async Task<GameObject> PlayEffectAsync(string effectName, Vector3 position,bool autoDestory=true)
        {
            GameObject prefab = await LoadEffectPrefabAsync(effectName);
            if (prefab == null)
            {
                return null;
            }

            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);

            if (autoDestory)
            {
                DestroyEffectWhenFinished(effect);
            }
            return effect;
        }

        public async Task PlayProjectileWithHitAsync(string projectileEffectName, string hitEffectName, Vector3 startPosition, Vector3 targetPosition)
        {
            GameObject projectile = await PlayEffectAsync(projectileEffectName, startPosition,false);
            if (projectile == null)
            {
                await PlayEffectAsync(hitEffectName, targetPosition);
                return;
            }

            VisualProjectileEffect visualProjectileEffect = projectile.GetComponent<VisualProjectileEffect>();
            if (visualProjectileEffect == null)
            {
                Object.Destroy(projectile);
                await PlayEffectAsync(hitEffectName, targetPosition);
                return;
            }

            visualProjectileEffect.Play(startPosition, targetPosition);

            await DelayByGameTimeAsync(visualProjectileEffect.Duration);

            await PlayEffectAsync(hitEffectName, targetPosition);
        }

        private async Task<GameObject> LoadEffectPrefabAsync(string location)
        {
            GameObject prefab = await ResourceManager.Instance.LoadAssetAsync<GameObject>(location);
            if (prefab == null)
            {
                Debug.LogWarning($"Effect prefab not found: {location}");
                return null;
            }

            return prefab;
        }


        private async Task DelayByGameTimeAsync(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            float timer = 0f;

            while (timer < seconds)
            {
                timer += Time.deltaTime;
                await Task.Yield();
            }
        }

        private void DestroyEffectWhenFinished(GameObject effect)
        {
            if (effect == null)
            {
                return;
            }

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

            Object.Destroy(effect, maxTime + 0.5f);
        }
    }
}