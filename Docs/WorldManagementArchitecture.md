# World Management Architecture

本文档规划“大地图经营模式”的第一阶段架构。目标是让逻辑层稳定使用格子，同时不把塔防战斗内的类强行复用到长期经营玩法里。

## 定位

大地图经营模式负责长期局外进度：

- 大地图格子逻辑
- 资源与库存
- 采集
- 建筑建造、升级、生产
- NPC 城镇交互
- 任务与冒险入口
- 与塔防模式的数据交换

塔防模式负责单局战斗：

- 波次、敌人、基地、塔
- 战斗内金币/消耗品
- 战斗内建塔、升级、出售

二者可以共享配置、道具定义、格子规则和部分建造流程思想，但运行时状态必须分离。

## 命名规则

大地图经营新增类统一使用 `World` 前缀。原因是项目中已有 `Game` 命名空间和大量通用 `Game` 类，`World` 更容易表达“长期大地图/局外世界”。

推荐命名：

- `WorldItem`
- `WorldItemManager`
- `WorldResourceWallet`
- `WorldGatherManager`
- `WorldGatherNode`
- `WorldGatherNodeState`
- `WorldBuildingManager`
- `WorldBuilding`
- `WorldBuildingState`
- `WorldBuildingPlacement`
- `WorldProductionManager`
- `WorldMapSession`

保留现有 TD 命名：

- `ItemManager`：继续作为 TD 战斗内道具/金币管理器。
- `TowerBuildManager`：继续作为 TD 建塔流程。
- `TowerManager`：继续作为 TD 塔运行时管理器。

不要新增另一个叫 `ItemManager`、`BuildManager` 或 `BuildingManager` 的模糊全局类。已有 `Game/Build` 里的空骨架可以后续改成共享建造基础，但第一阶段建议大地图使用 `WorldBuildingManager`，减少和 TD 建塔冲突。

## 现有内容复用判断

### 可以复用

`Data/Excel/item.xlsx` 与 `ItemConfig` 可以作为全项目物品定义表继续使用。木头、石头、矿石、食物、种子、蓝图、材料都可以是 Item。

`ItemIds` 的区间规划可以继续沿用或扩展：

- `1..999999`：基础资源，比如金币、木头、石头、食物。
- `3000000..3999999`：蓝图。
- `4000000..4999999`：材料。
- `5000000..5999999`：种子。

`MapCellData`、`TileData`、`MapObjectData`、`MapTileRule` 可以作为格子逻辑基础继续使用。当前它们已经支持：

- 格子坐标
- 地形类型
- Overlay
- Walkable / Buildable / MoveCost
- `MapObjectData.ObjectType = Resource / Building / Interactable`
- `BlocksBuild` / `BlocksMove`

塔防 `TowerBuildManager` 的流程思想可以复用：

```text
选择配置 -> 创建预览 -> 检查格子 -> 检查并扣资源 -> 实例化表现 -> 写入格子占用 -> 注册运行时对象
```

### 不建议直接复用

`ItemManager` 不应该被大地图经营复用。它当前明确是 TD 战斗内道具管理器，并且会在 `MapManager.ClearBattleRuntime` 中清空，适合单局战斗，不适合长期存档。

`TowerBuildManager` 不应该直接用于经营建筑。塔有攻击、射程、技能、战斗升级、出售返还等语义；经营建筑有占地、建造时间、生产、工人、耐久、前置科技、城镇交互等语义。

`TowerConfig` / `TowerLevelConfig` 不应该扩展成经营建筑表。字段已经明显偏战斗塔：`Range`、`Damage`、`AttackInterval`、`SkillId`、`AttackEffect`。

## 资源与库存

第一阶段新增长期物品管理：

```text
Assets/Scripts/Game/Island/Resources
  WorldItem.cs
  WorldItemManager.cs
  WorldCostResolver.cs
  WorldRewardResolver.cs
```

核心接口：

```csharp
public sealed class WorldItemManager
{
    public int GetCount(int itemId);
    public bool HasItems(IReadOnlyList<WorldItem> costs);
    public void AddItem(int itemId, int count);
    public void AddItems(IReadOnlyList<WorldItem> items);
    public bool TryConsumeItems(IReadOnlyList<WorldItem> costs);
}
```

`WorldItemManager` 和 TD `ItemManager` 都使用 `itemId`，但不是同一个运行时容器：

```text
ItemConfig
  -> TD ItemManager          单局战斗库存
  -> WorldItemManager        长期经营物品数量
```

以后进入塔防时，可以由大地图生成一份战斗初始资源：

```text
WorldItemManager + 战斗规则
  -> BattleStartLoadout
      -> ItemManager.Initialize / AddItem
```

塔防胜利结算时，再把奖励写回：

```text
BattleReward
  -> WorldItemManager.AddItem(...)
```

## 格子逻辑与占用

表现层可以是 Terrain、模型拼接、格子 prefab 或混合方式，但逻辑层固定使用格子。

