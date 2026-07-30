# Codex Project Memory

这是给 Codex 新对话的最小入口，不是完整设计文档。读完本文件后，只按当前任务继续读相关模块。

## Project Basics

```text
Workspace: D:\Cube
Engine: Unity 2022
User language: Chinese
Main direction: island survival / island management, with separate tower-defense combat mode
Current focus: management UI, Story -> Quest flow, Blueprint production, data-driven configuration
```

重要路径：

```text
Assets/Scripts/Framework
Assets/Scripts/Game
Assets/Scripts/Game/UI/Management
Assets/Scripts/Game/Island
Assets/Scripts/Game/Quests
Assets/Scripts/Game/Story
Assets/Scripts/Game/Blueprints
Assets/Scripts/Game/Data/Generated

Assets/Arts/UI/Panels
Assets/Arts/UI/Icons
Assets/Arts/Map

Data/Excel
Data/Defines
Assets/Data/Bin
Assets/Data/Json
Docs
```

## Read Only What You Need

```text
UI framework work:
  Docs/Architecture/UIFramework.md

Management UI work:
  Docs/Architecture/UIFramework.md
  Docs/Modules/ManagementUI.md

Module tables / tools / related files:
  Docs/Architecture/ModuleWorkMap.md

Island gameplay work:
  Docs/Product/ManagementMode.md
  Docs/Modules/Island.md

Quest / Story / Blueprint work:
  Docs/Modules/QuestStoryBlueprint.md
  Docs/Product/ProgressionAndQuests.md

Story-only work:
  Docs/Modules/Story.md
  Docs/Product/ProgressionAndQuests.md

Data / Luban / localization work:
  Docs/Architecture/DataPipeline.md

Map / minimap / tile art work:
  Docs/Architecture/MapRuntime.md
  Docs/Modules/MapAndTileArt.md

Tower defense / ability work:
  Docs/Product/TowerDefenseMode.md
  Docs/Modules/TowerDefense.md
  Docs/Modules/AbilityAndSkill.md

Naming or file movement:
  Docs/Decisions/0001-naming-rules.md
  Docs/Architecture/CodebaseOverview.md

Project audit / cleanup:
  Docs/Audits/ProjectMidpointAudit-2026-07-09.md
```

## Hard Rules

- New code defaults to no `World` or `Game` prefix. Existing `World*` classes may stay because Prefabs and serialized data may reference them.
- New Luban / Excel tables follow the same rule: do not add `World` or `Game` prefixes. Existing `world_*` tables are historical and should not be used as templates for new tables; use `tool.xlsx` / `ToolConfig` / `TbTool`, not `world_tool.xlsx` / `WorldToolConfig` / `TbWorldTool`.
- Quest domain uses `Quest`, not `Task`, and not `WorldQuest`.
- Production / crafting config uses `Blueprint`, not `Recipe` or `WorldRecipe`.
- Story save model is `StorageManager.StoryData`, reached through `SaveData.Story`; do not recreate `Assets/Scripts/Game/Story/StoryData.cs`.
- Bag domain uses `Bag`, not `Inventory`; do not create new bag code or UI folders named `Inventory`.
- Regenerate data with `Data/gen_all.bat`; do not suggest running only `Data/gen_client.bat`.
- Runtime config source is `Assets/Data/Bin/*.bytes`; JSON is for inspection and debugging.
- Do not manually edit Luban generated code or generated data as a lasting solution.
- Move Unity scripts with `.meta`; do not delete and recreate scripts that may be referenced by Prefabs.
- UI nodes should be wired through serialized fields. Do not reintroduce runtime `Transform.Find` for stable HUD nodes.
- Avoid changing Prefab layout from runtime code unless the script is explicitly responsible for generated lists, grids, or layout.
- The current shell is Windows PowerShell 5.1. Prefer simple commands and single-quoted `rg` patterns; split complex searches instead of using dense quoted regex.

## Current UI Agreements

- UI framework identity is currently `prefabPath`; `Show`、`Hide`、`IsShown` all work by path.
- `RightOutside` supports desktop right-click outside close. Mobile touch / click outside is mapped by the framework.
- Common management panels should support `CloseButton | Back | RightOutside`.
- Panel outside close is handled by the framework-created bottom `PanelOutsideBlocker` + `UIOutsideClickDetector` + `PanelManager`; Panel no longer creates outside mask objects.
- A panel's visual `Overlay` must not receive raycasts or bind its own close handler. `PanelOutsideBlocker` is below every UI layer, so it catches blank space without blocking normal UI.
- Outside close consumes the pointer for that frame through `UIManager`; world input must call `WorldPointerPicker.IsPointerOverUi()` so a right-click that closes Quest / TechTree does not also move or interact in the world.
- BottomBar 入口状态图标目前只用于 Bag 的 Open / Close 箭头。Build / ToolKit 没有 Open / Close 状态图标设计，不要补。
- Exclusive group is for same-level independent panels, currently Build / ToolKit / Production / Quest / TechTree / Menu. Bag is a BottomBar child node and is coordinated locally with those panels.
- Stack group is for panel-level return history. Do not split ordinary child nodes into `UIPanel` just to use Stack.
- Sound / Language / Save currently exist as independent UIPanel and can use Stack short term. Product-wise they are closer to Menu internal pages and can later become serialized child nodes under Menu.
- Tooltip 使用 `UIManager.Instance.Tooltips`、全局唯一 `TooltipView` 和 `UILayer.Tooltip = 35`；外部 API 是 `Show` / `Hide`。每个 Tooltip 都必须有 `TooltipData` 来源：固定内容来自 Trigger，动态塔 / 物品 / 技能由各自可复用 View 按业务 ID 统一 `Bind`，已有 Pointer 组件可直接调用 Manager；不要误解成只有塔需要提供数据。`TooltipTrigger` 不带 UI 前缀且没有视觉职责。Tooltip prefab 必须关闭 raycast，并在离开、点击、拖拽、禁用或 UI 清理时隐藏。

