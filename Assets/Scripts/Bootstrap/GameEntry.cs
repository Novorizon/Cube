using Game.Framework;
using System.Threading.Tasks;
using UI;
using UnityEngine;

namespace Game
{
    public class GameEntry : MonoBehaviour
    {
        [SerializeField]
        private int startupWorldMapId = 1001;

        private void Start()
        {
            Initialize().Forget();
        }

        private async Task Initialize()
        {
            await ResourceManager.Instance.InitializeAsync();

            GameInputManager.Instance.Initialize(InputMode.World);
            CameraManager.Instance.Initialize();
            MapInputController.Instance.Initialize();

            DataManager.Instance.Initialize();
            WorldGatherManager.Instance.Initialize();
            WorldBuildingManager.Instance.Initialize();
            MineManager.Instance.Initialize();
            FarmManager.Instance.Initialize();
            WorldIncomeManager.Instance.Initialize();
            StorageManager.Instance.Initialize();
            StorageManager.Instance.Load();

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

            UIManager.Instance.ClearAll(true);
            MapManager.Instance.LoadWorldMap(startupWorldMapId);
        }

        private void Update()
        {
            WorldBuildingManager.Instance.Update();
            WorldIncomeManager.Instance.Update();
            StorageManager.Instance.Update();

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
            StorageManager.Instance.Save();
            WorldGameplayController.Shutdown();
            AbilityManager.Instance.Release();
            BattleTargetClickManager.Instance.Release();
            GameInputManager.Instance.Release();
        }
    }
}

