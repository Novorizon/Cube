using Game.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game
{
    public sealed class BattleTargetClickManager : Singleton<BattleTargetClickManager>
    {
        private Camera targetCamera;
        private LayerMask targetLayerMask = ~0;
        private float rayDistance = 1000f;
        private bool initialized;
        private bool inputRegistered;
        private readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();

        public void Initialize(Camera targetCamera = null)
        {
            if (targetCamera != null)
            {
                this.targetCamera = targetCamera;
            }
            else if (this.targetCamera == null)
            {
                this.targetCamera = Camera.main;
            }

            if (!inputRegistered)
            {
                GameInputManager.Instance.BattleSelectPerformed += OnBattleSelectPerformed;
                inputRegistered = true;
            }

            initialized = true;
        }

        public void Release()
        {
            if (inputRegistered && GameInputManager.IsCreated)
            {
                GameInputManager.Instance.BattleSelectPerformed -= OnBattleSelectPerformed;
            }

            inputRegistered = false;
            initialized = false;
            targetCamera = null;
            iconCache.Clear();
            missingIconWarnings.Clear();
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

        public void SelectByScreenPosition(Vector2 screenPosition, bool ignoreClickOnUi = true)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (GameInputManager.Instance.CurrentMode != InputMode.Battle)
            {
                return;
            }

            if (TowerBuildManager.Instance.HasSelectedTower)
            {
                return;
            }

            if (ignoreClickOnUi && IsPointerOverUi())
            {
                return;
            }

            TrySelectTarget(screenPosition);
        }

        public void ClearSelection()
        {
            Messager.Instance.Notify(BattleMessageTopic.TargetInfoCleared, new TargetInfoClearMessage());
        }

        private void OnBattleSelectPerformed(InputAction.CallbackContext context)
        {
            if (context.canceled)
            {
                return;
            }

            Vector2 screenPosition = GameInputManager.Instance.PointerPosition;
            SelectByScreenPosition(screenPosition);
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
            RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, targetLayerMask);

            if (hits == null || hits.Length == 0)
            {
                ClearSelection();
                return;
            }

            System.Array.Sort(hits, CompareRaycastHitDistance);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (TryShowTowerInfo(hitCollider))
                {
                    return;
                }

                if (TryShowNpcInfo(hitCollider))
                {
                    return;
                }

                if (TryShowBaseInfo(hitCollider))
                {
                    return;
                }
            }

            ClearSelection();
        }

        private int CompareRaycastHitDistance(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
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

            NotifyTargetInfo(info);
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

            NotifyTargetInfo(info);
            return true;
        }

        private bool TryShowBaseInfo(Collider hitCollider)
        {
            BaseView baseView = hitCollider.GetComponentInParent<BaseView>();

            if (baseView == null)
            {
                return false;
            }

            NotifyTargetInfo(BuildBaseInfo());
            return true;
        }

        private void NotifyTargetInfo(TdTargetRuntimeInfo info)
        {
            TargetInfoMessage message = new TargetInfoMessage();
            message.Info = info;
            Messager.Instance.Notify(BattleMessageTopic.TargetInfoChanged, message);
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

            TowerLevelConfig levelConfig = DataManager.Instance.GetTowerLevel(tower.ConfigId, tower.Level);
            if (levelConfig == null)
            {
                return default;
            }

            DataManager.Instance.TryGetNextTowerLevel(tower, out TowerLevelConfig nextLevelConfig);
            int sellGold = TowerBuildManager.Instance.CalculateSellCount(tower);
            int attackAdd = nextLevelConfig != null ? nextLevelConfig.Damage - levelConfig.Damage : 0;

            TdTargetRuntimeInfo info = new TdTargetRuntimeInfo
            {
                Type = TdTargetInfoType.Tower,
                TargetId = tower.ConfigId,
                Name = config.Name,
                Description = config.Description,
                Icon = LoadIconSprite(config.IconLocation),
                PreviewPrefabLocation = levelConfig.PrefabLocation,
                Coord = tower.Coord,
                Level = tower.Level,
                Attack = levelConfig.Damage,
                AttackAdd = attackAdd,
                Range = levelConfig.Range,
                AttackInterval = levelConfig.AttackInterval,
                UpgradeCost = nextLevelConfig != null ? nextLevelConfig.UpgradeCost : 0,
                SellGold = sellGold,
                CanUpgrade = nextLevelConfig != null,
                CanSell = true
            };

            info.InfoSlots = new List<TdInfoSlotData>
            {
                new TdInfoSlotData("level", "等级", info.Level.ToString()),
                new TdInfoSlotData("attack", "攻击", info.Attack.ToString(), info.AttackAdd > 0 ? $"+{info.AttackAdd}" : string.Empty),
                new TdInfoSlotData("range", "范围", $"{info.Range:0.#}"),
                new TdInfoSlotData("speed", "攻速", $"{info.AttackInterval:0.#}s"),
                new TdInfoSlotData("upgradeCost", "升级", info.CanUpgrade ? info.UpgradeCost.ToString() : "--"),
                new TdInfoSlotData("sellGold", "出售", info.SellGold.ToString())
            };

            return info;
        }

        private TdTargetRuntimeInfo BuildNpcInfo(Npc npc)
        {
            if (npc == null || npc.Config == null || npc.Data == null)
            {
                return default;
            }

            TdTargetRuntimeInfo info = new TdTargetRuntimeInfo
            {
                Type = TdTargetInfoType.Npc,
                TargetId = npc.Config.Id,
                Name = npc.Config.Name,
                Description = npc.Config.Description,
                PreviewPrefabLocation = npc.Config.PrefabLocation,
                CurrentHp = npc.Data.CurrentHp,
                MaxHp = npc.Data.MaxHp,
                Attack = npc.Data.DamageToBase,
                Range = npc.Data.AttackRange,
                AttackInterval = npc.Data.AttackInterval,
                CanUpgrade = false,
                CanSell = false
            };

            info.InfoSlots = new List<TdInfoSlotData>
            {
                new TdInfoSlotData("hp", "生命", $"{Mathf.Max(0, info.CurrentHp)}/{info.MaxHp}"),
                new TdInfoSlotData("attack", "伤害", info.Attack.ToString()),
                new TdInfoSlotData("range", "攻击范围", $"{info.Range:0.#}"),
                new TdInfoSlotData("speed", "攻击间隔", $"{info.AttackInterval:0.#}s"),
                new TdInfoSlotData("moveSpeed", "移动速度", $"{npc.Data.MoveSpeed:0.#}"),
                new TdInfoSlotData("reward", "击杀金币", npc.Data.RewardGold.ToString())
            };

            return info;
        }

        private TdTargetRuntimeInfo BuildBaseInfo()
        {
            BaseConfig config = BaseManager.Instance.Config;
            TdTargetRuntimeInfo info = new TdTargetRuntimeInfo
            {
                Type = TdTargetInfoType.Base,
                TargetId = config != null ? config.Id : 0,
                Name = "基地",
                Description = "保护基地，生命归零则战斗失败。",
                Icon = config != null ? LoadIconSprite(config.IconLocation) : null,
                PreviewPrefabLocation = config != null ? config.PrefabLocation : string.Empty,
                CurrentHp = BaseManager.Instance.CurrentLife,
                MaxHp = BaseManager.Instance.MaxLife,
                CanUpgrade = false,
                CanSell = false
            };

            if (config != null)
            {
                info.Name = config.Name;
                info.Description = config.Description;
            }

            info.InfoSlots = new List<TdInfoSlotData>
            {
                new TdInfoSlotData("hp", "生命", $"{Mathf.Max(0, info.CurrentHp)}/{info.MaxHp}")
            };

            return info;
        }

        private Sprite LoadIconSprite(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            if (iconCache.TryGetValue(location, out Sprite cachedSprite))
            {
                return cachedSprite;
            }

            if (!location.StartsWith("Assets/", System.StringComparison.Ordinal))
            {
                if (missingIconWarnings.Add(location))
                {
                    Debug.LogWarning($"Target icon location must be a full asset path. location: {location}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(location);
            if (sprite != null)
            {
                iconCache[location] = sprite;
            }
            else if (missingIconWarnings.Add(location))
            {
                Debug.LogWarning($"Target icon load failed. location: {location}");
            }

            return sprite;
        }
    }
}
