# Tower Defense Module

本文记录塔防业务模块入口。产品边界见 `Docs/Product/TowerDefenseMode.md`。

## 目录

```text
Assets/Scripts/Game/TowerDefense
  AbilityAdapters
  Base
  Battle
  Effects
  Enemy
  Messages
  Settlement
  Targeting
  Tower
  Wave
```

相关旧目录仍存在：

```text
Assets/Scripts/Game/Tower
Assets/Scripts/Game/Wave
Assets/Scripts/Game/Battle
```

## 地图入口

经营大地图和塔防战斗地图统一由 `MapManager.Instance` 加载，但使用两个语义明确的入口：

```csharp
MapManager.Instance.LoadWorldMap(worldMapId);
MapManager.Instance.LoadBattleMap(mapConfigId);
```

- 经营主界面的正式入口是 `WorldMainPanel/RightBar/BattleEntry`，当前默认加载 `map.xlsx` 的 `30950001`。
- `LoadWorldMap` 接收世界地图 id，加载 `Assets/Data/Map/{worldMapId}.json`，创建经营运行时并显示 `WorldMainPanel`。
- `LoadBattleMap` 接收 `map.xlsx` 的配置 id，通过 `MapConfig.MapId` 定位地图 JSON，初始化战斗金币、基地、波次和 `BattlePage`。
- 战斗内重开和下一关分别使用 `RestartCurrentBattleMap`、`LoadNextBattleMap`；相关 API 保留 `Battle` 语义。
- 两个入口继续复用 `MapManager.LoadMapData`、地图索引和 `CreateMap`。不要新增含糊的 `LoadMap` 入口。
- 当前只维护一个 `MapManager.CurrentMap`。不要把 World / Battle 拆成两套同时存在的地图运行时状态；如果以后确实需要双地图常驻，再提取共享的 `MapRuntimeContext`。
- `MapManager` 使用 `partial` 按职责组织：`MapManager.World.cs` 放经营地图入口与后处理，`MapManager.Battle.cs` 放塔防入口、HUD 和关卡导航，`MapManager.Loading.cs` 放共用数据加载，`MapManager.Persistence.cs` 放地图移除物存档；`MapManager.cs` 保留共享状态、地图生成、索引和查询。

## UI 资源

```text
Assets/Arts/UI/Panels/Battle        BattlePage、战斗 Popup、子 Prefab 和模块图片
Assets/Arts/UI/Icons/Towers         防御塔图标
Assets/Arts/UI/Icons/Skills         战斗技能图标
```

旧路径 `Assets/Arts/UI/TowerDefense` 已停用。Prefab 运行时路径、本地化绑定工具、AssetBundle 收集配置以及 `tower.xlsx` / `skill.xlsx` 必须使用上面的新路径。

## 战斗 UI 结构

`BattlePage.prefab` 是完整战斗流程的 `UIPage`。`TopPanel`、建塔、道具、目标信息、技能、战斗控制和小地图分别是内嵌 `UIPanel`，由 `UIEmbeddedPanelGroup` 统一传递生命周期；Panel 内的 Slot、进度条、卡片和模型描述组件使用 `MonoBehaviour`。`TopPanel` 按当前 Prefab 的 `Base/Hp`、`Coin`、`Wave`、`Enemy` 层级显式绑定，避免依赖同名节点扫描。

建塔列表的公共 `TowerCard.prefab` 根节点挂一个 `TooltipTrigger`，由 `TowerBuildCardView` 在 `Init` 中统一执行一次 `tooltipTrigger.Bind(CreateTooltipData)`；这段绑定属于可复用的卡片类型，不是为每座塔实例分别编写。`CreateTooltipData` 按实例的 `TowerId` 读取内容，名称和描述分别使用 `localization.xlsx` 中的 `tower.{id}.name`、`tower.{id}.desc`。不增加 Tooltip 专用 Excel 表，也不为每种塔创建不同的卡片 Prefab。

`BattleControlPanel/SystemButton` 复用经营大地图的系统菜单 `WorldMenuPanel.prefab`，通过 `PanelManager` 打开；语言和音量继续进入同一套 Menu 子面板与共享设置 API。结算仍是独立 Popup：

```text
Assets/Arts/UI/Panels/Menu/MenuPanel.prefab
BattleResultPopup.prefab
```

同一个 `WorldMenuPanel` 会按运行模式切换入口：