当前 `TileData` 只直接记录 `Tower`，这对 TD 足够，但对大地图不够。大地图需要独立占用模型：

```text
Assets/Scripts/Game/Island
  MapManager.CanPlaceMapObject
  MapManager.TryAddMapObject
  MapManager.TryRemoveMapObject
```

建议第一阶段不要立刻重构 `TileData` 里的 `Tower` 字段。塔防占用继续走 `CanPlaceTower` / `TryPlaceTower`，大地图对象占用复用 `MapManager` 的 `objectsByCoord` 与 `MapObjectData`：

```csharp
public sealed class MapManager
{
    public bool IsBuildable(Vector3Int coord);
    public bool CanPlaceMapObject(Vector3Int coord);
    public bool TryAddMapObject(MapObjectData mapObject);
    public bool TryRemoveMapObject(int objectId);
    public bool CanPlaceTower(Vector3Int coord); // wraps CanPlaceMapObject
    public bool TryPlaceTower(Vector3Int coord, Tower tower);
}
```

后续如果 TD 和大地图都稳定后，再考虑把 `TileData.Tower` 泛化成通用 `RuntimeOccupant`。现在直接改风险较高，因为 TD 建塔、寻路和选择逻辑都依赖它。

## 采集设计

采集不是简单“点一下加资源”，它至少需要分三层：

### Luban Excel 规则

新增或修改 `Data/Excel` 下的 Luban Excel 时，第一列必须保留 `#` 标记列，和现有表格式保持一致。后续新增的 `world_*` 表都按这个规则创建，避免 Luban 生成失败或列解析错位。

### 1. 配置层

新增 Luban 表：

```text
Data/Defines/world_gather.xml
Data/Excel/world_gather.xlsx
```

建议字段：

```text
id
name
rewardGroupId
depleteAfterTimes
respawnSeconds
enable
```

奖励独立成组：

```text
Data/Defines/world_reward.xml
Data/Excel/world_reward.xlsx
```

字段：

```text
id
groupId
itemId
minCount
maxCount
weight
```

这样采集、任务、冒险、塔防结算都可以复用 `world_reward`。

### 2. 地图对象层

地图上的树、矿点、草药点、宝箱都可以是 `MapObjectData`：

```text
ObjectType = Resource
ConfigId = world_gather.id 或 world_resource.id
BlocksBuild = true/false
BlocksMove = true/false
Coord = 格子坐标
```

如果一个采集点需要保存剩余次数、刷新时间、已枯竭状态，则运行时用 `WorldGatherNodeState` 保存：

```csharp
public sealed class WorldGatherNodeState
{
    public int ObjectId;
    public int ConfigId;
    public Vector3Int Coord;
    public int RemainingTimes;
    public long RespawnAtWorldTick;
    public bool Depleted;
}
```

### 3. 行为层

采集流程：

```text
点击格子/对象
  -> WorldGatherManager.TryGather
  -> 校验配置、资源对象类型、节点剩余次数
  -> WorldRewardResolver.GetRewardGroup
  -> WorldItemManager.AddItems
  -> 更新节点剩余次数/枯竭/刷新时间
  -> 通知 UI 和地图表现刷新
```

第一阶段只做即时采集：

```csharp
public bool TryGather(MapObjectData mapObject, out IReadOnlyList<WorldItem> rewards)
{
}
```

后续加角色走过去、工作时间、工人分配、离线收益时，再增加命令或任务层。

## 建筑设计

大地图建筑与塔防塔分开建表。

新增 Luban 表：

```text
Data/Defines/world_building.xml
Data/Excel/world_building.xlsx
Data/Defines/world_building_level.xml
Data/Excel/world_building_level.xlsx
```

`world_building` 字段：

```text
id
name
description
buildingType
sizeX
sizeZ
placementRule
prefabLocation
iconLocation
unlockConditionId
maxLevel
enable
```

`world_building_level` 字段：

```text
id
buildingId
level
buildCostGroupId
upgradeCostGroupId
buildSeconds
productionId
storageBonusGroupId
workerSlots
prefabLocation
enable
```

成本建议也用组表，而不是单个 `CostItemId + CostCount`：

```text
Data/Defines/world_cost.xml
Data/Excel/world_cost.xlsx
```

字段：

```text
id
groupId
itemId
count
```

原因：经营建筑经常需要木头 + 石头 + 金币 + 蓝图，单成本字段很快不够用。

### 运行时状态

```text
Assets/Scripts/Game/Island/Buildings
  WorldBuilding.cs
  WorldBuildingState.cs
  WorldBuildingManager.cs
  WorldBuildingPlacement.cs
```

```csharp
public sealed class WorldBuildingState
{
    public int InstanceId;
    public int ConfigId;
    public int Level;
    public Vector3Int OriginCoord;
    public int Rotation;
    public WorldBuildingStatus Status;
    public long FinishAtWorldTick;
}
```

状态：

```text
Placing
Constructing
Active
Upgrading
Disabled
Damaged
```

