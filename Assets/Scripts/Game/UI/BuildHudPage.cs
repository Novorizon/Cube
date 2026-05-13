using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BuildHudPage : UIPage
    {
        [SerializeField]
        private Button normalTowerButton;

        [SerializeField]
        private Button iceTowerButton;

        [SerializeField]
        private Button cancelButton;

        private void Awake()
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

        private void OnDestroy()
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
            TowerBuildManager.Instance.SelectTower(TowerType.Normal);
        }

        private void OnIceTowerButtonClicked()
        {
            TowerBuildManager.Instance.SelectTower(TowerType.Ice);
        }

        private void OnCancelButtonClicked()
        {
            TowerBuildManager.Instance.CancelSelect();
        }
    }
}
