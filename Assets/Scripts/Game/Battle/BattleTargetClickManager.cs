using Game.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game
{
    /// <summary>
    /// TD 战斗内目标选择管理器。
    /// 不主动轮询输入，由外部输入系统在点击时调用 SelectByScreenPosition。
    /// </summary>
    public sealed class BattleTargetClickManager : Singleton<BattleTargetClickManager>
    {
        private Camera targetCamera;
        private LayerMask targetLayerMask = ~0;
        private float rayDistance = 1000f;
        private BattleHudController battleHud;

        public void Initialize(BattleHudController battleHud, Camera targetCamera = null)
        {
            this.battleHud = battleHud;

            if (targetCamera != null)
            {
                this.targetCamera = targetCamera;
            }
            else if (this.targetCamera == null)
            {
                this.targetCamera = Camera.main;
            }
        }

        public void SetBattleHud(BattleHudController battleHud)
        {
            this.battleHud = battleHud;
        }

        public void SetTargetCamera(Camera targetCamera)
        {
            this.targetCamera = targetCamera;
        }

        public void SetTargetLayerMask(LayerMask targetLayerMask)
        {
            this.targetLayerMask = targetLayerMask;
        }

        public void SetRayDistance(float rayDistance)
        {
            this.rayDistance = Mathf.Max(1f, rayDistance);
        }

        /// <summary>
        /// 由输入系统在点击发生时调用。
        /// </summary>
        public void SelectByScreenPosition(Vector2 screenPosition, bool ignoreClickOnUi = true)
        {
            if (ignoreClickOnUi && IsPointerOverUi())
            {
                return;
            }

            TrySelectTarget(screenPosition);
        }

        public void ClearSelection()
        {
            battleHud?.ClearTargetInfo();
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
            Camera camera = GetTargetCamera();

            if (camera == null)
            {
                Debug.LogWarning("Select target failed. Target camera is null.");
                return;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, targetLayerMask))
            {
                ClearSelection();
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

            ClearSelection();
        }

        private Camera GetTargetCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            return targetCamera;
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
                ClearSelection();
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
                ClearSelection();
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
