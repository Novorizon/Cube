using Game.Framework;
using System;
using System.Threading.Tasks;
using UI;
using UnityEngine;

namespace Game
{
    public class GameEntry : MonoBehaviour
    {
        [SerializeField]
        private string mainMenuPagePath = "Assets/Arts/UI/Pages/MainMenuPage.prefab";

        private void Start()
        {
            Initialize().Forget();
        }

        private async Task Initialize()
        {
            GameInputManager.Instance.Initialize(InputMode.Gameplay);
            CameraManager.Instance.Initialize();
            MapInputController.Instance.Initialize();
            await ResourceManager.Instance.InitializeAsync();

            bool mapInitialized = MapManager.Instance.Initialize();

            if (!mapInitialized)
            {
                Debug.LogError("MapManager initialize failed.");
            }

            bool towerBuildInitialized = TowerBuildManager.Instance.Initialize();

            if (!towerBuildInitialized)
            {
                Debug.LogError("TowerBuildManager initialize failed.");
            }

            TowerBuildInputController.Instance.Initialize();

            UIManager.Instance.UseResourceManagerLoader();

            await UIManager.Instance.Pages.ResetToAsync(mainMenuPagePath);
        }
        private void OnDestroy()
        {
            GameInputManager.Instance.Release();
        }
    }
}