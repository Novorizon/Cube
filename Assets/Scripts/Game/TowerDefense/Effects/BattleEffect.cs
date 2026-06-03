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

        public static async Task PlayProjectileWithHitAsync(string projectileEffectName, string hitEffectName, Vector3 startPosition, Vector3 targetPosition)
        {
            GameObject projectile = await EffectManager.Instance.PlayEffectAsync(projectileEffectName, startPosition, false);
            if (projectile == null)
            {
                await EffectManager.Instance.PlayEffectAsync(hitEffectName, targetPosition);
                return;
            }

            VisualProjectileEffect visualProjectileEffect = projectile.GetComponent<VisualProjectileEffect>();
            if (visualProjectileEffect == null)
            {
                Object.Destroy(projectile);
                await EffectManager.Instance.PlayEffectAsync(hitEffectName, targetPosition);
                return;
            }

            visualProjectileEffect.Play(startPosition, targetPosition);

            await DelayByGameTimeAsync(visualProjectileEffect.Duration);

            await EffectManager.Instance.PlayEffectAsync(hitEffectName, targetPosition);
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