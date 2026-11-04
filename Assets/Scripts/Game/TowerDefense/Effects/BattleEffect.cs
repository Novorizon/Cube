using Game.Framework;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Game
{
    public class BattleEffect
    {

        public static async Task PlayEffectAsync(string effect,Vector3 position)
        {
            await EffectManager.Instance.PlayEffectAsync(effect, position);
        }


        public static Task<GameObject> PlayNpcDeathAsync(Vector3 position)
        {
            return EffectManager.Instance.PlayEffectAsync("Assets/Arts/Effects/NpcDeathBurstEffect.prefab", position);
        }

        public static Task<GameObject> PlayBaseDestroyedAsync(Vector3 position)
        {
            return EffectManager.Instance.PlayEffectAsync("Assets/Arts/Effects/BaseDestroyedEffect.prefab", position);
        }

        public static async Task PlayProjectileAsync(string projectileEffectName, Vector3 startPosition, Vector3 targetPosition)
        {
            GameObject projectile = await EffectManager.Instance.PlayEffectAsync(projectileEffectName, startPosition,false);
            if (projectile == null)
            {
                return;
            }

            VisualProjectileEffect visualProjectileEffect = projectile.GetComponent<VisualProjectileEffect>();
            if (visualProjectileEffect == null)
            {
                Object.Destroy(projectile);
                return;
            }

            visualProjectileEffect.Play(startPosition, targetPosition);

            await DelayByGameTimeAsync(visualProjectileEffect.Duration);
        }

        public static async Task PlayProjectileWithHitAsync(string projectileEffectName, string hitEffectName, Vector3 startPosition, Vector3 targetPosition, System.Func<bool> isActive = null, System.Action onImpact = null)
        {
            if (isActive != null && !isActive()) return;
            GameObject projectile = null;
            try
            {
                projectile = await EffectManager.Instance.PlayEffectAsync(projectileEffectName, startPosition, false);
                if (isActive != null && !isActive()) return;
                VisualProjectileEffect visual = projectile != null ? projectile.GetComponent<VisualProjectileEffect>() : null;
                if (visual != null)
                {
                    visual.Play(startPosition, targetPosition);
                    float elapsed = 0f;
                    while (elapsed < visual.Duration)
                    {
                        if (isActive != null && !isActive()) return;
                        elapsed += Time.deltaTime;
                        await Task.Yield();
                    }
                }
                if (isActive != null && !isActive()) return;
                onImpact?.Invoke();
                if (isActive != null && !isActive()) return;
                GameObject hit = await EffectManager.Instance.PlayEffectAsync(hitEffectName, targetPosition);
                if (hit != null && isActive != null && !isActive()) Object.Destroy(hit);
            }
            finally
            {
                if (projectile != null) Object.Destroy(projectile);
            }
        }

        private static async Task DelayByGameTimeAsync(float seconds)
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
    }
}
