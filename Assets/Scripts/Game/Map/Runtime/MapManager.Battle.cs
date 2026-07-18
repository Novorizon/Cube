using Game.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UI;
using UnityEngine;

namespace Game
{
    public partial class MapManager
    {
        private const string BattlePagePrefabPath = "Assets/Arts/UI/Panels/Battle/BattlePage.prefab";
        private const string MainMenuPagePath = "Assets/Arts/UI/Pages/MainMenuPage.prefab";

        private int currentBattleMapConfigId;

        public bool LoadBattleMap(int mapConfigId)
        {
            if (!DataManager.Instance.Map.TryGet(mapConfigId, out MapConfig mapConfig))
            {
                Debug.LogError($"Load battle map failed. Missing map config: {mapConfigId}");
                return false;
            }

            string location = "Assets/Data/Map/" + mapConfig.MapId + ".json";

            ClearBattleRuntime(true);
            currentBattleMapConfigId = mapConfigId;
            BattleItemManager.Instance.AddItem(ItemIds.Gold, mapConfig.InitialGold);

            bool loadDataSuccess = LoadMapData(location);
            if (!loadDataSuccess)
            {
                return false;
            }

            CreateMap();
            AfterBattleMapCreated(mapConfig);
            return true;
        }

        private void AfterBattleMapCreated(MapConfig mapConfig)
        {
            CameraManager.Instance.Initialize();
            CameraManager.Instance.SetViewAngle(55f, 45f);
            CameraManager.Instance.SetPadding(2f);
            CameraManager.Instance.FocusCurrentMap();

            StartBattleAsync(mapConfig).Forget();
        }

        private async Task StartBattleAsync(MapConfig mapConfig)
        {
            BattlePage battlePage = await ShowBattlePageAsync();
            if (battlePage == null)
            {
                Debug.LogError("Start battle failed because BattlePage could not be opened.");
                return;
            }

            WorldHpBarManager.Instance.Initialize();
            BattleFlowManager.Instance.BeginBattle(mapConfig);

            if (!BaseManager.Instance.LoadBase(mapConfig.BaseId))
            {
                BattleFlowManager.Instance.CompleteDefeat("Base load failed.");
                return;
            }

            GameInputManager.Instance.SetMode(InputMode.Battle);

            if (!DataManager.Instance.LoadWave(mapConfig.WaveNormal))
            {
                BattleFlowManager.Instance.CompleteDefeat("Wave data load failed.");
                return;
            }

            //WaveConfig waveConfig = DataManager.Instance.Wave.Get(1);
            if (!WaveManager.Instance.StartWave())
            {
                BattleFlowManager.Instance.CompleteDefeat("Wave start failed.");
            }
        }

        private async Task<BattlePage> ShowBattlePageAsync()
        {
            UIHandle handle = await UIManager.Instance.Pages.ReplaceAsync(BattlePagePrefabPath);
            if (!handle.IsValid)
            {
                return null;
            }

            if (handle.View is BattlePage battlePage)
            {
                battlePage.SkillClicked -= OnBattlePageSkillClicked;
                battlePage.SkillClicked += OnBattlePageSkillClicked;
                battlePage.AutoNextWaveChanged -= OnBattlePageAutoNextWaveChanged;
                battlePage.AutoNextWaveChanged += OnBattlePageAutoNextWaveChanged;
                battlePage.TowerSellTargetClicked -= OnBattlePageTowerSellClicked;
                battlePage.TowerSellTargetClicked += OnBattlePageTowerSellClicked;
                battlePage.TowerUpgradeTargetClicked -= OnBattlePageTowerUpgradeClicked;
                battlePage.TowerUpgradeTargetClicked += OnBattlePageTowerUpgradeClicked;
                battlePage.ItemClicked -= OnBattlePageItemClicked;
                battlePage.ItemClicked += OnBattlePageItemClicked;
                OnBattlePageAutoNextWaveChanged(battlePage.AutoNextWaveEnabled);
                return battlePage;
            }

            Debug.LogError($"Battle HUD prefab root must use {nameof(BattlePage)}.");
            return null;
        }

        private void OnBattlePageSkillClicked(int skillId)
        {
            Ability.CastResult result = AbilityManager.Instance.CastBaseAbilityAtBestTarget(skillId);
            if (result == null || result.Success)
            {
                return;
            }

            string message = string.IsNullOrWhiteSpace(result.Message)
                ? LocalizationManager.GetOrFallback("ui.td.toast.skill_failed", "技能释放失败")
                : result.Message;
            Toast.Warning(message);
            Debug.LogWarning($"Cast skill failed. skillId: {skillId}, reason: {result.FailureReason}, message: {result.Message}");
        }

        private void OnBattlePageAutoNextWaveChanged(bool autoNextWave)
        {
            // true means waves chain immediately after spawn completion; false waits for the field to clear.
            WaveManager.Instance.SetWaitAllEnemiesKilledBeforeNextWave(!autoNextWave);
        }

