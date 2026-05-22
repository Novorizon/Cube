using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UI;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Game
{
    public sealed class BuildTowerPanel : UIPanel
    {
        private const int NormalTowerConfigId = 1001;
        private const int IceTowerConfigId = 1003;

        //[SerializeField]
        //private Button normalTowerButton;

        //[SerializeField]
        //private Button iceTowerButton;

        //[SerializeField]
        //private Button cancelButton;

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private TowerBuildCardView cardPrefab;

        private readonly List<TowerBuildCardView> cards = new List<TowerBuildCardView>();
        private int selectedTowerConfigId;

        public event Action<int> TowerClicked;

        protected override void OnCreate()
        {
            //if (normalTowerButton != null)
            //{
            //    normalTowerButton.onClick.AddListener(OnNormalTowerButtonClicked);
            //}

            //if (iceTowerButton != null)
            //{
            //    iceTowerButton.onClick.AddListener(OnIceTowerButtonClicked);
            //}

            //if (cancelButton != null)
            //{
            //    cancelButton.onClick.AddListener(OnCancelButtonClicked);
            //}
        }

        protected override void OnDestroyed()
        {
            //if (normalTowerButton != null)
            //{
            //    normalTowerButton.onClick.RemoveListener(OnNormalTowerButtonClicked);
            //}

            //if (iceTowerButton != null)
            //{
            //    iceTowerButton.onClick.RemoveListener(OnIceTowerButtonClicked);
            //}

            //if (cancelButton != null)
            //{
            //    cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
            //}

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
            SpriteAtlas atlas = ResourceManager.Instance.LoadSpriteAtlas("Assets/Arts/UI/Atlas/TowerDefense.spriteatlasv2");
            Sprite[] sprites = new Sprite[atlas.spriteCount];
            int count = atlas.GetSprites(sprites);

            for (int i = 0; i < count; i++)
            {
                Sprite sprite = sprites[i];

                if (sprite == null)
                {
                    continue;
                }

                string spriteName = sprite.name.Replace("(Clone)", "");
                Debug.Log($"Atlas sprite: {spriteName}");
            }
            foreach (KeyValuePair<int, TowerConfig> pair in DataManager.Instance.Tower.GetAll())
            {
                int towerId = pair.Key;
                TowerConfig towerConfig = pair.Value;

                if (!towerConfig.Enable)
                    continue;

                string towerName = towerConfig.Name;
                string iconLocation = towerConfig.IconLocation;
                int costItemId = towerConfig.CostItemId;
                int costCount = towerConfig.CostCount;
                towerName = towerConfig.Name;

                Sprite icon = atlas.GetSprite(iconLocation); ;
                TowerBuildCardView card = Instantiate(cardPrefab, contentRoot);
                card.Init(towerId, towerName, costCount, OnTowerCardClicked);
                card.SetIcon(icon);
                card.SetSelected(towerId == selectedTowerConfigId);
                cards.Add(card);
                Debug.Log($"TowerId: {towerId}, Name: {towerConfig.Name}, Cost: {towerConfig.CostCount}");
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

        private void OnNormalTowerButtonClicked()
        {
            SelectTower(NormalTowerConfigId);
        }

        private void OnIceTowerButtonClicked()
        {
            SelectTower(IceTowerConfigId);
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
    }
}