## Current Feature Status

Data:

```text
Quest / Blueprint / Story 已接入正式 Luban 表
Ability / Skill / Item / Localization / Map / Tower / Wave / Tech / World* 表也在 Tables 中
```

Management UI:

```text
WorldMainPanel 是常驻 HUD 容器
TopBar / BottomBar / EntryBar / RightBar / BuildingDetail 是 prefab 上的 MonoBehaviour 子模块
RightBar/BattleEntry 是经营到塔防的模式入口，当前调用 MapManager.Instance.LoadBattleMap(30950001)
Build / ToolKit / Production / Quest / TechTree / Menu 入口由 WorldPanelEntryController 注册
Bag 是 BottomBar 内部节点，由 WorldBottomBarPanel 开关；WorldMainPanel 协调它与独立浮动面板互斥
独立浮动面板的互斥关闭交给 PanelManager Exclusive group
浮动面板布局使用 WorldFloatingPanelLayout
```

Quest / Story / Blueprint:

```text
Story 可触发 QuestEvent
Quest 支持多目标、前置、追踪、完成、领取、奖励发放、保存和消息
Blueprint 完成可触发 QuestEventType.BlueprintCompleted
当前开局链路是 Story 10001 -> 拾取 -> 建 House -> 做斧 -> 砍树 -> 做镐 -> 挖石 -> 建 Workbench
任务奖励 Toast / 获得反馈仍需完善
```

Island opening resources:

```text
默认世界地图是 Assets/Data/Map/1001.json
地图编辑器放置 Resource 时使用 resource.id
30300008 Branch / 30300009 Loose Stone 是开局拾取物
30300001 Tree 需要斧，30300002 Rock 需要镐
ToolKit 新存档默认空槽，获得工具后自动放入；加载已有存档时会把已拥有的工具补入空槽；不要假设玩家开局已有斧/镐/锄
开局基础工具 Blueprint 的 buildingId = 0，不需要 Workbench；QuestPanel 详情按钮直接“制作”
Bag slot 0-9 是 HotBar，10+ 是 Bag；BottomBar 持有唯一 BagDragController，快捷栏和 Bag 子节点之间支持带跟随图标的拖动移动/交换，不拆堆，且不跨其它 Panel
```

Story:

```text
StoryManager 从 Story / StoryStep Luban 表加载配置，StoryStep 是正式推进单位，不使用 Beat
StoryPanel prefab 已绑定文字卡片、静态插画视口和 GuideOverlay；支持 Text / Illustration / Mixed / Guide
StoryData 保存 CurrentStoryId、CurrentStepIndex、CompletedStoryIds，可从未完成 Step 恢复
DialogManager / EventTriggerSystem 目前只是占位
```

Map:

```text
地图由 MapData 运行时生成，不是整张场景 Prefab
经营地图入口是 MapManager.Instance.LoadWorldMap(worldMapId)
塔防地图入口是 MapManager.Instance.LoadBattleMap(mapConfigId)；不要重新增加含糊的 LoadMap
两种入口共用同一个 MapManager、CurrentMap、LoadMapData 和 CreateMap，不维护双份地图运行时状态
MapManager 用 partial 文件按 World / Battle / Loading / Persistence 组织；这只是代码分区，不是多个 Manager 实例
塔防 UI 资源位于 Assets/Arts/UI/Panels/Battle；塔和技能图标分别位于 Assets/Arts/UI/Icons/Towers、Assets/Arts/UI/Icons/Skills
MapData.Objects 是真实地图对象
纯 UI 标记后续应进入 MapMarkerData 或等价结构
寻路使用 MapPathFinder 格子 A*，不是 NavMesh；角色移动目标是可走格中心附近
MapManager.IsWalkable / IsBuildable 看 objectsByCoord 和 BlocksMove / BlocksBuild，不看 view/collider 是否隐藏
逻辑上消失的地图对象要 TryRemoveMapObject + MarkMapObjectRemoved；只隐藏 view 仍可能挡路
拾取资源会移除 MapObject；采集资源 respawnSeconds <= 0 且耗尽后也会移除 MapObject，respawnSeconds > 0 则保留并等待刷新
资源交互站位由 ResourceInteractionController 生成候选，NavigationController 选择可走、可到达、处于交互距离内的格子；不要把资源本体加入 path node
地图编辑器放置资源默认使用 Excel / Luban 的 resource 阻挡属性；当前 MapObjectData 存最终值，不记录是否覆盖
采集工具动作只使用 Assets/Arts/Character/Player 内的玩家动画资源；当前 UseTool 使用 Meshy_AI_Forestbound_Adventure_biped_Animation_Heavy_Hammer_Swing_withSkin.fbx，但必须通过 WorldPlayer.controller 的 UseTool Upper Body 层和 WorldPlayer_UseToolUpperBody.mask 播放，只影响手臂/手指，避免 Heavy_Hammer_Swing 的全身后撤姿态把角色视觉上弹出去。不要引用 Assets/Arts/FBX/CharacterFBX 的其他角色动作，也不要叠加额外程序化采集动作
```

## Documentation Rule

如果发现文档与代码冲突，先读代码和 Prefab，再更新对应主题文档。重要框架规则、业务流程、命名约定、易踩坑细节或长期设计决策发生变化时，必须同步更新 Markdown 源文档，并重新生成 `Docs/Human/index.html`。不要把过时探索继续堆进 `CodexProjectMemory.md`，也不要只把长期规则写在聊天记录或 HTML 里。