- 经营大地图显示 `Save`，隐藏 `Retreat`。
- 战场显示 `Retreat`，隐藏 `Save`；战斗过程中不能从系统菜单存档。
- `Retreat` 的中文统一为“撤退”，不使用含义过弱的“返回大地图”，也不使用可能被理解为正常结算的 `Finish`。
- 点击撤退必须先确认；确认文案明确说明当前战斗立即结束且不获得本场奖励。确认后调用 `MapManager.RetreatFromBattle()` 清理战斗运行时并重新加载进入战斗前的世界地图，不触发胜利、失败或奖励结算流程。
- Prefab 节点为 `MenuPanel/Content/Scroll View/Viewport/Content/Retreat`，序列化字段为 `WorldMenuPanel.retreatButton`。菜单入口必须存在于 Prefab，运行时只切换显隐，不创建或克隆该入口。

撤退相关文本来自 `localization.xlsx`：

```text
ui.td.menu.retreat
ui.td.menu.retreat_confirm_message
ui.common.cancel
```

战斗 UI 的运行时节点引用必须全部来自 Prefab 序列化。`BattleUiPrefabBinder` 负责一次性绑定和校验 Prefab；运行时代码不得用名称或层级扫描兜底。

目标操作区属于 `InfoPanel/Action`，使用 Prefab 中预先制作的 `Action1`、`Action2`、`Action3` 三个按钮，不在运行时创建或克隆按钮。目标配置只填写 `actionGroupId`，`battle_target_action.xlsx` 决定该组包含哪些动作；点击目标后再根据实时状态过滤动作，没有动作时隐藏整个 `Action`。当前塔组 `100` 包含升级和出售：升级同时要求 `tower.canUpgrade` 且 `tower_level.xlsx` 存在下一级，价格读取下一级的 `upgradeCostItemId/upgradeCost`；出售要求当前等级 `sellGoldRate > 0`，返还金额按等级数据实时计算。动作表不重复保存价格或出售金额。

`ItemPanel` 是与 `InfoPanel` 平级的独立道具面板，只显示当前持有且可在战斗中使用的道具；目标操作与道具使用不共享组件或事件。

当前已接入暂停、倍速、音量持久化、语言切换、自动下一波、技能冷却、目标信息、道具执行器入口和世界血条。暂停控制在运行时显示 `PauseButton`，暂停后切换为 `PlayButton`，恢复时回到暂停图标。小地图仍保持禁用，不属于当前实现范围。目标模型预览使用 `BattleTargetPreviewDescriptor` 显式声明 Renderer、Animator 和需要禁用的运行时组件，并使用独立 Layer/Camera/Light culling mask。

当前数据约束：

- `tbitem.json` 的 27 项物品目前全部是 `useScope = 0`，因此战斗道具栏已具备校验、扣除和处理器注册链路，但还没有可实际释放的战斗道具；新增战斗物品时必须同时配置使用范围并注册对应处理器。
- `tbnpc.json` 仍有 NPC 引用 `Assets/Arts/Character/Pirate/Pirate_Male.prefab`，仓库当前没有该 Prefab。NPC 数据修复前，目标模型预览会回退到图标，且使用该 NPC 的战斗生成也需要资源侧补齐。
- `NotoSansSC-Regular SDF` 使用动态 atlas；`BattleUiPrefabBinder` 会保证 atlas 纹理可读，避免战斗 Popup 新增中文字符时生成失败。

## NPC 动画

塔防 NPC 动画由 `Assets/Scripts/Game/TowerDefense/Enemy/NpcAnimationController.cs` 管理，不复用经营玩家的 `ActionController`。

```text
Walk   Bool     控制移动循环
Attack Trigger  播放攻击动作
Die    Trigger  播放死亡动作
```

死亡动作通过 Animator 状态进度等待完成，超时使用 `Time.unscaledTime`，NPC 销毁时通过 cancellation token 终止等待。Animator 生成工具和运行时代码必须使用相同的 `Walk / Attack / Die` 参数名。

## 与 Ability

新塔防技能优先走 Ability 系统，塔防对象通过 `Assets/Scripts/Game/AbilityAdapters` 适配到 Ability core。

## 与经营

- 塔防结算奖励写回经营长期系统。
- 塔防单局状态不要直接混进经营存档。
- 经营建筑和塔防防御塔可以共享配置概念，但 manager 和运行时状态要分开。








