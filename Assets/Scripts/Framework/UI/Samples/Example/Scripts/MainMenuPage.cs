using Game;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class MainMenuPage : UIPage
    {
        [SerializeField]
        private Button enterMapButton;

        [SerializeField]
        private int mapId = 1;

        protected override void OnCreate()
        {
            if (enterMapButton != null)
            {
                enterMapButton.onClick.AddListener(OnEnterMapClicked);
            }
        }

        protected override void OnDestroyed()
        {
            if (enterMapButton != null)
            {
                enterMapButton.onClick.RemoveListener(OnEnterMapClicked);
            }
        }

        private void OnEnterMapClicked()
        {
            MapManager.Instance.LoadMap(mapId);

            // 当前先简单隐藏主菜单。
            // 后面如果你做 GamePage，可以改成：
            // await UIManager.Instance.Pages.ReplaceAsync("Assets/Data/UI/Pages/GamePage.prefab");
            gameObject.SetActive(false);
             _=UIManager.Instance.Pages.ResetToAsync("Assets/Arts/UI/Pages/BuildHudPage.prefab");
        }
    }
}