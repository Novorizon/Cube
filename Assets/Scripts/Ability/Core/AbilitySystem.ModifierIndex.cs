using System.Collections.Generic;

namespace Game.Ability
{
    public sealed partial class AbilitySystem
    {
        private readonly Dictionary<int, List<Modifier>> modifiersByParent = new Dictionary<int, List<Modifier>>();
        private readonly HashSet<Modifier> registeredModifiers = new HashSet<Modifier>();
        private readonly HashSet<Projectile> registeredProjectiles = new HashSet<Projectile>();
        private readonly HashSet<Thinker> registeredThinkers = new HashSet<Thinker>();
        private readonly Dictionary<ModifierEventType, List<Modifier>> configuredListeners = new Dictionary<ModifierEventType, List<Modifier>>();
        private readonly List<Modifier> customListeners = new List<Modifier>();
        private readonly Dictionary<Modifier, long> listenerOrder = new Dictionary<Modifier, long>();
        private long nextListenerOrder;

        internal bool IsModifierRegistered(Modifier modifier) => modifier != null && registeredModifiers.Contains(modifier);
        internal bool ContainsProjectile(Projectile projectile) => projectile != null && registeredProjectiles.Contains(projectile);

        private void IndexModifier(Modifier modifier)
        {
            registeredModifiers.Add(modifier);
            int id = modifier.Parent.EntityId;
            if (!modifiersByParent.TryGetValue(id, out List<Modifier> bucket)) modifiersByParent.Add(id, bucket = new List<Modifier>());
            bucket.Add(modifier);

            // Built-in stat/lifecycle modifiers have no event callback. Custom scripts retain
            // the global event contract; configured scripts subscribe to their declared event.
            if (modifier.Script is DefaultModifierScript) return;
            if (modifier.Script is ConfiguredModifierScript)
            {
                ModifierEventType type = modifier.Definition.TriggerEventType;
                if (type == ModifierEventType.None || modifier.Definition.TriggerActions.Count == 0) return;
                if (!configuredListeners.TryGetValue(type, out List<Modifier> listeners)) configuredListeners.Add(type, listeners = new List<Modifier>());
                listeners.Add(modifier);
            }
            else customListeners.Add(modifier);
            listenerOrder.Add(modifier, nextListenerOrder++);
        }

        private void UnindexModifier(Modifier modifier)
        {
            registeredModifiers.Remove(modifier);
            if (modifiersByParent.TryGetValue(modifier.Parent.EntityId, out List<Modifier> bucket))
            {
                bucket.Remove(modifier);
                if (bucket.Count == 0) modifiersByParent.Remove(modifier.Parent.EntityId);
            }
            if (!listenerOrder.Remove(modifier)) return;
            if (modifier.Script is ConfiguredModifierScript)
            {
                // Definitions are fixed after registration. Use the membership itself for
                // cleanup so even an unsupported caller mutation cannot retain a dead entry.
                foreach (List<Modifier> listeners in configuredListeners.Values) listeners.Remove(modifier);
            }
            else customListeners.Remove(modifier);
        }

        private void ClearRuntimeIndexes()
        {
            modifiersByParent.Clear();
            registeredModifiers.Clear();
            registeredProjectiles.Clear();
            registeredThinkers.Clear();
            configuredListeners.Clear();
            customListeners.Clear();
            listenerOrder.Clear();
            nextListenerOrder = 0;
        }

        private List<Modifier> RentUnitModifiers(IUnit unit)
        {
            modifiersByParent.TryGetValue(unit.EntityId, out List<Modifier> bucket);
            return RuntimeSnapshotPool<Modifier>.Rent(bucket);
        }

        private List<Modifier> RentModifierEventListeners(ModifierEvent value)
        {
            var snapshot = RuntimeSnapshotPool<Modifier>.Rent();
            configuredListeners.TryGetValue(value.EventType, out List<Modifier> configured);
            int configuredIndex = 0;
            int customIndex = 0;
            // Merge two registration-ordered lists without sorting or changing callback order.
            while (configuredIndex < (configured?.Count ?? 0) || customIndex < customListeners.Count)
            {
                bool takeConfigured = customIndex >= customListeners.Count || configuredIndex < (configured?.Count ?? 0) &&
                    listenerOrder[configured[configuredIndex]] < listenerOrder[customListeners[customIndex]];
                Modifier modifier = takeConfigured ? configured[configuredIndex++] : customListeners[customIndex++];
                if (!takeConfigured || ((ConfiguredModifierScript)modifier.Script).ShouldHandleEvent(value)) snapshot.Add(modifier);
            }
            return snapshot;
        }
    }

}
