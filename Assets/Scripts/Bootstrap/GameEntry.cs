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
            TechManager.Instance.Initialize();
            WorldGatherManager.Instance.Initialize();
            WorldBuildingManager.Instance.Initialize();
            MineManager.Instance.Initialize();
            FarmManager.Instance.Initialize();
            BlueprintManager.Instance.Initialize();
            QuestManager.Instance.Initialize();
            StoryManager.Instance.Initialize();
            ToolKitManager.Instance.Initialize();
            BagManager.Instance.Initialize();
            CalendarManager.Instance.Initialize();
            WorldIncomeManager.Instance.Initialize();
            StorageManager.Instance.Initialize();
            StorageManager.Instance.Load();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GmCommandController.EnsureExists();
#endif

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
            QuestToastListener.Instance.Initialize();

            UIManager.Instance.ClearAll(true);
            if (MapManager.Instance.LoadWorldMap(startupWorldMapId))
            {
                StoryManager.Instance.TryStartAutoStories();
            }
        }

        private void Update()
        {
            CalendarManager.Instance.Update(Time.unscaledDeltaTime);
            WorldBuildingManager.Instance.Update();
            WorldIncomeManager.Instance.Update();
            StorageManager.Instance.Update();

            if (!BattleFlowManager.Instance.IsRunning)
            {
                return;
            }

            AbilityManager.Instance.Update(Time.deltaTime);
            NpcManager.Instance.Update(Time.deltaTime);
            WorldHpBarManager.Instance.Update();

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
            StoryManager.Instance.Release();
            QuestToastListener.Instance.Release();
            BagManager.Instance.Release();
            GameplayController.Shutdown();
            AbilityManager.Instance.Release();
            WorldHpBarManager.Instance.Clear();
            BattleTargetClickManager.Instance.Release();
            GameInputManager.Instance.Release();
        }
    }
}

