# Island Module

本文记录岛屿经营业务模块。

## 目录

```text
Assets/Scripts/Game/Island
  Buildings
  Calendar
  Exploration
  Farming
  Gathering
  Income
  Mining
  Production
  Resources
  Tech
  Tools
  Workers
  WorldMessage.cs
```

## 资源和背包

长期经营库存使用：

```text
Assets/Scripts/Game/Items/ItemManager.cs
Assets/Scripts/Game/Items/ItemStack.cs
```

全项目物品定义：

```text
Data/Excel/item.xlsx
Assets/Scripts/Game/Items/ItemIds.cs
```

塔防单局物品或金币由 `BattleItemManager` 保存，不要直接复用为长期库存。塔防结算时应通过明确奖励流程写回 `ItemManager`。

物品使用规则：

```text
Bag / HotBar 点击 -> BagManager.TryUseSlot -> ItemManager.Use
工具点击只完成选择，不算真实使用
未配置效果的食物、消耗品或其它物品返回失败
采集、建矿、开垦、种植成功后，由业务完成点调用 ItemManager.NotifyUseCompleted
QuestEventType.UseItem 只记录已经完成的使用，不负责执行物品效果
```

## 采集和工具

采集相关：

```text
Assets/Scripts/Game/Island/Exploration/GameplayController.cs
Assets/Scripts/Game/Island/Exploration/NavigationController.cs
Assets/Scripts/Game/Island/Exploration/ActionController.cs
Assets/Scripts/Game/Island/Exploration/ResourceInteractionController.cs
Assets/Scripts/Game/Island/Exploration/PlacementController.cs
Assets/Scripts/Game/Island/Tools/ActionToolResolver.cs
Assets/Scripts/Game/Island/Tools/ToolKitDefinitions.cs
Assets/Scripts/Game/Island/Tools/ToolKitManager.cs
```

当前工具规则：

```text
树 -> 斧
石头 / 矿 -> 镐
农田开垦 -> 锄
钓鱼 -> 鱼竿
```

ToolKit 当前能力：

```text
默认 10 个格子
新存档默认工具槽为空
获得工具时自动放入 ToolKit 并选中；加载已有存档时，也会把已拥有的工具补入空槽
只允许使用玩家实际拥有的工具；旧存档里只有槽位但没有对应物品时不能使用
可点击选择工具
可拖动到空格
可拖动到另一个工具上交换
```

开局地图资源和任务对应：

```text
MapObjectData.ObjectType = Resource
MapObjectData.ConfigId = resource.id

30300008 Branch      -> 拾取 -> Wood
30300009 Loose Stone -> 拾取 -> Stone
30300001 Tree        -> 斧采集 -> Wood
30300002 Rock        -> 镐采集 -> Stone
```

资源生命周期和寻路关系见 `Docs/Architecture/MapRuntime.md`。当前关键规则：

```text
寻路 / 建造占用看 MapManager 的 objectsByCoord，不看 WorldResourceView 是否隐藏
拾取资源成功后会 TryRemoveMapObject + MarkMapObjectRemoved
采集资源 respawnSeconds <= 0 且耗尽后会 TryRemoveMapObject + MarkMapObjectRemoved
采集资源 respawnSeconds > 0 耗尽后保留 MapObject，用 GatherNode 等待刷新
资源交互会先从目标周围选择可走、可到达、在交互距离内的站位，再移动执行动作
GameplayController 只路由输入和生命周期；NavigationController 负责移动，ActionController 负责动作阶段，ResourceInteractionController 负责资源结算
采集工具动作只使用 Assets/Arts/Character/Player 内的玩家动画资源；当前 UseTool 使用 Meshy_AI_Forestbound_Adventure_biped_Animation_Heavy_Hammer_Swing_withSkin.fbx，但必须通过 WorldPlayer.controller 的 UseTool Upper Body 层和 WorldPlayer_UseToolUpperBody.mask 播放，只影响手臂/手指，避免 Heavy_Hammer_Swing 的全身后撤姿态把角色视觉上弹出去。不要引用 Assets/Arts/FBX/CharacterFBX 的其他角色动作，也不要叠加额外程序化采集动作
```

`ActionController` 的上层入口统一为：

```csharp
bool TryStart(ActionRequest request, ActionCallbacks callbacks = default);
bool Stop(ActionStopReason reason, ActionExitMode exitMode);
```

规则：

