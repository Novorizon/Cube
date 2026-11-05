using Game.Framework;
using System.Threading;
using System.Threading.Tasks;
using UI;

namespace Game
{
    public partial class MapManager
    {
        private const int DefaultWorldMapId = 1001;
        private int currentWorldMapId;

        public bool LoadWorldMap(int worldMapId)
        {
            return LoadWorldMap(worldMapId, true);
        }

        /// <summary>Startup entry: the world and main panel must be ready before input is enabled.</summary>
        public async Task<bool> LoadWorldMapAsync(int worldMapId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LoadWorldMap(worldMapId, false))
            {
                return false;
            }

            bool shown = await ShowWorldMainPanelAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (shown)
            {
                GameInputManager.Instance.SetMode(InputMode.World);
            }

            return shown;
        }

        private bool LoadWorldMap(int worldMapId, bool showMainPanel)
        {
            string location = "Assets/Data/Map/" + worldMapId + ".json";

            ClearBattleRuntime(true);
            currentBattleMapConfigId = 0;

            bool loadDataSuccess = LoadMapData(location);
            if (!loadDataSuccess)
            {
                return false;
            }

            currentWorldMapId = worldMapId;
            CreateMap();
            AfterWorldMapCreated(showMainPanel);
            return true;
        }

        public void RetreatFromBattle()
        {
            int worldMapId = currentWorldMapId > 0 ? currentWorldMapId : DefaultWorldMapId;
            UIManager.Instance.Pages.Clear(true);

            if (!LoadWorldMap(worldMapId))
            {
                ReturnToMainMenu();
            }
        }

        private void AfterWorldMapCreated(bool showMainPanel)
        {
            CameraManager.Instance.Initialize();
            GameInputManager.Instance.SetMode(showMainPanel ? InputMode.World : InputMode.UI);
            FarmManager.Instance.CreateViews();
            GameplayController.Ensure();
            if (showMainPanel)
            {
                ShowWorldMainPanelAsync().Forget();
            }
        }

        private async Task ShowWorldMainPanelAsync()
        {
            await ShowWorldMainPanelAsync(CancellationToken.None);
        }

        private async Task<bool> ShowWorldMainPanelAsync(CancellationToken cancellationToken)
        {
            UIHandle handle = await UIManager.Instance.Panels.ShowAsync(WorldMainPanel.PrefabPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!handle.IsValid)
            {
                return false;
            }

            if (handle.View is WorldMainPanel panel)
            {
                panel.RefreshNow();
                return true;
            }

            return false;
        }
    }
}