建造流程：

```text
选择建筑
  -> WorldBuildingPlacement 创建预览
  -> MapManager.CanPlaceMapObject 检查占地
  -> WorldItemManager.TryConsumeItems(buildCostGroup)
  -> 写入 WorldBuildingState
  -> 占用格子
  -> 生成建筑表现
  -> 如果 buildSeconds > 0，进入 Constructing
  -> 时间到后 Active
```

升级流程：

```text
选择已有建筑
  -> 查 world_building_level 下一级
  -> 校验成本和条件
  -> 扣 WorldItemManager
  -> Status = Upgrading
  -> 时间到后 Level + 1
  -> 刷新生产、容量、外观
```

## 生产系统

农田、矿场、工坊都不应该在建筑类里写死产出逻辑。建筑只挂生产配置。

新增表：

```text
Data/Defines/world_production.xml
Data/Excel/world_production.xlsx
```

字段：

```text
id
buildingId
level
outputRewardGroupId
cycleSeconds
enable
```

第一阶段可先做：

- 农田：每周期产出食物。
- 矿场：每周期产出石头或矿石。
- 工坊：消耗木头/矿石，产出材料。

## 与塔防的关系

大地图经营和塔防之间通过明确 DTO 交换，不共享运行时管理器。

```text
WorldItemManager
WorldBuildingState
WorldQuestState
  -> BattleStartContext
      -> TD ItemManager / TowerBuildManager / Wave

TD BattleResult
  -> WorldRewardApplier
      -> WorldItemManager / WorldQuestState / WorldMapState
```

举例：

- 大地图建了“箭塔工坊”后，塔防解锁某类塔。
- 大地图库存有木头和金币，进入塔防时转换成初始战斗金币或可用道具。
- 塔防胜利后获得矿石、蓝图、NPC 好感或新地图区域解锁。

## 存档策略

第一阶段玩家存档使用本地 JSON 文件：

```text
Application.persistentDataPath/save_0.json
```

运行时状态常驻内存，数据变化时只调用 `StorageManager.MarkDirty()`，不在每次采集、物品变化、生产变化时立刻写文件。`StorageManager.Update()` 会在 dirty 后延迟数秒统一保存，退出游戏时再强制保存一次。

第一版保存内容：

```text
SaveData
  Version
  WorldItems      itemId + count
  GatherNodes     objectId + gatherConfigId + remainingTimes + availableAtUnixTime
```

写文件流程：

```text
序列化 SaveData
  -> 写 save_0.json.tmp
  -> 写成功后替换 save_0.json
```

`PlayerPrefs` 只用于音量、画质、新手引导开关等偏好设置，不用于经营状态。SQLite 暂不引入，等存档数据需要复杂查询或大量历史记录时再考虑。

## 推荐目录

```text
Assets/Scripts/Game/Island
  WorldMapSession.cs
  Resources/
    WorldItem.cs
    WorldItemManager.cs
    WorldCostResolver.cs
    WorldRewardResolver.cs
  Gathering/
    WorldGatherManager.cs
    WorldGatherNodeState.cs
  Buildings/
    WorldBuildingManager.cs
    WorldBuilding.cs
    WorldBuildingState.cs
    WorldBuildingPlacement.cs
  Production/
    WorldProductionManager.cs
  Exploration/
  Workers/
```

如果后续确认某些建造能力 TD 和 World 都能共享，再从 `WorldBuildingPlacement` 和 `TowerBuildManager` 中抽到：

```text
Assets/Scripts/Game/Build
  BuildCostGroup.cs
  BuildFootprint.cs
  BuildPlacementValidator.cs
  BuildPreviewController.cs
```

但第一阶段不要为了“通用”先抽太深。

## 第一阶段实施顺序

1. 新增 `WorldItemManager`，复用 `ItemConfig`，不接 TD `ItemManager`。
2. 新增 `world_cost`、`world_reward` 设计，成本和奖励都用组。
3. 增强 `MapManager` 的通用地图对象接口，不改 `TileData.Tower`。
4. 新增 `world_gather` 与 `WorldGatherManager`，先支持即时采集。
5. 新增 `world_building`、`world_building_level` 与 `WorldBuildingManager`，先支持 1x1 建筑。
6. 新增 `world_production`，让农田/矿场周期产出。
7. 做最小闭环：采集木头/石头 -> 建农田/矿场 -> 建筑产出 -> 库存变化。
8. 再接 NPC、任务、冒险和塔防入口。

## 第一版最小闭环

第一版只需要这些内容：

```text
资源：Gold, Wood, Stone, Food
采集点：Tree, Rock
建筑：Farm, Quarry, Storage
逻辑：点击采集、建造 1x1、建筑周期产出、长期库存
地图：仍用现有 MapData / MapObjectData / TileData
```

完成后，大地图经营模式就有了独立骨架，后续再逐步接入更复杂的种田、挖矿、NPC 城镇、任务和冒险。
