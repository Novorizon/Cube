using System;
using UnityEngine;

namespace Game
{
    public sealed class BattleHudController : MonoBehaviour
    {
        [SerializeField] private TdUiConfig config;
        [SerializeField] private StatusPanel statusPanel;
        [SerializeField] private BuildTowerPanel buildTowerPanel;
        [SerializeField] private TowerInfoPanel towerInfoPanel;
        [SerializeField] private SkillPanel skillPanel;
        [SerializeField] private BattleControlPanel battleControlPanel;
        [SerializeField] private MiniMapPanel miniMapPanel;

        public event Action<int> TowerBuildClicked;
        public event Action<int> SkillClicked;
        public event Action<int> TowerUpgradeClicked;
        public event Action<int> TowerSellClicked;
        public event Action<float> SpeedChanged;
        public event Action<bool> AutoNextWaveChanged;

        private void Awake()
        {
            if (config != null)
            {
                if (buildTowerPanel != null)
                {
                    buildTowerPanel.Build(config.Towers);
                }
                if (skillPanel != null)
                {
                    skillPanel.Build(config.Skills);
                }
            }

            if (buildTowerPanel != null)
            {
                buildTowerPanel.TowerClicked += towerId => TowerBuildClicked?.Invoke(towerId);
            }
            if (skillPanel != null)
            {
                skillPanel.SkillClicked += skillId => SkillClicked?.Invoke(skillId);
            }
            if (towerInfoPanel != null)
            {
                towerInfoPanel.UpgradeClicked += towerId => TowerUpgradeClicked?.Invoke(towerId);
                towerInfoPanel.SellClicked += towerId => TowerSellClicked?.Invoke(towerId);
            }
            if (battleControlPanel != null)
            {
                battleControlPanel.SpeedChanged += speed => SpeedChanged?.Invoke(speed);
                battleControlPanel.AutoNextWaveChanged += value => AutoNextWaveChanged?.Invoke(value);
            }
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

        public void ShowTowerInfo(TdTowerRuntimeInfo info)
        {
            towerInfoPanel?.Show(info);
        }

        public void HideTowerInfo()
        {
            towerInfoPanel?.Hide();
        }

        public void SetSkillCount(int skillId, int count)
        {
            skillPanel?.SetSkillCount(skillId, count);
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
    }
}
