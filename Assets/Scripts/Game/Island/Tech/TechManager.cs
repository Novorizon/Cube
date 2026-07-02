using Game.Framework;
using System.Collections.Generic;

namespace Game
{
    public sealed class TechManager
    {
        public static TechManager Instance { get; } = new TechManager();

        private readonly HashSet<int> researchedTechIds = new HashSet<int>();
        private readonly Dictionary<int, TechNodeConfig> buildingUnlockTechs = new Dictionary<int, TechNodeConfig>();
        private WorldCostResolver costResolver;

        public int Revision { get; private set; }

        private TechManager()
        {
        }

        public void Initialize()
        {
            researchedTechIds.Clear();
            buildingUnlockTechs.Clear();
            Revision = 0;
            costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);
            BuildUnlockIndex();
        }

        public bool IsResearched(int techId)
        {
            if (techId <= 0)
            {
                return false;
            }

            if (researchedTechIds.Contains(techId))
            {
                return true;
            }

            return DataManager.Instance.TechNode != null &&
                   DataManager.Instance.TechNode.TryGet(techId, out TechNodeConfig config) &&
                   config != null &&
                   config.Enable &&
                   config.DefaultUnlocked;
        }

        public bool IsBuildingUnlockedByTech(int buildingId)
        {
            return TryGetBuildingUnlockTech(buildingId, out TechNodeConfig config) &&
                   IsResearched(config.Id);
        }

        public bool TryGetBuildingUnlockTech(int buildingId, out TechNodeConfig config)
        {
            if (buildingId <= 0)
            {
                config = null;
                return false;
            }

            if (buildingUnlockTechs.TryGetValue(buildingId, out config) &&
                config != null &&
                config.Enable)
            {
                return true;
            }

            config = null;
            return false;
        }

        public string GetBuildingUnlockRequirementText(int buildingId)
        {
            if (!TryGetBuildingUnlockTech(buildingId, out TechNodeConfig config))
            {
                return string.Empty;
            }

            if (IsResearched(config.Id))
            {
                return string.Empty;
            }

            if (config.PreTechId > 0 && !IsResearched(config.PreTechId))
            {
                return LocalizationManager.Format("ui.tech.require.prerequisite", GetTechName(config.PreTechId));
            }

            return LocalizationManager.Format("ui.tech.require.tech", GetTechName(config.Id));
        }

        public TechResearchState GetResearchState(TechNodeConfig config, out string reason)
        {
            reason = string.Empty;
            if (config == null)
            {
                reason = LocalizationManager.Get("ui.tech.reason.missing_config");
                return TechResearchState.Invalid;
            }

            if (!config.Enable)
            {
                reason = LocalizationManager.Get("ui.tech.reason.disabled");
                return TechResearchState.Disabled;
            }

            if (IsResearched(config.Id))
            {
                reason = LocalizationManager.Get("ui.tech.reason.already_unlocked");
                return TechResearchState.Researched;
            }

            if (config.PreTechId > 0 && !IsResearched(config.PreTechId))
            {
                reason = LocalizationManager.Format("ui.tech.reason.need_prerequisite", GetTechName(config.PreTechId));
                return TechResearchState.LockedByPrerequisite;
            }

            IReadOnlyList<WorldItem> costs = GetCosts(config.CostGroupId);
            if (config.CostGroupId > 0 && costs.Count == 0)
            {
                reason = LocalizationManager.Get("ui.tech.reason.missing_cost_config");
                return TechResearchState.MissingCostConfig;
            }

            if (!WorldItemManager.Instance.HasItems(costs))
            {
                reason = LocalizationManager.Format("ui.tech.reason.not_enough_cost", FormatMissingCosts(costs));
                return TechResearchState.NotEnoughCost;
            }

            return TechResearchState.CanResearch;
        }

        public bool CanResearch(TechNodeConfig config, out string reason)
        {
            return GetResearchState(config, out reason) == TechResearchState.CanResearch;
        }

        public bool TryResearch(int techId, out string reason)
        {
            reason = string.Empty;
            if (DataManager.Instance.TechNode == null ||
                !DataManager.Instance.TechNode.TryGet(techId, out TechNodeConfig config))
            {
                reason = LocalizationManager.Get("ui.tech.reason.missing_config");
                return false;
            }

            if (!CanResearch(config, out reason))
            {
                return false;
            }

            IReadOnlyList<WorldItem> costs = GetCosts(config.CostGroupId);
            if (!WorldItemManager.Instance.TryConsumeItems(costs))
            {
                reason = LocalizationManager.Format("ui.tech.reason.not_enough_cost", FormatMissingCosts(costs));
                return false;
            }

            researchedTechIds.Add(techId);
            Revision++;
            StorageManager.Instance.MarkDirty();
            Messager.Instance.Notify(WorldMessageTopic.TechChanged, new TechChangedMessage
            {
                TechId = techId,
                FullRefresh = false
            });
            return true;
        }

        public void LoadSaveData(SaveTechData data)
        {
            researchedTechIds.Clear();
            if (data?.ResearchedTechIds == null)
            {
                Revision++;
                return;
            }

            for (int i = 0; i < data.ResearchedTechIds.Length; i++)
            {
                int techId = data.ResearchedTechIds[i];
                if (techId > 0)
                {
                    researchedTechIds.Add(techId);
                }
            }

            Revision++;
        }

        public SaveTechData CreateSaveData()
        {
            int[] ids = new int[researchedTechIds.Count];
            researchedTechIds.CopyTo(ids);
            System.Array.Sort(ids);
            return new SaveTechData
            {
                ResearchedTechIds = ids
            };
        }

        private void BuildUnlockIndex()
        {
            IReadOnlyDictionary<int, TechNodeConfig> configs = DataManager.Instance.TechNode?.GetAll();
            if (configs == null)
            {
                return;
            }

            foreach (KeyValuePair<int, TechNodeConfig> pair in configs)
            {
                TechNodeConfig config = pair.Value;
                if (config != null && config.Enable && config.UnlockBuildingId > 0)
                {
                    buildingUnlockTechs[config.UnlockBuildingId] = config;
                }
            }
        }

        private IReadOnlyList<WorldItem> GetCosts(int costGroupId)
        {
            if (costGroupId <= 0 || costResolver == null)
            {
                return System.Array.Empty<WorldItem>();
            }

            return costResolver.GetCostGroup(costGroupId);
        }

        private string FormatMissingCosts(IReadOnlyList<WorldItem> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return LocalizationManager.Get("ui.common.none");
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                WorldItem cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                int current = WorldItemManager.Instance.GetCount(cost.ItemId);
                if (current >= cost.Count)
                {
                    continue;
                }

                parts.Add($"{GetItemName(cost.ItemId)} {current}/{cost.Count}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : LocalizationManager.Get("ui.common.none");
        }

        private string GetTechName(int techId)
        {
            return LocalizedConfigText.TechName(techId);
        }

        private string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }
    }
}
