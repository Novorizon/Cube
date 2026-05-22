using Game.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    /// <summary>
    /// TD 战斗内目标点击管理器。
    /// 统一处理鼠标点击、射线检测、目标识别，并把目标信息传递给 BattleHudController。
    /// </summary>
    public sealed class BattleTargetClickManager : Singleton<EffectManager>
    {
        [SerializeField]
        private Camera targetCamera;

        [SerializeField]
        private LayerMask targetLayerMask = ~0;

        [SerializeField]
        private float rayDistance = 1000f;

        [SerializeField]
        private BattleHudController battleHud;

        public void Initialize()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        public void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (IsPointerOverUi())
            {
                return;
            }

            TrySelectTarget(Input.mousePosition);
        }

        public void SetBattleHud(BattleHudController battleHud)
        {
            this.battleHud = battleHud;
        }

        public void SetTargetCamera(Camera targetCamera)
        {
            this.targetCamera = targetCamera;
        }

        private bool IsPointerOverUi()
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private void TrySelectTarget(Vector2 screenPosition)
        {
            if (targetCamera == null)
            {
                Debug.LogWarning("Select target failed. Target camera is null.");
                return;
            }

            Ray ray = targetCamera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, targetLayerMask))
            {
                battleHud?.ClearTargetInfo();
                return;
            }

            if (TryShowTowerInfo(hit.collider))
            {
                return;
            }

            if (TryShowNpcInfo(hit.collider))
            {
                return;
            }

            if (TryShowBaseInfo(hit.collider))
            {
                return;
            }

            battleHud?.ClearTargetInfo();
        }

        private bool TryShowTowerInfo(Collider hitCollider)
        {
            Tower tower = hitCollider.GetComponentInParent<Tower>();

            if (tower == null)
            {
                return false;
            }

            TdTargetRuntimeInfo info = BuildTowerInfo(tower);

            if (info.Type == TdTargetInfoType.None)
            {
                battleHud?.ClearTargetInfo();
                return true;
            }

            battleHud?.ShowTargetInfo(info);
            return true;
        }

        private bool TryShowNpcInfo(Collider hitCollider)
        {
            Npc npc = hitCollider.GetComponentInParent<Npc>();

            if (npc == null)
            {
                return false;
            }

            TdTargetRuntimeInfo info = BuildNpcInfo(npc);

            if (info.Type == TdTargetInfoType.None)
            {
                battleHud?.ClearTargetInfo();
                return true;
            }

            battleHud?.ShowTargetInfo(info);
            return true;
        }

        private bool TryShowBaseInfo(Collider hitCollider)
        {
            BaseView baseView = hitCollider.GetComponentInParent<BaseView>();

            if (baseView == null)
            {
                return false;
            }

            TdTargetRuntimeInfo info = BuildBaseInfo();
            battleHud?.ShowTargetInfo(info);
            return true;
        }

        private TdTargetRuntimeInfo BuildTowerInfo(Tower tower)
        {
            if (tower == null || tower.ConfigId <= 0)
            {
                return default;
            }

            TowerConfig config = DataManager.Instance.Tower.Get(tower.ConfigId);

            if (config == null)
            {
                return default;
            }

            int sellGold = Mathf.RoundToInt(config.CostCount * config.SellGoldRate);

            return new TdTargetRuntimeInfo
            {
                Type = TdTargetInfoType.Tower,
                TargetId = tower.ConfigId,
                Name = config.Name,
                Description = config.Description,
                Level = 1,
                Attack = config.Damage,
                AttackAdd = 0,
                Range = config.Range,
                AttackInterval = config.AttackInterval,
                UpgradeCost = config.UpgradeCost,
                SellGold = sellGold,
                CanUpgrade = config.CanUpgrade,
                CanSell = true
            };
        }

        private TdTargetRuntimeInfo BuildNpcInfo(Npc npc)
        {
            if (npc == null || npc.Config == null || npc.Data == null)
            {
                return default;
            }

            return new TdTargetRuntimeInfo
            {
                Type = TdTargetInfoType.Npc,
                TargetId = npc.Config.Id,
                Name = npc.Config.Name,
                Description = npc.Config.Description,
                CurrentHp = npc.Data.CurrentHp,
                MaxHp = npc.Data.MaxHp,
                Attack = npc.Data.DamageToBase,
                Range = npc.Data.AttackRange,
                AttackInterval = npc.Data.AttackInterval,
                CanUpgrade = false,
                CanSell = false
            };
        }

        private TdTargetRuntimeInfo BuildBaseInfo()
        {
            return new TdTargetRuntimeInfo
            {
                Type = TdTargetInfoType.Base,
                TargetId = 1,
                Name = "基地",
                Description = "保护基地，生命归零则战斗失败。",
                CurrentHp = BaseManager.Instance.CurrentLife,
                MaxHp = BaseManager.Instance.MaxLife,
                CanUpgrade = false,
                CanSell = false
            };
        }
    }
}
