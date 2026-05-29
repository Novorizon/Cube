using Game.Framework;
using System;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class BuildTowerPanel : UIPanel
    {
        private const string TowerCardSkillPrefabPath = "Assets/Arts/UI/TowerDefense/Prefabs/TowerCardSkill.prefab";

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private GameObject cardPrefab;

        private readonly List<TowerBuildCardView> cards = new List<TowerBuildCardView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();
        private GameObject towerCardSkillPrefab;
        private int selectedTowerConfigId;

        public event Action<int> TowerClicked;

        protected override void OnCreate()
        {
        }

        protected override void OnDestroyed()
        {
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

                string towerName = towerConfig.Name;
                string iconLocation = towerConfig.IconLocation;
                int costCount = towerConfig.CostCount;
                if (DataManager.Instance.TryGetTowerLevel(towerId, 1, out TowerLevelConfig levelConfig))
                {
                    costCount = levelConfig.BuildCost;
                }

                Sprite icon = LoadIcon(iconLocation, "Tower");
                IReadOnlyList<SkillConfig> skills = GetTowerSkills(towerId);
                GameObject go = Instantiate(cardPrefab, contentRoot);
                TowerBuildCardView card = go?.GetComponent<TowerBuildCardView>();
                card.Init(towerId, towerName, costCount, OnTowerCardClicked);
                card.SetIcon(icon);
                card.SetSkills(skills, skills.Count > 0 ? GetTowerCardSkillPrefab() : null, location => LoadIcon(location, "Tower skill"));
                card.SetSelected(towerId == selectedTowerConfigId);
                cards.Add(card);
                createdCount++;
                Debug.Log($"TowerId: {towerId}, Name: {towerConfig.Name}, Cost: {costCount}");
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
                Toast.Warning("金币不足");
                return;
            }

            selectedTowerConfigId = towerConfigId;
            RefreshSelected();

            TowerBuildManager.Instance.SelectTower(towerConfigId);
            TowerBuildInputController.Instance.RefreshPreviewAtCurrentPointer();
            TowerClicked?.Invoke(towerConfigId);
        }

        private void OnCancelButtonClicked()
        {
            selectedTowerConfigId = 0;
            RefreshSelected();

            TowerBuildManager.Instance.CancelSelect();
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

        private GameObject GetTowerCardSkillPrefab()
        {
            if (towerCardSkillPrefab != null)
            {
                return towerCardSkillPrefab;
            }

            towerCardSkillPrefab = ResourceManager.Instance.LoadGameObject(TowerCardSkillPrefabPath);
            if (towerCardSkillPrefab == null)
            {
                Debug.LogWarning($"Tower card skill prefab load failed. location: {TowerCardSkillPrefabPath}");
            }

            return towerCardSkillPrefab;
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
