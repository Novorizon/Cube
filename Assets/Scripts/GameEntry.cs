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
            DataManager.Instance.Initialize();
            NpcConfig npc = DataManager.Instance.GetNpcConfig(1001);
            EnemySpawner.Instance.Initialize();
            BaseManager.Instance.Initialize(20);
            EnsureEnemyUpdateDriver();

            MapManager.Instance.Initialize();
            TowerBuildManager.Instance.Initialize();
            TowerBuildInputController.Instance.Initialize();

            UIManager.Instance.UseResourceManagerLoader();

            await UIManager.Instance.Pages.ResetToAsync(mainMenuPagePath);
        }
        private void OnDestroy()
        {
            GameInputManager.Instance.Release();
        }
        private void EnsureEnemyUpdateDriver()
        {
            EnemyUpdateDriver driver = FindObjectOfType<EnemyUpdateDriver>();

            if (driver != null)
            {
                return;
            }

            GameObject driverObject = new GameObject("EnemyUpdateDriver");
            driverObject.AddComponent<EnemyUpdateDriver>();
        }
    }
}