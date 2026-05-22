using System;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class BattleHudController : UIPanel
    {
        [SerializeField]
        private TdUiConfig config;

        [SerializeField]
        private StatusPanel statusPanel;

        [SerializeField]
        private BuildTowerPanel buildTowerPanel;

        [SerializeField]
        private ItemPanel itemPanel;

        [SerializeField]
        private InfoPanel targetInfoPanel;

        [SerializeField]
        private SkillPanel skillPanel;

        [SerializeField]
        private BattleControlPanel battleControlPanel;

        [SerializeField]
        private MiniMapPanel miniMapPanel;

        public event Action<int> TowerBuildClicked;
        public event Action<int> SkillClicked;
        public event Action<int> TowerUpgradeClicked;
        public event Action<int> TowerSellClicked;
        public event Action<float> SpeedChanged;
        public event Action<bool> AutoNextWaveChanged;
        public event Action<int> ItemClicked;

        protected override void OnCreate()
        {
            InitializePanels();
            RegisterEvents();
        }

        protected override void OnDestroyed()
        {
            UnregisterEvents();
        }

        public void InitializePanels()
        {
            if (config == null)
            {
                return;
            }

            if (buildTowerPanel != null)
            {
                buildTowerPanel.Initialize();
            }

            if (skillPanel != null)
            {
                skillPanel.Build(config.Skills);
            }

            if (itemPanel != null)
            {
                itemPanel.Build();
            }

            if (targetInfoPanel != null)
            {
                targetInfoPanel.Initialize();
            }
        }

        private void RegisterEvents()
        {
            if (buildTowerPanel != null)
            {
                buildTowerPanel.TowerClicked += OnTowerBuildClicked;
            }

            if (skillPanel != null)
            {
                skillPanel.SkillClicked += OnSkillClicked;
            }

            if (targetInfoPanel != null)
            {
                targetInfoPanel.UpgradeClicked += OnTowerUpgradeClicked;
                targetInfoPanel.SellClicked += OnTowerSellClicked;
            }

            if (battleControlPanel != null)
            {
                battleControlPanel.SpeedChanged += OnSpeedChanged;
                battleControlPanel.AutoNextWaveChanged += OnAutoNextWaveChanged;
            }

            if (itemPanel != null)
            {
                itemPanel.ItemClicked += OnItemClicked;
            }
        }

        private void UnregisterEvents()
        {
            if (buildTowerPanel != null)
            {
                buildTowerPanel.TowerClicked -= OnTowerBuildClicked;
            }

            if (skillPanel != null)
            {
                skillPanel.SkillClicked -= OnSkillClicked;
            }

            if (targetInfoPanel != null)
            {
                targetInfoPanel.UpgradeClicked -= OnTowerUpgradeClicked;
                targetInfoPanel.SellClicked -= OnTowerSellClicked;
            }

            if (battleControlPanel != null)
            {
                battleControlPanel.SpeedChanged -= OnSpeedChanged;
                battleControlPanel.AutoNextWaveChanged -= OnAutoNextWaveChanged;
            }

            if (itemPanel != null)
            {
                itemPanel.ItemClicked -= OnItemClicked;
            }

            TowerBuildClicked = null;
            SkillClicked = null;
            TowerUpgradeClicked = null;
            TowerSellClicked = null;
            SpeedChanged = null;
            AutoNextWaveChanged = null;
            ItemClicked = null;
        }

        public void SetBaseLife(int current, int max)
        {
            statusPanel?.SetBaseLife(current, max);
        }

        public void SetGold(int gold)
        {
            statusPanel?.SetGold(gold);
        }

        public void SetWave(int currentWave, int totalWave)
        {
            statusPanel?.SetWave(currentWave, totalWave);
        }

        public void SetEnemyCount(int alive, int total)
        {
            statusPanel?.SetEnemyCount(alive, total);
        }

        public void ShowTargetInfo(TdTargetRuntimeInfo info)
        {
            targetInfoPanel?.Show(info);
        }

        public void ClearTargetInfo()
        {
            targetInfoPanel?.ClearInfo();
        }

        public void ShowTowerInfo(TdTowerRuntimeInfo info)
        {
            targetInfoPanel?.ShowTower(info);
        }

        public void HideTowerInfo()
        {
            ClearTargetInfo();
        }

        public void SetSkillCount(int skillId, int count)
        {
            skillPanel?.SetSkillCount(skillId, count);
        }

        public void SetItemCount(int itemId, int count)
        {
            itemPanel?.SetItemCount(itemId, count);
        }

        public void SetMiniMapBounds(Vector2 min, Vector2 max)
        {
            miniMapPanel?.SetMapBounds(min, max);
        }

        public void ClearMiniMap()
        {
            miniMapPanel?.Clear();
        }

        public void AddMiniMapIcon(Vector2 mapPosition, MiniMapIconType type)
        {
            miniMapPanel?.AddIcon(mapPosition, type);
        }

        private void OnTowerBuildClicked(int towerId)
        {
            TowerBuildClicked?.Invoke(towerId);
        }

        private void OnSkillClicked(int skillId)
        {
            SkillClicked?.Invoke(skillId);
        }

        private void OnTowerUpgradeClicked(int towerId)
        {
            TowerUpgradeClicked?.Invoke(towerId);
        }

        private void OnTowerSellClicked(int towerId)
        {
            TowerSellClicked?.Invoke(towerId);
        }

        private void OnSpeedChanged(float speed)
        {
            SpeedChanged?.Invoke(speed);
        }

        private void OnAutoNextWaveChanged(bool value)
        {
            AutoNextWaveChanged?.Invoke(value);
        }

        private void OnItemClicked(int itemId)
        {
            ItemClicked?.Invoke(itemId);
        }
    }
}
