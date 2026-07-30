using System;
using System.Collections.Generic;

namespace Game
{
    public static class GuideTargetRegistry
    {
        private static readonly Dictionary<string, GuideTarget> Targets =
            new Dictionary<string, GuideTarget>(StringComparer.Ordinal);

        public static event Action<string, GuideTarget> TargetChanged;

        public static bool TryGet(string targetId, out GuideTarget target)
        {
            if (!string.IsNullOrWhiteSpace(targetId) &&
                Targets.TryGetValue(targetId, out target) &&
                target != null &&
                target.isActiveAndEnabled)
            {
                return true;
            }

            target = null;
            return false;
        }

        internal static void Register(string targetId, GuideTarget target)
        {
            if (string.IsNullOrWhiteSpace(targetId) || target == null)
            {
                return;
            }

            Targets[targetId] = target;
            TargetChanged?.Invoke(targetId, target);
        }

        internal static void Unregister(string targetId, GuideTarget target)
        {
            if (string.IsNullOrWhiteSpace(targetId) || target == null)
            {
                return;
            }

            if (Targets.TryGetValue(targetId, out GuideTarget current) && current == target)
            {
                Targets.Remove(targetId);
                TargetChanged?.Invoke(targetId, null);
            }
        }
    }
}
