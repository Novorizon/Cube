using UI;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BuildTowerPanel : UIPanel
    {
        private const int NormalTowerConfigId = 1001;
        private const int IceTowerConfigId = 1003;

        [SerializeField]
        private Button normalTowerButton;

        [SerializeField]
        private Button iceTowerButton;

        [SerializeField]
        private Button cancelButton;

        protected override void OnCreate()
        {
            if (normalTowerButton != null)
            {
                normalTowerButton.onClick.AddListener(OnNormalTowerButtonClicked);
            }

            if (iceTowerButton != null)
            {
                iceTowerButton.onClick.AddListener(OnIceTowerButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
            }
        }

        protected override void OnDestroyed()
        {
            if (normalTowerButton != null)
            {
                normalTowerButton.onClick.RemoveListener(OnNormalTowerButtonClicked);
            }

            if (iceTowerButton != null)
            {
                iceTowerButton.onClick.RemoveListener(OnIceTowerButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
            }
        }

        private void OnNormalTowerButtonClicked()
        {
            if (!DataManager.Instance.Tower.TryGet(NormalTowerConfigId, out TowerConfig config))
            {
                Debug.LogWarning($"Select tower failed. Missing tower config: {NormalTowerConfigId}");
                return;
            }
            int gold=ItemManager.Instance.GetCount(ItemIds.Gold);
            if(gold< config.CostCount)
            {
                Debug.LogWarning($"Gold is not enought: {gold}");
                return;
            }
            TowerBuildManager.Instance.SelectTower(NormalTowerConfigId);
        }

        private void OnIceTowerButtonClicked()
        {
            if (!TowerManager.Instance.HasGold(IceTowerConfigId))
            {
                Toast.Warning("½ð±Ò²»×ã");
                return;
            }
            TowerBuildManager.Instance.SelectTower(IceTowerConfigId);
        }

        private void OnCancelButtonClicked()
        {
            TowerBuildManager.Instance.CancelSelect();
        }
    }
}
