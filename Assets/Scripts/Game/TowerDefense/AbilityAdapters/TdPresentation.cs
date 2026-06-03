using Game.Framework;
using Game.Ability;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Presentation adapter for effects and sounds requested by the ability runtime.
    /// </summary>
    public sealed class TdPresentation : IPresentation
    {
        private static readonly HashSet<string> InvalidEffectWarnings = new HashSet<string>();

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
