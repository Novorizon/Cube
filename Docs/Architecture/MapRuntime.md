# Map Runtime

本文记录地图运行时、地图对象、寻路、交互站位、资源移除和存档边界。地图美术见 `Docs/Modules/MapAndTileArt.md`，地图对象和 UI 标记边界见 `Docs/Decisions/0003-map-object-vs-marker.md`。

## 运行时链路

当前地图不是整张场景 Prefab，也不是把完整地图作为一个 Prefab 绑定到 UI 或场景中。

```text
Assets/Data/Map/{mapId}.json
  -> MapData
  -> MapManager.LoadWorldMap(mapId)
     或 MapManager.LoadBattleMap(mapConfigId)
  -> MapManager.LoadMapData(...)
  -> ApplyRemovedMapObjects(...)
  -> RebuildTileIndex()
  -> RebuildObjectIndex()
  -> WorldBuildingManager.RegisterMapObjects()
  -> MapManager.CreateMap()
  -> 根据 Cells / Objects / SpawnPoints / GoalPoint 动态生成地图视图
```

相关代码：

```text
Assets/Scripts/Game/Map/Runtime/MapManager.cs                # shared state, creation, indexes, queries
Assets/Scripts/Game/Map/Runtime/MapManager.World.cs          # LoadWorldMap and world post-load flow
Assets/Scripts/Game/Map/Runtime/MapManager.Battle.cs         # LoadBattleMap and battle flow/UI/navigation
Assets/Scripts/Game/Map/Runtime/MapManager.Loading.cs        # shared JSON loading pipeline
Assets/Scripts/Game/Map/Runtime/MapManager.Persistence.cs    # removed-map-object save state
Assets/Scripts/Game/Map/Data/MapData.cs
Assets/Scripts/Game/Map/Data/MapCellData.cs
Assets/Scripts/Game/Map/Data/MapObjectData.cs
Assets/Scripts/Game/Map/Pathfinding/MapPathFinder.cs
Assets/Scripts/Game/Island/Exploration/GameplayController.cs
Assets/Scripts/Game/Island/Exploration/NavigationController.cs
Assets/Scripts/Game/Island/Exploration/ActionController.cs
Assets/Scripts/Game/Island/Exploration/ResourceInteractionController.cs
```

## 数据分层

地图相关数据分三层：

```text
静态地图数据
  MapData.Cells
  MapData.Objects
  SpawnPoints / GoalPoint

运行时索引和视图
  tileDataMap / topLogicTileDataMap
  objectsByCoord
  TileView / WorldResourceView / building views

长期存档状态
  SaveRemovedMapObjectData
  SaveGatherNodeData
  SaveWorldBuildingData
  SaveFarmData / SaveWorldFarmPlotData
  SavePlayerData
```

`Renderer.enabled = false`、`Collider.enabled = false`、`GameObject.SetActive(false)` 只影响 Unity 视图和物理碰撞。当前寻路、建造占用和地图对象查询主要看 `MapManager` 的运行时索引，不看 view 是否隐藏。

## MapData.Cells

`Cells` 表示地形格子和逻辑属性：

```text
地形
覆盖层
是否可走
是否可建
移动成本
高度 / 逻辑层
```

`MapManager.CreateMap()` 遍历 `Cells`，根据 `MapTilePrefabConfig` 找到地块资源并实例化运行时 `TileView`。

当前 `MapManager.IsWalkable(coord)` 判断：

```text
coord 在 tileDataMap 中
是逻辑地块
是 exposed 顶层逻辑格
没有 BlocksMove 的地图对象
tileData.IsRuntimeWalkable == true
```

当前 `MapManager.IsBuildable(coord)` 判断：

```text
coord 在 tileDataMap 中
是逻辑地块
是 exposed 顶层逻辑格
没有 BlocksBuild 的地图对象
tileData.IsRuntimeBuildable == true
```

`GetMoveCost(coord)` 会先要求 `IsWalkable(coord)`，不可走时返回 `int.MaxValue`。

## MapData.Objects

`Objects` 表示地图上真实存在、需要生成视图或影响占用和交互的对象：

```text
Decoration
Resource
Building
Interactable
```

字段边界：

```text
ObjectId      地图对象实例 id，同一张地图内应稳定唯一
ObjectType    Decoration / Resource / Building / Interactable
ConfigId      业务配置 id，例如 Resource 使用 resource.id
X/Y/Z         对象所在逻辑格
LocalPosition 对象相对格子的局部偏移
LocalEuler    对象局部旋转
LocalScale    对象局部缩放
BlocksBuild   是否阻挡建造
BlocksMove    是否阻挡移动
```

对象类别默认值：

