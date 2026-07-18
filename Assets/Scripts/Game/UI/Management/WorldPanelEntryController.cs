using System;
using System.Collections.Generic;
using UI;

namespace Game
{
    internal sealed class WorldPanelEntry
    {
        public WorldPanelEntry(
            string id,
            string groupId,
            string prefabPath,
            Action<bool> setOpenState = null)
        {
            Id = id;
            GroupId = groupId;
            PrefabPath = prefabPath;
            setOpen = setOpenState;
        }

        private readonly Action<bool> setOpen;

        public string Id { get; }
        public string GroupId { get; }
        public string PrefabPath { get; }

        public void SetOpen(bool isOpen)
        {
            setOpen?.Invoke(isOpen);
        }
    }

    internal sealed class WorldPanelEntryController
    {
        private readonly Dictionary<string, WorldPanelEntry> entriesById = new Dictionary<string, WorldPanelEntry>();
        private readonly List<WorldPanelEntry> entries = new List<WorldPanelEntry>();

        public void Clear()
        {
            entriesById.Clear();
            entries.Clear();
        }

        public void Register(WorldPanelEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.PrefabPath))
            {
                return;
            }

            entriesById[entry.Id] = entry;
            entries.Add(entry);

            if (!string.IsNullOrWhiteSpace(entry.GroupId) && UIManager.Instance != null)
            {
                UIManager.Instance.Panels.RegisterExclusivePanel(entry.GroupId, entry.PrefabPath);
            }
        }

        public bool TryGet(string entryId, out WorldPanelEntry entry)
        {
            return entriesById.TryGetValue(entryId, out entry) && entry != null;
        }

        public bool IsShown(WorldPanelEntry entry)
        {
            return entry != null &&
                   UIManager.Instance != null &&
                   UIManager.Instance.Panels.IsShown(entry.PrefabPath);
        }

        public void RefreshStates()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                WorldPanelEntry entry = entries[i];
                entry?.SetOpen(IsShown(entry));
            }
        }
    }
}
