using Game.Framework;
using System;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class BuildTowerPanel : UIPanel
    {
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private GameObject cardPrefab;

        private readonly List<TowerBuildCardView> cards = new List<TowerBuildCardView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();
        private int selectedTowerConfigId;

        public event Action<int> TowerClicked;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.None;

        protected override void OnOpen(object args)
        {
            BattleItemManager.Instance.OnItemChanged += OnItemChanged;
            TowerBuildManager.Instance.SelectionChanged += OnBuildSelectionChanged;
            selectedTowerConfigId = TowerBuildManager.Instance.SelectedTowerConfigId;
            Initialize();
        }

        protected override void OnClose()
        {
            BattleItemManager.Instance.OnItemChanged -= OnItemChanged;
            TowerBuildManager.Instance.SelectionChanged -= OnBuildSelectionChanged;
            CancelSelect();
        }

        protected override void OnDestroyed()
        {
            BattleItemManager.Instance.OnItemChanged -= OnItemChanged;
            TowerBuildManager.Instance.SelectionChanged -= OnBuildSelectionChanged;
            ClearCards();
            TowerClicked = null;
        }

        public void Initialize()
        {
            if (contentRoot == null)
            {
                Debug.LogWarning("BuildTowerPanel initialize skipped. contentRoot is null.");
                return;
            }

            if (cardPrefab == null)
            {
                Debug.LogWarning("BuildTowerPanel initialize skipped. cardPrefab is null.");
                return;
            }

            if (DataManager.Instance.Tower == null)
            {
                Debug.LogWarning("BuildTowerPanel initialize skipped. tower config table is null.");
                return;
            }

            ClearCards();
            IReadOnlyDictionary<int, TowerConfig> towerConfigs = DataManager.Instance.Tower.GetAll();
            if (towerConfigs == null)
            {
                Debug.LogWarning("BuildTowerPanel initialize skipped. tower config map is null.");
                return;
            }

            int createdCount = 0;
            foreach (KeyValuePair<int, TowerConfig> pair in towerConfigs)
            {
                int towerId = pair.Key;
                TowerConfig towerConfig = pair.Value;

                if (towerConfig == null || !towerConfig.Enable)
                    continue;

                string towerName = LocalizedConfigText.TowerName(towerId);
                string description = LocalizedConfigText.TowerDescription(towerId);
                string iconLocation = towerConfig.IconLocation;
                int costCount = towerConfig.CostCount;
                int damage = towerConfig.Damage;
                if (DataManager.Instance.TryGetTowerLevel(towerId, 1, out TowerLevelConfig levelConfig))
                {
                    costCount = levelConfig.BuildCost;
                    damage = levelConfig.Damage;
                }

                Sprite icon = LoadIcon(iconLocation, "Tower");
                IReadOnlyList<SkillConfig> skills = GetTowerSkills(towerId);
                GameObject go = Instantiate(cardPrefab, contentRoot);
                TowerBuildCardView card = go?.GetComponent<TowerBuildCardView>();
                if (card == null)
                {
                    if (go != null)
                    {
                        Destroy(go);
                    }

                    Debug.LogError($"Build tower card prefab is missing {nameof(TowerBuildCardView)}.", cardPrefab);
                    continue;
                }

                card.Init(towerId, towerName, description, costCount, damage, OnTowerCardClicked);
                card.SetIcon(icon);
                card.SetSkills(skills, location => LoadIcon(location, "Tower skill"));
                card.SetSelected(towerId == selectedTowerConfigId);
                card.SetAffordable(TowerManager.Instance.HasGold(towerId));
                cards.Add(card);
                createdCount++;
            }

            if (createdCount == 0)
            {
                Debug.LogWarning("BuildTowerPanel initialized, but no enabled tower configs were found.");
            }
        }

        public void SetSelectedTower(int towerConfigId)
        {
            selectedTowerConfigId = towerConfigId;
            RefreshSelected();
        }

        public void ClearSelected()
        {
            selectedTowerConfigId = 0;
            RefreshSelected();
        }

        public void CancelSelect()
        {
            OnCancelButtonClicked();
        }

        private void OnTowerCardClicked(int towerConfigId)
        {
            SelectTower(towerConfigId);
        }

        private void SelectTower(int towerConfigId)
        {
            if (!TowerManager.Instance.HasGold(towerConfigId))
            {
                Toast.Warning(LocalizationManager.Get("ui.td.toast.not_enough_gold"));
                return;
            }

            TowerBuildManager.Instance.SelectTower(towerConfigId);
            TowerBuildInputController.Instance.RefreshPreviewAtCurrentPointer();
            TowerClicked?.Invoke(towerConfigId);
        }

        private void OnCancelButtonClicked()
        {
            TowerBuildManager.Instance.CancelSelect();
        }

        private void OnBuildSelectionChanged(int towerConfigId)
        {
            selectedTowerConfigId = towerConfigId;
            RefreshSelected();
        }

        private void RefreshSelected()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                TowerBuildCardView card = cards[i];
                if (card == null)
                {
                    continue;
                }

                card.SetSelected(card.TowerId == selectedTowerConfigId);
            }
        }

        private void OnItemChanged(int itemId, int count)
        {
            if (itemId != ItemIds.Gold)
            {
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                TowerBuildCardView card = cards[i];
                if (card != null)
                {
                    card.SetAffordable(TowerManager.Instance.HasGold(card.TowerId));
                }
            }
        }

        private void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    Destroy(cards[i].gameObject);
                }
            }

            cards.Clear();
        }

        private IReadOnlyList<SkillConfig> GetTowerSkills(int towerId)
        {
            IReadOnlyDictionary<int, TowerLevelConfig> levelConfigs = DataManager.Instance.TowerLevel?.GetAll();
            if (levelConfigs == null || DataManager.Instance.Skill == null)
            {
                return Array.Empty<SkillConfig>();
            }

            List<TowerLevelConfig> towerLevels = new List<TowerLevelConfig>();
            foreach (KeyValuePair<int, TowerLevelConfig> pair in levelConfigs)
            {
                TowerLevelConfig levelConfig = pair.Value;
                if (levelConfig != null && levelConfig.Enable && levelConfig.TowerId == towerId && levelConfig.SkillId > 0)
                {
                    towerLevels.Add(levelConfig);
                }
            }

            if (towerLevels.Count == 0)
            {
                return Array.Empty<SkillConfig>();
            }

            towerLevels.Sort(CompareTowerLevel);

            List<SkillConfig> skills = new List<SkillConfig>();
            HashSet<int> skillIds = new HashSet<int>();
            for (int i = 0; i < towerLevels.Count; i++)
            {
                int skillId = towerLevels[i].SkillId;
                if (!skillIds.Add(skillId))
                {
                    continue;
                }

                if (DataManager.Instance.Skill.TryGet(skillId, out SkillConfig skillConfig) && skillConfig != null && skillConfig.Enable)
                {
                    skills.Add(skillConfig);
                }
            }

            return skills;
        }

        private static int CompareTowerLevel(TowerLevelConfig left, TowerLevelConfig right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.Level.CompareTo(right.Level);
        }

        private Sprite LoadIcon(string location, string iconKind)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            if (!location.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (missingIconWarnings.Add(location))
                {
                    Debug.LogWarning($"{iconKind} icon location must be a full asset path. location: {location}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(location);
            if (sprite == null && missingIconWarnings.Add(location))
            {
                Debug.LogWarning($"{iconKind} icon load failed. location: {location}");
            }

            return sprite;
        }
    }
}
