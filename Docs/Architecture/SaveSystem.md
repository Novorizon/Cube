# Save System

本文记录当前存档边界。需要改具体存档字段时，先读代码。

## 主要代码

```text
Assets/Scripts/Game/Save/SaveData.cs
Assets/Scripts/Game/Save/StorageManager.cs
```

## Calendar 存档

游戏内日历由 `CalendarManager` 管理：

```text
Assets/Scripts/Game/Island/Calendar/CalendarManager.cs
Assets/Scripts/Game/Island/Calendar/Season.cs
```

相关存档字段：

```text
SaveData.SavedAtUnixTime
SaveCalendarData.Year
SaveCalendarData.Month
SaveCalendarData.Day
SaveCalendarData.Hour
SaveCalendarData.Minute
SaveCalendarData.AccumulatedRealSeconds
```

规则：

- `SavedAtUnixTime` 记录真实保存时间。
- `SaveCalendarData` 记录游戏内时间和残余累计秒。
- `StorageManager.Load()` 读档后根据离线真实时间推进 `CalendarManager`。
- UI 不维护独立时间，统一从 `CalendarManager` 读取。

## 注意

新增长期系统状态时，要明确它属于经营长期存档、塔防单局状态，还是纯 UI 临时状态。塔防单局金币、临时 Buff、波次进度不要直接混入长期经营库存。

## 地图对象存档

静态地图 JSON 表示地图初始状态。运行中被移除或状态改变的地图对象不要写回 `Assets/Data/Map/{mapId}.json`，应写入存档。

当前地图相关字段：

```text
SaveData.RemovedMapObjects
SaveData.GatherNodes
SaveData.WorldBuildings
SaveData.Farms
SaveData.WorldFarmPlots
SaveData.Player
```

边界：

```text
SaveRemovedMapObjectData
  表示某张地图上的某个 MapObject 已经逻辑消失，读档时 ApplyRemovedMapObjects 会从 MapData.Objects 过滤掉。

SaveGatherNodeData
  表示采集节点仍在地图对象里，但 RemainingTimes / AvailableAtUnixTime 改变。

SaveWorldBuildingData
  表示玩家建造建筑的运行时状态。

SaveFarmData / SaveWorldFarmPlotData
  表示农田范围、地块和作物状态。

SavePlayerData
  表示玩家在地图中的位置和朝向。
```

规则：

```text
逻辑上已经不存在的对象：使用 MapManager.TryRemoveMapObject + MarkMapObjectRemoved，进入 RemovedMapObjects。
仍在地图上、只是采集次数或刷新时间变化的资源：进入 GatherNodes。
只隐藏 view 不等于从地图逻辑移除；寻路 / 建造仍看 MapManager 的对象索引。
```

## Story 存档

Story 存档挂在：

```text
SaveData.Story
StorageManager.StoryData
```

当前字段：

```text
CurrentStoryId
CurrentStepIndex
CompletedStoryIds
```

剧情 Step 改变时会保存 `CurrentStepIndex`。世界 UI 就绪后，`StoryManager.TryStartAutoStories()` 会先恢复未完成的 `CurrentStoryId`，并从该 Step 重新打开 StoryPanel；旧存档缺少字段时默认值为 0。








