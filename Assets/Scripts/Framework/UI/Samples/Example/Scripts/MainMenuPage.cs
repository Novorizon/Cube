using UnityEngine;
using UnityEngine.UI;

namespace UI.Sample
{
    public sealed class MainMenuPage : UIPage
    {
        [SerializeField] Button? openSettingsButton;
        [SerializeField] Button? openSidePanelButton;
        [SerializeField] Button? showToastButton;
        [SerializeField] Button? showLoadingButton;

        protected override void OnCreate()
        {
            if (openSettingsButton != null)
            {
                openSettingsButton.onClick.AddListener(OnOpenSettingsClicked);
            }

            if (openSidePanelButton != null)
            {
                openSidePanelButton.onClick.AddListener(OnToggleSidePanelClicked);
            }

            if (showToastButton != null)
            {
                showToastButton.onClick.AddListener(OnShowToastClicked);
            }

            if (showLoadingButton != null)
            {
                showLoadingButton.onClick.AddListener(OnShowLoadingClicked);
            }
        }

        void OnOpenSettingsClicked()
        {
            _ = UIRoot.Instance.Popups.OpenAsync("UI/Popups/SettingsPopup");
        }

        void OnToggleSidePanelClicked()
        {
            _ = UIRoot.Instance.Panels.ToggleAsync("UI/Panels/SidePanel");
        }

        void OnShowToastClicked()
        {
            UIRoot.Instance.Toasts.Enqueue("UI/Toasts/SimpleToast", "Saved!");
        }

        async void OnShowLoadingClicked()
        {
            using var token = await UIRoot.Instance.Overlays.ShowBlockingAsync("UI/Overlays/LoadingOverlay");
            await System.Threading.Tasks.Task.Delay(1200);
        }
    }
}