        private void OnBattlePageTowerSellClicked(TdTargetRuntimeInfo info)
        {
            if (info.Type != TdTargetInfoType.Tower)
            {
                return;
            }

            if (!TryGetTower(info.Coord, out Tower tower) || tower == null)
            {
                Toast.Warning(LocalizationManager.GetOrFallback("ui.td.toast.tower_missing", "未找到要出售的防御塔"));
                return;
            }

            if (!TowerBuildManager.Instance.TrySellTower(tower, out int sellItemId, out int sellCount))
            {
                return;
            }

            BattleTargetClickManager.Instance.ClearSelection();
            Toast.Info(LocalizationManager.GetOrFallback("ui.td.toast.sell_success", "出售成功") + $" +{sellCount}");
        }

        private void OnBattlePageTowerUpgradeClicked(TdTargetRuntimeInfo info)
        {
            if (info.Type != TdTargetInfoType.Tower)
            {
                return;
            }

            if (!TryGetTower(info.Coord, out Tower tower) || tower == null)
            {
                Toast.Warning(LocalizationManager.GetOrFallback("ui.td.toast.tower_missing", "未找到要升级的防御塔"));
                return;
            }

            if (TowerBuildManager.Instance.TryUpgradeTower(tower))
            {
                BattleTargetClickManager.Instance.ClearSelection();
            }
        }

        private void OnBattlePageItemClicked(int itemId)
        {
            BattleItemUseResult result = BattleItemUseService.Instance.TryUse(itemId);
            if (result == BattleItemUseResult.Success)
            {
                return;
            }

            string itemName = LocalizedConfigText.ItemName(itemId);
            switch (result)
            {
                case BattleItemUseResult.MissingItem:
                    Toast.Warning(LocalizationManager.GetOrFallback("ui.td.toast.item_missing", "道具数量不足"));
                    break;
                case BattleItemUseResult.NotUsableInBattle:
                    Toast.Info($"{itemName}: " + LocalizationManager.GetOrFallback("ui.td.toast.item_not_usable", "无法在战斗中使用"));
                    break;
                case BattleItemUseResult.NoHandler:
                    Toast.Info($"{itemName}: " + LocalizationManager.GetOrFallback("ui.td.toast.item_handler_missing", "尚未配置战斗效果"));
                    break;
                default:
                    Toast.Warning($"{itemName}: " + LocalizationManager.GetOrFallback("ui.td.toast.item_use_failed", "使用失败"));
                    break;
            }
        }

        public void RestartCurrentBattleMap()
        {
            int mapConfigId = currentBattleMapConfigId;
            if (mapConfigId <= 0 && BattleFlowManager.Instance.LastEndMessage != null)
            {
                mapConfigId = BattleFlowManager.Instance.LastEndMessage.MapId;
            }

            if (mapConfigId <= 0)
            {
                Debug.LogWarning("Restart battle map failed. Current battle map config id is invalid.");
                return;
            }

            LoadBattleMap(mapConfigId);
        }

        public bool HasNextBattleMap(int mapConfigId)
        {
            return TryGetNextBattleMapConfigId(mapConfigId, out _);
        }

        public bool LoadNextBattleMap(int mapConfigId)
        {
            if (!TryGetNextBattleMapConfigId(mapConfigId, out int nextMapConfigId))
            {
                Toast.Info("已经是最后一关");
                return false;
            }

            return LoadBattleMap(nextMapConfigId);
        }

        public void ReturnToMainMenu()
        {
            ReturnToMainMenuAsync().Forget();
        }

        private async Task ReturnToMainMenuAsync()
        {
            ClearBattleRuntime(true);

            if (GameInputManager.IsCreated)
            {
                GameInputManager.Instance.SetMode(InputMode.World);
            }

            await UIManager.Instance.Pages.ResetToAsync(MainMenuPagePath);
        }

        private bool TryGetNextBattleMapConfigId(int mapConfigId, out int nextMapConfigId)
        {
            nextMapConfigId = 0;

            if (DataManager.Instance.Map == null || DataManager.Instance.Map.GetAll() == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, MapConfig> pair in DataManager.Instance.Map.GetAll())
            {
                int candidateId = pair.Key;
                if (candidateId <= mapConfigId)
                {
                    continue;
                }

                if (nextMapConfigId == 0 || candidateId < nextMapConfigId)
                {
                    nextMapConfigId = candidateId;
                }
            }

            return nextMapConfigId > 0;
        }

        private void ClearBattleRuntime(bool hideBattleUi)
        {
            Time.timeScale = 1f;
            WaveManager.Instance.Stop();
            WaveManager.Instance.Clear();
            NpcManager.Instance.Clear();
            TowerManager.Instance.Clear();
            TowerBuildManager.Instance.Clear();
            BattleTargetClickManager.Instance.ClearSelection();
            WorldHpBarManager.Instance.Clear();
            BaseManager.Instance.ClearBaseObject();
            AbilityManager.Instance.Release();
            AbilityManager.Instance.Initialize();
            BattleItemManager.Instance.Clear();
            DataManager.Instance.ClearWave();
            BattleFlowManager.Instance.Initialize();
            GameplayController.Shutdown();
            ClearMap();

            if (hideBattleUi)
            {
                UIManager.Instance.Popups.CloseAll(true);
                UIManager.Instance.Panels.HideAll(true);
            }
        }
    }
}
