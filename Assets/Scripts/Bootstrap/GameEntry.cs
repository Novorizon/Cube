using Game.Framework;
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
            await ResourceManager.Instance.InitializeAsync();

            GameInputManager.Instance.Initialize(InputMode.Gameplay);
            CameraManager.Instance.Initialize();
            MapInputController.Instance.Initialize();

            DataManager.Instance.Initialize();

            BaseManager.Instance.Initialize();
            NpcManager.Instance.Initialize();
            TowerManager.Instance.Initialize();
            WaveManager.Instance.Initialize();
            AbilityManager.Instance.Initialize();
            BattleFlowManager.Instance.Initialize();

            MapManager.Instance.Initialize();
            TowerBuildManager.Instance.Initialize();
            TowerBuildInputController.Instance.Initialize();
            BattleTargetClickManager.Instance.Initialize();

            UIManager.Instance.UseResourceManagerLoader();

            await UIManager.Instance.Pages.ResetToAsync(mainMenuPagePath);
        }

        private void Update()
        {
            if (!BattleFlowManager.Instance.IsRunning)
            {
                return;
            }

            AbilityManager.Instance.Update(Time.deltaTime);
            NpcManager.Instance.Update(Time.deltaTime);

            if (!BattleFlowManager.Instance.IsRunning)
            {
                return;
            }

            TowerManager.Instance.Update(Time.deltaTime);

            if (!BattleFlowManager.Instance.IsRunning)
            {
                return;
            }

            WaveManager.Instance.Update(Time.deltaTime);
        }

        private void OnDestroy()
        {
            AbilityManager.Instance.Release();
            BattleTargetClickManager.Instance.Release();
            GameInputManager.Instance.Release();
        }
    }
}