```text
资源类别默认属性来自 Excel / Luban 配置，例如 resource.blocksBuild / resource.blocksMove。
地图编辑器放置资源时默认使用 Excel 属性，只有需要单个对象特殊处理时才在编辑器里单独修改。
当前 MapObjectData 保存的是最终 BlocksBuild / BlocksMove 值，不保存“是否覆盖”的三态信息。
如果以后要求已放置对象自动跟随 Excel 默认值变化，需要新增覆盖标记或提供地图迁移工具。
```

`RebuildObjectIndex()` 会把 `MapData.Objects` 注册到 `objectsByCoord`。建筑等多格对象会按 footprint 注册到多个 coord。

重要规则：

```text
objectsByCoord 是当前寻路 / 建造 / 对象查询的主要占用索引
隐藏 view 不会自动从 objectsByCoord 删除对象
只有 MapManager.TryRemoveMapObject 会从 currentMap.Objects 和 objectsByCoord 移除对象
```

## 寻路规则

当前经营模式角色移动使用格子 A*，不是 Unity NavMesh，也不是 Collider-based 寻路。

```text
NavigationController
  -> MapPathFinder.TryFindPath(startCoord, goalCoord)
  -> MapManager.GetMoveCost(toCoord)
  -> MapManager.IsWalkable(toCoord)
```

当前 `MapPathFinder` 规则：

```text
四方向邻居：+X / -X / +Z / -Z
不直接走对角
邻居必须是 top logic tile
邻居必须通过 MapPathConnectionRules.CanConnect
邻居必须通过 MapManager.IsWalkable
上坡额外消耗 UphillExtraCost = 5
启发式使用 x/z/y 曼哈顿距离
```

路径平滑：

```text
NavigationController.UsePathSmoothing = true 时
MapPathSmoother.SmoothBySupercoverLineOfSight 会减少中间路径点
Line of sight 只允许同一 y 层
沿线所有经过格仍必须通过 MapManager.IsWalkable
```

移动终点：

```text
普通移动：
  点击地块时记录 raycast / 数学拾取的 world point
  A* 仍以点击点所在 top logic tile 为 goal
  最后一段移动终点使用点击 world point 的 X/Z，加角色站立高度

格子回退移动：
  SetPlayerDestination(coord)
    -> MapManager.GetTileWorldPosition(coord)
    -> 加角色高度偏移
```

也就是说，寻路目标仍是格子坐标，但最终移动点不一定是格中心。普通移动可以落到鼠标点击点；资源交互可以落到候选格内的动作站位。寻路系统本身不把“物体边缘外一点”作为 path node；物体边缘、交互距离、站位选择属于交互系统职责。

## 交互站位

点击资源时当前流程：

```text
ResourceInteractionController.TryStartAtPointer
  -> Start
  -> TryMoveNearTarget
  -> NavigationController.TryMoveToBestApproach
  -> ActionController.TryStart(ActionRequest, ActionCallbacks)
  -> OnMarker 结算拾取 / 采集 / 建矿
  -> OnCompleted 结束动作或开始下一次采集
```

这里的 `OnMarker` 是 `ActionController` 根据 Animator `normalizedTime` 触发的一次性阶段回调，不是动画文件里的 `Animation Event`。完整定义、取消语义、当前 Marker 和 timeout 数值见 `Docs/Modules/Island.md` 的“动作阶段点”。

`GetResourceInteractionPosition` 优先使用资源 Collider 的 `ClosestPoint`，否则使用地图对象坐标和局部偏移。这个点用于距离判断和转向，不是寻路算法的节点。

当前交互距离：

```text
InteractionDistance = 1.35f
```

当前已实现的行为：

```text
如果点击时已经在交互距离内，立即停下并执行交互
如果不在距离内，先从资源对象周围扫描可站立格
候选格必须可走、可寻路到达，并且站在该格时处于 InteractionDistance 内
在半径 MoveTargetSearchRadius 内选择路径成本较低的候选格作为 goal
资源交互最终移动点可以是候选格内的自定义 world position，不强制走到格中心
有自定义资源动作站位时，不因为刚进入最大 InteractionDistance 就提前执行，会继续走到站位附近
没有自定义动作站位时，移动过程中一旦进入交互距离，会提前停下并执行交互
每帧移动前也会先判断交互距离，避免已到交互范围后又被继续拉向格中心
如果一直没提前进入距离，走到格子路径终点后再尝试执行
```

当前站位规则：

```text
不要把交互物体本身加入 path node
物体通过 BlocksMove / BlocksBuild 参与阻挡
交互系统从目标对象周围找可站立格
对候选可站立格运行现有 MapPathFinder
选择路径成本最低的格子作为 goal
走到可站立格或中途进入交互距离后，转向目标并执行动作
```

当前候选站位以对象所在格为中心扫描外圈。以后资源支持多格 footprint 时，应改为取 footprint 外圈。

## 资源生命周期

当前资源分两类。

拾取资源：

