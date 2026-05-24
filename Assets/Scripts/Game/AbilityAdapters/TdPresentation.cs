using Game.Framework;
using Game.Ability;
using UnityEngine;

namespace Game
{
    public sealed class TdPresentation : IPresentation
    {
        public void PlayEffect(string effectName, Vector3 position)
        {
            if (string.IsNullOrEmpty(effectName))
            {
                return;
            }

            BattleEffect.PlayEffectAsync(effectName, position).Forget();
        }

        public void PlayEffect(string effectName, IUnit target)
        {
            if (target == null)
            {
                return;
            }

            PlayEffect(effectName, target.Position);
        }

        public void PlaySound(string soundName, Vector3 position)
        {
            if (string.IsNullOrEmpty(soundName))
            {
                return;
            }

            Debug.Log($"Ability sound requested: {soundName}, position: {position}");
        }
    }
}
