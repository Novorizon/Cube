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

            if (DataManager.Instance.TechNode == null ||
                !DataManager.Instance.TechNode.TryGet(techId, out TechNodeConfig config) ||
                config == null ||
                !config.Enable)
            {
                return false;
            }

            return config.DefaultUnlocked || researchedTechIds.Contains(techId);
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

            if (TryGetUnmetPrerequisite(config, out int prerequisiteTechId))
            {
                return LocalizationManager.Format("ui.tech.require.prerequisite", GetTechName(prerequisiteTechId));
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

            if (TryGetUnmetPrerequisite(config, out int prerequisiteTechId))
            {
                reason = LocalizationManager.Format("ui.tech.reason.need_prerequisite", GetTechName(prerequisiteTechId));
                return TechResearchState.LockedByPrerequisite;
            }

            IReadOnlyList<ItemStack> costs = GetCosts(config.CostGroupId);
            if (config.CostGroupId > 0 && costs.Count == 0)
            {
                reason = LocalizationManager.Get("ui.tech.reason.missing_cost_config");
                return TechResearchState.MissingCostConfig;
            }

            if (!ItemManager.Instance.HasItems(costs))
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

            IReadOnlyList<ItemStack> costs = GetCosts(config.CostGroupId);
            if (!ItemManager.Instance.TryConsumeItems(costs))
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

            bool removedInvalidUnlocks = RemoveInvalidResearchUnlocks();
            Revision++;
            if (removedInvalidUnlocks)
            {
                StorageManager.Instance.MarkDirty();
            }
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

        private bool TryGetUnmetPrerequisite(TechNodeConfig config, out int prerequisiteTechId)
        {
            prerequisiteTechId = 0;
            if (config == null)
            {
                return false;
            }

            int currentPrerequisiteId = config.PreTechId;
            HashSet<int> visitedTechIds = null;
            while (currentPrerequisiteId > 0)
            {
                if (visitedTechIds == null)
                {
                    visitedTechIds = new HashSet<int>();
                }

                if (!visitedTechIds.Add(currentPrerequisiteId))
                {
                    prerequisiteTechId = currentPrerequisiteId;
                    return true;
                }

                if (!IsResearched(currentPrerequisiteId))
                {
                    prerequisiteTechId = currentPrerequisiteId;
                    return true;
                }

                if (DataManager.Instance.TechNode == null ||
                    !DataManager.Instance.TechNode.TryGet(currentPrerequisiteId, out TechNodeConfig prerequisiteConfig) ||
                    prerequisiteConfig == null ||
                    !prerequisiteConfig.Enable)
                {
                    prerequisiteTechId = currentPrerequisiteId;
                    return true;
                }

                currentPrerequisiteId = prerequisiteConfig.PreTechId;
            }

            return false;
        }

        private bool RemoveInvalidResearchUnlocks()
        {
            bool removedAny = false;
            bool removedThisPass;
            List<int> invalidTechIds = new List<int>();

            do
            {
                removedThisPass = false;
                invalidTechIds.Clear();

                foreach (int techId in researchedTechIds)
                {
                    if (!IsValidSavedResearch(techId))
                    {
                        invalidTechIds.Add(techId);
                    }
                }

                for (int i = 0; i < invalidTechIds.Count; i++)
                {
                    removedThisPass |= researchedTechIds.Remove(invalidTechIds[i]);
                }

                removedAny |= removedThisPass;
            }
            while (removedThisPass);

            return removedAny;
        }

        private bool IsValidSavedResearch(int techId)
        {
            if (DataManager.Instance.TechNode == null ||
                !DataManager.Instance.TechNode.TryGet(techId, out TechNodeConfig config) ||
                config == null ||
                !config.Enable ||
                config.DefaultUnlocked)
            {
                return false;
            }

            return !TryGetUnmetPrerequisite(config, out _);
        }

        private IReadOnlyList<ItemStack> GetCosts(int costGroupId)
        {
            if (costGroupId <= 0 || costResolver == null)
            {
                return System.Array.Empty<ItemStack>();
            }

            return costResolver.GetCostGroup(costGroupId);
        }

        private string FormatMissingCosts(IReadOnlyList<ItemStack> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return LocalizationManager.Get("ui.common.none");
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < costs.Count; i++)
            {
                ItemStack cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                int current = ItemManager.Instance.GetCount(cost.ItemId);
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
