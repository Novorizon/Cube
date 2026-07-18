using Game.Framework;
using System.Threading.Tasks;
using UI;

namespace Game
{
    public partial class MapManager
    {
        public bool LoadWorldMap(int worldMapId)
        {
            string location = "Assets/Data/Map/" + worldMapId + ".json";

            ClearBattleRuntime(true);
            currentBattleMapConfigId = 0;

            bool loadDataSuccess = LoadMapData(location);
            if (!loadDataSuccess)
            {
                return false;
            }

            CreateMap();
            AfterWorldMapCreated();
            return true;
        }

        private void AfterWorldMapCreated()
        {
            CameraManager.Instance.Initialize();
            GameInputManager.Instance.SetMode(InputMode.World);
            FarmManager.Instance.CreateViews();
            GameplayController.Ensure();
            ShowWorldMainPanelAsync().Forget();
        }

        private async Task ShowWorldMainPanelAsync()
        {
            UIHandle handle = await UIManager.Instance.Panels.ShowAsync(WorldMainPanel.PrefabPath);
            if (!handle.IsValid)
            {
                return;
            }

            if (handle.View is WorldMainPanel panel)
            {
                panel.RefreshNow();
            }
        }
    }
}
