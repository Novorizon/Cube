using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Bootstrap;
using Game.Framework;
using UI;

namespace Game
{
    /// <summary>Cube's explicit composition root. Gameplay rules stay in their existing managers.</summary>
    public sealed class CubeGameLifecycle : IGameLifecycle
    {
        private readonly int startupWorldMapId;
        private readonly GameCleanupScope cleanup = new GameCleanupScope();
        private bool storageInitialized;
        private bool ready;

        public CubeGameLifecycle(int startupWorldMapId)
        {
            this.startupWorldMapId = startupWorldMapId;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeviceManager.Instance.Initialize();
            await ResourceManager.Instance.InitializeAsync();
            // Resource initialization has no cancellation contract. Never continue project
            // construction after the owner stopped while that operation was in flight.
            cancellationToken.ThrowIfCancellationRequested();

            GameInputManager input = GameInputManager.Instance;
            cleanup.Add(input.Release);
            input.Initialize(InputMode.UI);
            Require(CameraManager.Instance.Initialize(), "Main camera initialization failed.");
            MapInputController.Instance.Initialize();

            DataManager.Instance.Initialize();
            Require(DataManager.Instance.Item != null && DataManager.Instance.Map != null, "Required configuration tables are unavailable.");
            TechManager.Instance.Initialize();
            WorldGatherManager.Instance.Initialize();
            WorldBuildingManager.Instance.Initialize();
            MineManager.Instance.Initialize();
            FarmManager.Instance.Initialize();
            BlueprintManager.Instance.Initialize();
            QuestManager.Instance.Initialize();

            cleanup.Add(() => StoryManager.Instance.Release());
            StoryManager.Instance.Initialize();
            ToolKitManager.Instance.Initialize();
            cleanup.Add(() => BagManager.Instance.Release());
            BagManager.Instance.Initialize();
            CalendarManager.Instance.Initialize();
            WorldIncomeManager.Instance.Initialize();

            StorageManager.Instance.Initialize();
            storageInitialized = true;
            SaveLoadResult loadResult = StorageManager.Instance.LoadSession();
            Require(loadResult != SaveLoadResult.Failed, "Saved session could not be restored. The existing save has been preserved.");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GmCommandController.EnsureExists();
#endif

            BaseManager.Instance.Initialize();
            NpcManager.Instance.Initialize();
            TowerManager.Instance.Initialize();
            WaveManager.Instance.Initialize();
            cleanup.Add(() => AbilityManager.Instance.Release());
            Require(AbilityManager.Instance.Initialize(), "Ability adapters could not initialize.");
            BattleFlowManager.Instance.Initialize();
            cleanup.Add(() => WorldHpBarManager.Instance.Clear());

            cleanup.Add(() => MapManager.Instance.ClearMap());
            Require(MapManager.Instance.Initialize(), "Map presentation configuration is unavailable.");
            TowerBuildManager.Instance.Initialize();
            TowerBuildInputController.Instance.Initialize();
            cleanup.Add(() => BattleTargetClickManager.Instance.Release());
            BattleTargetClickManager.Instance.Initialize();

            UIManager.Instance.UseResourceManagerLoader();
            cleanup.Add(() => QuestToastListener.Instance.Release());
            QuestToastListener.Instance.Initialize();
            cleanup.Add(GameplayController.Shutdown);
            cleanup.Add(() => UIManager.Instance.ClearAll(true));

            UIManager.Instance.ClearAll(true);
            Require(await MapManager.Instance.LoadWorldMapAsync(startupWorldMapId, cancellationToken), "The initial world or its main UI could not be loaded.");
            cancellationToken.ThrowIfCancellationRequested();
            StoryManager.Instance.TryStartAutoStories();
            cancellationToken.ThrowIfCancellationRequested();
            StorageManager.Instance.BeginSession();
            ready = true;
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            CalendarManager.Instance.Update(unscaledDeltaTime);
            WorldBuildingManager.Instance.Update();
            WorldIncomeManager.Instance.Update();
            StorageManager.Instance.Update();

            if (!BattleFlowManager.Instance.IsRunning)
            {
                return;
            }

            AbilityManager.Instance.Update(deltaTime);
            NpcManager.Instance.Update(deltaTime);
            WorldHpBarManager.Instance.Update();

            // An earlier step can finish the battle during this same frame.
            if (!BattleFlowManager.Instance.IsRunning)
            {
                return;
            }

            TowerManager.Instance.Update(deltaTime);
            if (BattleFlowManager.Instance.IsRunning)
            {
                WaveManager.Instance.Update(deltaTime);
            }
        }

        public void Shutdown(GameShutdownReason reason)
        {
            using (var shutdown = new GameCleanupScope())
            {
                // Save, close the session, then release project objects. Each step still
                // runs if another fails; the scope reports all failures together.
                shutdown.Add(cleanup.Dispose);
                if (storageInitialized)
                {
                    shutdown.Add(() =>
                    {
                        StorageManager.Instance.EndSession();
                        storageInitialized = false;
                    });
                }

                if (ready && reason == GameShutdownReason.Requested)
                {
                    shutdown.Add(() =>
                    {
                        if (StorageManager.Instance.CanSave && !StorageManager.Instance.Save())
                        {
                            throw new InvalidOperationException("The final session save failed.");
                        }
                    });
                }

                ready = false;
            }
        }

        private static void Require(bool succeeded, string message)
        {
            if (!succeeded)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