```text
Branch / Loose Stone 等
interactionType = Pickup
PickupResource
  -> BagManager.TryAddItems
  -> RemoveResourceView
  -> MapManager.TryRemoveMapObject
  -> MapManager.MarkMapObjectRemoved
  -> Destroy(resourceView.gameObject)
```

拾取后会从 `currentMap.Objects` 和 `objectsByCoord` 移除，并保存到 `SaveRemovedMapObjectData`。寻路、建造、点击查询都会认为它不存在。

采集资源：

```text
Tree / Rock 等
interactionType = Gather
WorldGatherManager.TryGather
  -> WorldGatherNodeState.Consume
  -> RemainingTimes--
  -> respawnSeconds <= 0 且耗尽：RemoveResourceView
       -> MapManager.TryRemoveMapObject
       -> MapManager.MarkMapObjectRemoved
       -> Destroy(resourceView.gameObject)
  -> respawnSeconds > 0 或未耗尽：resourceView.RefreshNow
```

当前规则：

```text
respawnSeconds <= 0 的采集资源耗尽后，移除 MapObject 并保存 RemovedMapObjects
respawnSeconds > 0 的采集资源耗尽后，可以保留 MapObject，用 GatherNode 状态等待刷新
```

如果未来需要“资源耗尽后隐藏且未来刷新，同时耗尽期间不挡路”，需要新增运行时资源状态参与 `IsWalkable / IsBuildable`，例如：

```text
Stage = Normal / Depleted / Growing / Hidden
Visible
CanInteract
BlocksMove
BlocksBuild
RespawnAtUnixTime
```

这类状态应存在运行时动态数据里，需要跨存档恢复的字段进入存档。不要把这种运行时状态写回静态地图 JSON 或静态配置表。

## 移除和隐藏规则

逻辑上已经不存在的对象，应从地图对象数据和运行时索引移除：

```text
拾取后的树枝 / 散石 / 掉落物
不刷新的树 / 石头 / 矿耗尽后
拆除后的建筑
清理后的障碍物
一次性剧情物件
玩家移除的装饰或地表物
```

移除流程：

```text
MapManager.TryRemoveMapObject(objectId)
MapManager.MarkMapObjectRemoved(objectId)
Destroy 或隐藏对应 view
StorageManager.Save 时写入 SaveRemovedMapObjectData
读档 LoadMapData 时 ApplyRemovedMapObjects
```

逻辑上暂时不可见但以后会回来，或仍要保留占用的对象，可以只隐藏 view，但必须明确其运行时状态：

```text
会刷新 / 再生的资源
季节性显示对象
成长阶段中的农作物
剧情阶段暂时隐藏但以后还会出现的对象
树桩 / 矿坑 / 废墟等仍需占地的残留物
雾隐 / 视野隐藏的对象
```

只隐藏 view 时，要确认 `BlocksMove / BlocksBuild` 是否仍然符合产品规则。当前代码没有动态 BlocksMove 状态；只要 MapObject 仍在且阻挡字段为 true，就会继续阻挡。

## 存档边界

当前和地图相关的长期存档：

```text
SaveRemovedMapObjectData
  MapId
  ObjectId

SaveGatherNodeData
  ObjectId
  GatherConfigId
  RemainingTimes
  AvailableAtUnixTime

SaveWorldBuildingData
SaveFarmData
SaveWorldFarmPlotData
SavePlayerData
```

边界：

```text
RemovedMapObjects 记录“这个地图对象已经被移除，不应从静态地图 JSON 恢复”
GatherNodes 记录“这个采集节点仍在地图里，但采集次数 / 刷新时间改变”
WorldBuildings 记录玩家建造的建筑运行时状态
Farms / WorldFarmPlots 记录农田范围和作物状态
Player 记录玩家位置和朝向
```

不要把普通运行时状态直接写回 `Assets/Data/Map/{mapId}.json`。静态地图 JSON 是地图初始状态；运行中变化通过 SaveData 表达。

## 小地图和地图标记

经营小地图应绑定 `MapManager.CurrentMap` 和运行时管理器数据。

建议分层：

```text
底图层：由 MapData.Cells 生成，或使用预烘焙底图图片
静态点层：商店、固定建筑、任务点、传送点等，加载地图时生成一次
低频动态层：玩家建造建筑、任务状态变化后增删或换状态
动态点层：玩家、NPC 等运行时位置，按帧或低频刷新坐标
```

后续如果需要大量纯标记点，新增 `MapMarkerData`：

```text
MarkerId
Type: Shop / Building / Quest / NpcSpawn / Teleport / Custom
Coord 或 X/Y/Z
Icon
VisibleDefault
RelatedQuestId
RelatedNpcId
```

边界：

```text
MapData.Objects：真实存在、需要实例化或影响占用 / 交互 / 存档的对象
MapMarkerData：地图 UI 标记、导航、任务提示、商店图标等表现数据
```
