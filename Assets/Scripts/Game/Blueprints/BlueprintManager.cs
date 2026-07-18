using Game.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game
{
    public sealed class BlueprintItem
    {
        public int ItemId;
        public int Count;
    }

    public sealed class BlueprintConfig
    {
        public int Id;
        public string Name;
        public int BuildingId;
        public BlueprintItem[] Inputs;
        public BlueprintItem[] Outputs;
        public int UnlockTechId;
        public int UnlockQuestId;
        public int DurationSeconds;
        public bool Enable = true;
    }

    public sealed class BlueprintManager
    {
        private const int BlueprintItemKindInput = 1;
        private const int BlueprintItemKindOutput = 2;

        public static BlueprintManager Instance { get; } = new BlueprintManager();

        private readonly Dictionary<int, BlueprintConfig> blueprints = new Dictionary<int, BlueprintConfig>();
        private readonly Dictionary<int, List<BlueprintConfig>> blueprintsByBuildingId = new Dictionary<int, List<BlueprintConfig>>();
        private readonly StringBuilder builder = new StringBuilder(128);

        private BlueprintManager()
        {
        }

        public void Initialize()
        {
            blueprints.Clear();
            blueprintsByBuildingId.Clear();
            LoadConfigs();
        }

        public BlueprintConfig Get(int blueprintId)
        {
            blueprints.TryGetValue(blueprintId, out BlueprintConfig config);
            return config;
        }

        public BlueprintConfig GetFirstBlueprintForBuilding(int buildingId)
        {
            if (!blueprintsByBuildingId.TryGetValue(buildingId, out List<BlueprintConfig> list) || list.Count == 0)
            {
                return null;
            }

            return list[0];
        }

        public IReadOnlyList<BlueprintConfig> GetBlueprintsForBuilding(int buildingId)
        {
            if (!blueprintsByBuildingId.TryGetValue(buildingId, out List<BlueprintConfig> list))
            {
                return Array.Empty<BlueprintConfig>();
            }

            return list;
        }

        public bool CanComplete(int blueprintId)
        {
            BlueprintConfig config = Get(blueprintId);
            if (config == null || !config.Enable)
            {
                return false;
            }

            if (config.BuildingId > 0 && !HasActiveBuilding(config.BuildingId))
            {
                return false;
            }

            if (config.UnlockTechId > 0 && !TechManager.Instance.IsResearched(config.UnlockTechId))
            {
                return false;
            }

            if (config.UnlockQuestId > 0 && !QuestManager.Instance.IsCompleted(config.UnlockQuestId))
            {
                return false;
            }

            return ItemManager.Instance.HasItems(CreateItemStacks(config.Inputs));
        }

        public bool TryCompleteFirstForBuilding(int buildingId)
        {
            BlueprintConfig config = GetFirstBlueprintForBuilding(buildingId);
            return config != null && TryComplete(config.Id);
        }

        public bool TryComplete(int blueprintId)
        {
            if (!CanComplete(blueprintId))
            {
                return false;
            }

            BlueprintConfig config = blueprints[blueprintId];
            IReadOnlyList<ItemStack> inputs = CreateItemStacks(config.Inputs);
            if (!ItemManager.Instance.TryConsumeItems(inputs))
            {
                return false;
            }

            IReadOnlyList<ItemStack> outputs = CreateItemStacks(config.Outputs);
            if (!BagManager.Instance.TryAddItems(outputs))
            {
                ItemManager.Instance.AddItems(inputs);
                return false;
            }

            QuestManager.Instance.NotifyEvent(QuestEventType.BlueprintCompleted, blueprintId);
            return true;
        }

        public string FormatBlueprint(BlueprintConfig config)
        {
            if (config == null)
            {
                return LocalizationManager.GetOrFallback("ui.blueprint.none", "No blueprint");
            }

            builder.Clear();
            builder.AppendLine(GetBlueprintName(config));
            builder.Append(LocalizationManager.GetOrFallback("ui.blueprint.input", "In:"));
            builder.Append(' ');
            AppendItems(builder, config.Inputs);
            builder.AppendLine();
            builder.Append(LocalizationManager.GetOrFallback("ui.blueprint.output", "Out:"));
            builder.Append(' ');
            AppendItems(builder, config.Outputs);
            return builder.ToString();
        }

        private void LoadConfigs()
        {
            IReadOnlyDictionary<int, BlueprintTableConfig> table = DataManager.Instance.Blueprint?.GetAll();
            if (table == null)
            {
                Debug.LogError("Blueprint config table is not loaded.");
                return;
            }

            Dictionary<int, List<BlueprintItemTableConfig>> itemsByBlueprintId = BuildBlueprintItemIndex();
            List<BlueprintTableConfig> rows = new List<BlueprintTableConfig>(table.Values);
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));

            for (int i = 0; i < rows.Count; i++)
            {
                AddBlueprint(CreateBlueprintConfig(rows[i], itemsByBlueprintId));
            }
        }

        private static Dictionary<int, List<BlueprintItemTableConfig>> BuildBlueprintItemIndex()
        {
            Dictionary<int, List<BlueprintItemTableConfig>> index = new Dictionary<int, List<BlueprintItemTableConfig>>();
            IReadOnlyDictionary<int, BlueprintItemTableConfig> table = DataManager.Instance.BlueprintItem?.GetAll();
            if (table == null)
            {
                return index;
            }

            foreach (KeyValuePair<int, BlueprintItemTableConfig> pair in table)
            {
                BlueprintItemTableConfig row = pair.Value;
                if (row == null || !row.Enable || row.BlueprintId <= 0)
                {
                    continue;
                }

                if (!index.TryGetValue(row.BlueprintId, out List<BlueprintItemTableConfig> rows))
                {
                    rows = new List<BlueprintItemTableConfig>();
                    index.Add(row.BlueprintId, rows);
                }

                rows.Add(row);
            }

            foreach (List<BlueprintItemTableConfig> rows in index.Values)
            {
                rows.Sort((a, b) => a.SortOrder != b.SortOrder
                    ? a.SortOrder.CompareTo(b.SortOrder)
                    : a.Id.CompareTo(b.Id));
            }

            return index;
        }

        private static BlueprintConfig CreateBlueprintConfig(
            BlueprintTableConfig row,
            IReadOnlyDictionary<int, List<BlueprintItemTableConfig>> itemsByBlueprintId)
        {
            if (row == null)
            {
                return null;
            }

            itemsByBlueprintId.TryGetValue(row.Id, out List<BlueprintItemTableConfig> items);
            return new BlueprintConfig
            {
                Id = row.Id,
                Name = row.Name,
                BuildingId = row.BuildingId,
                Inputs = CreateBlueprintItems(items, BlueprintItemKindInput),
                Outputs = CreateBlueprintItems(items, BlueprintItemKindOutput),
                UnlockTechId = row.UnlockTechId,
                UnlockQuestId = row.UnlockQuestId,
                DurationSeconds = row.DurationSeconds,
                Enable = row.Enable,
            };
        }

        private static BlueprintItem[] CreateBlueprintItems(IReadOnlyList<BlueprintItemTableConfig> rows, int itemKind)
        {
            if (rows == null || rows.Count == 0)
            {
                return Array.Empty<BlueprintItem>();
            }

            List<BlueprintItem> items = new List<BlueprintItem>();
            for (int i = 0; i < rows.Count; i++)
            {
                BlueprintItemTableConfig row = rows[i];
                if (row == null || row.ItemKind != itemKind || row.ItemId <= 0 || row.Count <= 0)
                {
                    continue;
                }

                items.Add(new BlueprintItem
                {
                    ItemId = row.ItemId,
                    Count = row.Count,
                });
            }

            return items.ToArray();
        }

        private void AddBlueprint(BlueprintConfig config)
        {
            if (config == null || config.Id <= 0 || !config.Enable || blueprints.ContainsKey(config.Id))
            {
                return;
            }

            blueprints.Add(config.Id, config);
            if (config.BuildingId <= 0)
            {
                return;
            }

            if (!blueprintsByBuildingId.TryGetValue(config.BuildingId, out List<BlueprintConfig> list))
            {
                list = new List<BlueprintConfig>();
                blueprintsByBuildingId.Add(config.BuildingId, list);
            }

            list.Add(config);
        }

        private static IReadOnlyList<ItemStack> CreateItemStacks(IReadOnlyList<BlueprintItem> items)
        {
            List<ItemStack> worldItems = new List<ItemStack>();
            if (items == null)
            {
                return worldItems;
            }

            for (int i = 0; i < items.Count; i++)
            {
                BlueprintItem item = items[i];
                if (item != null && item.ItemId > 0 && item.Count > 0)
                {
                    worldItems.Add(new ItemStack(item.ItemId, item.Count));
                }
            }

            return worldItems;
        }

        private static bool HasActiveBuilding(int buildingId)
        {
            foreach (KeyValuePair<int, WorldBuilding> pair in WorldBuildingManager.Instance.GetAllBuildings())
            {
                WorldBuilding building = pair.Value;
                if (building != null && building.ConfigId == buildingId && building.Status == WorldBuildingStatus.Active)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendItems(StringBuilder builder, IReadOnlyList<BlueprintItem> items)
        {
            if (items == null || items.Count == 0)
            {
                builder.Append(LocalizationManager.Get("ui.common.none"));
                return;
            }

            bool appended = false;
            for (int i = 0; i < items.Count; i++)
            {
                BlueprintItem item = items[i];
                if (item == null || item.ItemId <= 0 || item.Count <= 0)
                {
                    continue;
                }

                if (appended)
                {
                    builder.Append(", ");
                }

                builder.Append(LocalizedConfigText.ItemName(item.ItemId));
                builder.Append(' ');
                builder.Append(item.Count);
                appended = true;
            }

            if (!appended)
            {
                builder.Append(LocalizationManager.Get("ui.common.none"));
            }
        }

        private static string GetBlueprintName(BlueprintConfig config)
        {
            return config != null ? LocalizedConfigText.BlueprintName(config.Id, config.Name) : string.Empty;
        }
    }
}