```text
TryStart 表示动作可能因已有动作、无效请求或表现层未就绪而失败
Stop 是幂等命令；无当前动作时仍可通过 ToIdle / ToMove 修正表现状态
TryStart 只有在 Animator 参数、State、Layer 和所需工具模型都准备好时才返回 true
OnMarker 用于拾取、采集、建矿、开垦、命中、投放等一次性业务结算
OnCompleted 表示 Animator 动作达到 normalizedTime >= 1 或已经退出动作状态；只有动画未进入或未结束时才使用 timeout 兜底
拾取、资源交互和农田动作统一通过 ActionController 播放或停止，不直接调用 WorldPlayerView 的动作 API
移动是连续 locomotion，由 NavigationController 更新 WorldPlayerView.SetMoveSpeed，不作为离散动作进入 ActionController
工具拥有与业务条件由上层检查，ActionController 只管理播放、阶段、完成和停止
WorldPlayerView 是 Animator 和工具模型的低层适配器，不维护独立的动作完成计时
```

## 动作阶段点

代码中的 `Marker` 表示一次动作里的业务阶段点，不是 Animation Clip 时间轴上的 `Animation Event`。

`ActionRequest.MarkerNormalizedTime` 使用 Animator 的归一化进度，取值范围是 `0..1`。例如 `0.55` 表示动作播放到约 55% 时触发，不是第 55 帧，也不是 0.55 秒。动画状态速度改变后，Marker 对应的真实时间会一起改变。

当前流程：

```text
TryStart
  -> WorldPlayerView 触发 Animator 状态
  -> ActionController 每帧读取 normalizedTime
  -> normalizedTime >= MarkerNormalizedTime
  -> OnMarker 触发一次业务结算
  -> 动作达到 normalizedTime >= 1 或退出状态
  -> OnCompleted 触发动作完成
```

Marker 的语义：

| 情况 | 结果 |
| --- | --- |
| 到达 Marker | `OnMarker` 只触发一次 |
| Marker 前调用 `Stop` | 不触发 `OnMarker`，不结算 |
| Marker 后调用 `Stop` | 已完成的结算不回滚 |
| 动画提前退出或超过 timeout | 触发 Marker 作为兜底，然后完成动作 |

当前用途：

| 动作 | Marker 结算内容 | Marker | timeout |
| --- | --- | ---: | ---: |
| 拾取 | 物品进入背包、移除地图资源 | `0.55` | `0.75s` |
| 采集 | 扣减采集次数、发放奖励、记录工具使用 | `0.55` | `1.25s` |
| 建矿 | 创建矿场、记录工具使用 | `0.55` | `1.25s` |
| 开垦 | 创建农田、记录工具使用 | `0.55` | `1.25s` |

这些值当前是代码常量：

```text
ResourceInteractionController
  PickupCollectNormalizedTime
  PickupTimeoutSeconds
  ToolActionMarkerNormalizedTime
  ToolActionTimeoutSeconds

PlacementController
  CultivateMarkerNormalizedTime
  CultivateTimeoutSeconds
```

当前实现不要求在动画资源上逐个添加事件，适合同类动作共用统一阶段比例。以后如果需要精确到斧头接触树木的具体帧、一个动作内多个命中点，或动作片段速度变化很大，再改为 Animation Event 或 StateMachineBehaviour 向 `ActionController` 报告阶段事件。

如果后续做会刷新的资源，不能只靠隐藏 view 表示状态，需要显式运行时状态控制：

```text
RemainingTimes
RespawnAtUnixTime
Visible
CanInteract
BlocksMove / BlocksBuild
Stage
```

## 农田

```text
Assets/Scripts/Game/Island/Farming
Assets/Scripts/Game/UI/Management/WorldFarmPanel.cs
Assets/Scripts/Game/UI/Management/WorldFarmPanelView.cs
Assets/Scripts/Game/UI/Management/WorldFarmSeedView.cs
Data/Excel/world_crop.xlsx
```

当前规则：

```text
FarmPanel 是独立 UIPanel
打开时贴 BottomBar 快捷栏上方
左侧 Info 显示作物图标、名称、产量
种子消耗 = 地块数量 * 作物种子系数
种子系数应配置在作物数据里
```

## 建筑和生产

经营建筑不复用塔防 `TowerConfig`，经营建造不直接复用塔防 `TowerBuildManager`。

相关配置：

```text
Data/Excel/world_building.xlsx
Data/Excel/world_building_level.xlsx
Data/Excel/world_building_income.xlsx
Data/Excel/world_cost.xlsx
Data/Excel/reward.xlsx
```

生产/制作配置使用 `Blueprint`，见 `Docs/Modules/QuestStoryBlueprint.md`。

## Calendar

```text
Assets/Scripts/Game/Island/Calendar/CalendarManager.cs
Assets/Scripts/Game/Island/Calendar/Season.cs
```

规则：

```text
一年 4 个月
每月 28 天
每个月对应一个季节
默认 600 秒真实时间 = 1 游戏日
```

`CalendarManager` 提供 `SetTimeOfDay`、`SetDateTime`、`AdvanceMinutes/Hours/Days`、`SetGameTimeScale`。UI 不自己维护独立时间，统一从 `CalendarManager` 取值。








