using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BuildTowerPanel : UIPanel
    {
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private TowerBuildCardView cardPrefab;

        private readonly List<TowerBuildCardView> cards = new List<TowerBuildCardView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();
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
            if (contentRoot == null || cardPrefab == null)
            {
                return;
            }

            ClearCards();
            foreach (KeyValuePair<int, TowerConfig> pair in DataManager.Instance.Tower.GetAll())
            {
                int towerId = pair.Key;
                TowerConfig towerConfig = pair.Value;

                if (!towerConfig.Enable)
                    continue;

                string towerName = towerConfig.Name;
                string iconLocation = towerConfig.IconLocation;
                int costCount = towerConfig.CostCount;
                if (DataManager.Instance.TryGetTowerLevel(towerId, 1, out TowerLevelConfig levelConfig))
                {
                    costCount = levelConfig.BuildCost;
                }
                towerName = towerConfig.Name;

                Sprite icon = LoadIcon(iconLocation);
                TowerBuildCardView card = Instantiate(cardPrefab, contentRoot);
                card.Init(towerId, towerName, costCount, OnTowerCardClicked);
                card.SetIcon(icon);
                card.SetSelected(towerId == selectedTowerConfigId);
                cards.Add(card);
                Debug.Log($"TowerId: {towerId}, Name: {towerConfig.Name}, Cost: {costCount}");
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

        private Sprite LoadIcon(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            if (!location.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (missingIconWarnings.Add(location))
                {
                    Debug.LogWarning($"Tower icon location must be a full asset path. location: {location}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(location);
            if (sprite == null && missingIconWarnings.Add(location))
            {
                Debug.LogWarning($"Tower icon load failed. location: {location}");
            }

            return sprite;
        }
    }
}
