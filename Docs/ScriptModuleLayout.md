# Script Module Layout

本文档用于规划 `Assets/Scripts` 的模块分类、文件夹命名和后续迁移规则。当前阶段先定结构，不直接移动脚本，避免 Unity 场景和 Prefab 引用混乱。

## 原则

- `Assets/Arts` 不参与本次整理，不移动美术资源。
- 已挂在 Scene、Prefab、ScriptableObject 上的 `MonoBehaviour` / `ScriptableObject` 脚本，移动时必须连同 `.meta` 一起移动，保持 GUID 不变。
- 第一阶段优先新增文件夹和新代码的归属规则；旧代码分批迁移。
- 不为了目录整洁修改类名、字段名、序列化字段名。需要改名时使用 `[FormerlySerializedAs]`。
- `Editor` 代码必须放在任意名为 `Editor` 的文件夹下，避免进入运行时编译。
- Luban 生成代码保持在固定生成目录，避免手动整理后被重新生成覆盖。
- 没有必要立刻引入 asmdef；如果以后接 HybridCLR，再按 AOT/Hotfix 边界补 asmdef。

## 推荐顶层结构

```text
Assets/Scripts
├── Bootstrap
├── Framework
├── Game
├── MapEditor
├── Ability
├── Skill
├── Tools
└── Sandbox
```

### Bootstrap

游戏启动、模式切换和热更新入口。

```text
Assets/Scripts/Bootstrap
├── GameEntry.cs
├── BootFlowManager.cs
├── GameModeManager.cs
└── GameMode.cs
```

当前对应：

- `Assets/Scripts/GameEntry.cs`

后续建议：

- `GameEntry` 保持很薄，只负责启动 `BootFlowManager`。
- `BootFlowManager` 负责资源初始化、版本检查、HybridCLR、配置加载、进入主菜单。
- `GameModeManager` 负责经营、建造、战前准备、战斗、剧情对话之间的状态切换。

### Framework

跨玩法通用基础设施，不直接包含具体游戏规则。

```text
Assets/Scripts/Framework
├── Resource
├── UI
├── InputSystem
├── Message
├── Event
├── Singleton
├── Task
├── Threading
├── Effect
└── Logging
```

当前基本已经符合：

- `Assets/Scripts/Framework/Resource`
- `Assets/Scripts/Framework/UI`
- `Assets/Scripts/Framework/InputSystem`
- `Assets/Scripts/Framework/Message`
- `Assets/Scripts/Framework/Event`
- `Assets/Scripts/Framework/Singleton`
- `Assets/Scripts/Framework/Task`
- `Assets/Scripts/Framework/Threading`
- `Assets/Scripts/Framework/Effect`

建议调整：

- `Logger.cs`、`NetLogger.cs` 后续可放入 `Framework/Logging`。
- `Framework/UI/Samples` 可以保留为样例，也可以后续移动到 `Sandbox/Samples/UI`。

### Game

正式游戏运行时逻辑。建议按“领域模块”拆，而不是按 Manager 堆在一起。

```text
Assets/Scripts/Game
├── Core
├── Data
├── Map
├── Island
├── Build
├── TowerDefense
├── Characters
├── Items
├── AbilityAdapters
├── UI
├── Input
├── Camera
├── Effects
├── Save
├── Story
└── Debug
```

#### Game/Core

运行时通用游戏状态。

```text
Game/Core
├── GameClock.cs
├── GameSession.cs
├── GameMode.cs
└── GameModeManager.cs
```

适合放：

- 天数、时间、暂停、加速
- 当前游戏阶段
- 当前存档会话状态

#### Game/Data

配置读取和 Luban 生成代码。

```text
Game/Data
├── DataManager.cs
├── ConfigTableReader.cs
├── Generated
└── GeneratedPlaceholders
```

当前对应：

- `Assets/Scripts/Game/Data`

规则：

- `Generated` 由 Luban 生成，尽量不手动改。
- 手写的数据适配器、读取器放在 `Game/Data` 根目录或 `Game/Data/Runtime`。

#### Game/Map

运行时地图、格子、地形、地块表现、地图寻路。

```text
Game/Map
├── Data
├── Runtime
├── Rendering
├── Pathfinding
└── Config
```

建议归属：

- `MapData.cs`、`MapTileData.cs`、`MapDecorationData.cs` -> `Game/Map/Data`
- `MapTileType.cs`、`MapTileOverlay.cs`、`MapDirection.cs`、`MapTileRule.cs` -> `Game/Map/Data`
- `MapTilePrefabConfig.cs`、`MapDecorationPrefabConfig.cs` -> `Game/Map/Config`
- `MapManager.cs`、`TileView.cs` -> `Game/Map/Runtime`
- `FlatTileVisual.cs` -> `Game/Map/Rendering`
- `MapDataAStarPathFinder.cs`、`MapPathFinder.cs`、`MapPathCellInfo.cs`、`MapOverlayConnectionRules.cs` -> `Game/Map/Pathfinding`

说明：

- 目前这些数据有一部分在 `MapEditor/Runtime`，但它们实际被编辑器和运行时共用。后续可以迁移到 `Game/Map`，让 `MapEditor` 只保留编辑器窗口和工具。

#### Game/Island

岛屿经营模拟专属逻辑。

```text
Game/Island
├── Resources
├── Production
├── Buildings
├── Farming
├── Mining
├── Exploration
└── Workers
```

后续新增：

- 木材、石头、食物、矿石等长期资源
- 农田、矿场、伐木场、工坊
- NPC 工作分配
- 岛屿探索和区域解锁

#### Game/Build

通用建造系统。经营建筑和塔防塔都可以复用放置规则。

```text
Game/Build
├── BuildManager.cs
├── BuildValidator.cs
├── BuildPreview.cs
├── BuildCommand.cs
└── BuildCost.cs
```

当前塔防建塔逻辑在：

- `Assets/Scripts/Game/Tower/TowerBuildManager.cs`
- `Assets/Scripts/Game/Tower/TowerBuildInputController.cs`

后续可以逐步抽出公共建造能力，塔防只保留塔相关规则。

#### Game/TowerDefense

塔防玩法专属逻辑。

```text
Game/TowerDefense
├── Battle
├── Wave
├── Tower
├── Enemy
├── Base
├── Targeting
└── Settlement
```

当前对应：

- `Game/Battle` -> `Game/TowerDefense/Battle`
- `Game/Wave` -> `Game/TowerDefense/Wave`
- `Game/Tower` -> `Game/TowerDefense/Tower`
- `Game/Base` -> `Game/TowerDefense/Base`
- `Game/Npc` 如果只表示塔防敌人，后续建议迁到 `Game/TowerDefense/Enemy`

命名建议：

- 如果是敌人，后续新代码用 `Enemy`，不要继续用 `Npc`。
- 如果是岛上居民，放到 `Game/Characters` 或 `Game/Island/Workers`。

#### Game/Characters

角色通用层，区分居民、敌人、可交互 NPC。

```text
Game/Characters
├── Actor.cs
├── ActorType.cs
├── GameEntity.cs
├── Residents
├── Enemies
└── Interaction
```

当前对应：

- `Assets/Scripts/Game/Entity`
- `Assets/Scripts/Game/Npc`

后续建议：

- `Entity` 作为底层抽象保留。
- 塔防敌人迁到 `TowerDefense/Enemy`。
- 岛上居民、剧情 NPC 放到 `Characters/Residents` 或 `Characters/Interaction`。

#### Game/Items

物品、掉落、库存。

```text
Game/Items
├── ItemData.cs
├── ItemManager.cs
├── Drop
└── Inventory
```

当前对应：

- `Game/Item`
- `Game/Drop`

建议：

- 文件夹名统一成复数 `Items`。
- `Drop` 可以作为 `Items/Drop`，除非掉落系统未来很复杂再独立。

#### Game/UI

游戏业务 UI，不包含通用 UI 框架。

```text
Game/UI
├── Common
├── Management
├── Build
├── TowerDefense
├── Story
├── Inventory
└── Toasts
```

当前对应：

- `Game/UI/TowerDefense`
- `Game/UI/Toasts`
- `StatusPanel.cs`
- `BuildTowerPanel.cs`

规则：

- `Framework/UI` 是通用 UI 框架。
- `Game/UI` 是具体游戏界面。
- 塔防界面继续放 `Game/UI/TowerDefense`。
- 经营界面后续放 `Game/UI/Management`。

#### Game/Input / Game/Camera / Game/Effects / Game/Save / Game/Story

```text
Game/Input
├── MapInputController.cs
└── BuildInputController.cs

Game/Camera
└── CameraManager.cs

Game/Effects
├── BattleEffect.cs
└── RuntimeEffects

Game/Save
├── StorageManager.cs
├── SaveData.cs
└── SaveVersion.cs

Game/Story
├── StoryManager.cs
├── QuestManager.cs
├── DialogManager.cs
└── EventTriggerSystem.cs
```

当前对应：

- `Game/CameraManager.cs` 后续可迁到 `Game/Camera`
- `Game/Effect` 后续可迁到 `Game/Effects`
- `Game/Input` 已存在
- `Save`、`Story` 是后续新增模块

### MapEditor

只放地图编辑器和编辑器辅助工具。

```text
Assets/Scripts/MapEditor
├── Editor
├── SceneTools
└── RuntimeBridge
```

当前对应：

- `MapEditor/Editor/MapEditorWindow.cs`
- `MapEditor/Editor/TileTopicUvChecker.cs`

建议：

- `MapEditor/Runtime` 中的纯数据类后续迁到 `Game/Map/Data` 或 `Game/Map/Config`。
- 只被编辑器使用的 Gizmo/检查工具保留在 `MapEditor`。
- 如果某个运行时组件只是为了编辑器辅助显示，例如 bounds gizmo，可以放 `MapEditor/SceneTools`，但要确认是否会挂在正式 prefab 上。

### Ability / Skill

技能系统保留独立模块。

```text
Assets/Scripts/Ability
├── Core
├── Scripting
└── Interfaces

Assets/Scripts/Skill
├── Core
├── Actions
├── Modifiers
├── Events
├── Targeting
└── Interfaces
```

当前已有两个系统：

- `Ability`：较新的能力系统。
- `Skill`：较旧的技能系统。

建议：

- 先不要强行合并。
- 新玩法优先接 `Ability`。
- `Skill` 标记为 legacy 或逐步迁移，等功能稳定后再统一。

### Tools

项目开发工具，不参与正式运行时。

```text
Assets/Scripts/Tools
└── Editor
    └── LubanTool.cs
```

当前对应：

- `Assets/Scripts/Editor/LubanTool.cs`

建议：

- 后续把 `Assets/Scripts/Editor` 改为 `Assets/Scripts/Tools/Editor`。

### Sandbox

样例、临时代码、实验脚本。

```text
Assets/Scripts/Sandbox
├── Samples
└── Experiments
```

当前适合迁入：

- `MapEditorTabGroupDemo.cs`
- `MapEditorToolbarDemo.cs`
- `MapEditorToolbarDemoInspector.cs`
- `Framework/UI/Samples`
- `Framework/Event/Samples`

规则：

- Sandbox 里的代码不作为正式游戏依赖。
- 如果脚本挂在场景或 prefab 上，迁移前先确认用途。

## 推荐迁移顺序

### 第 0 步：只定规则

- 新增本文档。
- 新代码按本文档放置。
- 不移动旧文件。

### 第 1 步：清理样例和临时代码

优先处理不会影响正式运行的内容：

- `MapEditorTabGroupDemo.cs`
- `MapEditorToolbarDemo.cs`
- `MapEditorToolbarDemoInspector.cs`
- `Framework/UI/Samples`
- `Framework/Event/Samples`

迁移方式：

- 在 Unity Project 面板中移动，或确保文件和 `.meta` 一起移动。
- 移动后运行一次编译。

### 第 2 步：整理编辑器工具

目标：

- `Assets/Scripts/Tools/Editor/LubanTool.cs`
- `Assets/Scripts/MapEditor/Editor/*`

注意：

- 仍然必须在 `Editor` 文件夹下。
- 编辑器窗口类一般不会挂到 prefab 上，移动风险较低。

### 第 3 步：拆出共享地图数据

目标：

- `MapEditor/Runtime/MapData.cs` 等纯数据类迁到 `Game/Map/Data`
- `MapTilePrefabConfig.cs` 等资源配置迁到 `Game/Map/Config`

注意：

- 这些 ScriptableObject 可能被 asset 引用，必须保留 `.meta` GUID。
- 不改类名和 namespace，先只移动文件夹。

### 第 4 步：整理塔防运行时

目标：

- `Game/Battle` -> `Game/TowerDefense/Battle`
- `Game/Wave` -> `Game/TowerDefense/Wave`
- `Game/Tower` -> `Game/TowerDefense/Tower`
- `Game/Base` -> `Game/TowerDefense/Base`

注意：

- 很多 MonoBehaviour 可能挂在 UI prefab、塔 prefab、场景对象上。
- 必须逐批移动并验证 prefab 引用没有 Missing Script。

### 第 5 步：新增经营模块

新增：

- `Game/Island`
- `Game/Build`
- `Game/Save`
- `Game/Story`
- `Game/UI/Management`

优先新增，不急着重构旧代码。

## Unity 引用安全规则

移动脚本时必须遵守：

1. `.cs` 和 `.cs.meta` 必须一起移动。
2. 不要删除再新建同名脚本，否则 GUID 会变，Prefab/Scene 会 Missing Script。
3. 不要随便改 `MonoBehaviour` 或 `ScriptableObject` 类名。
4. 不要随便改 namespace 后忘记引用；Unity 的序列化主要看 GUID，但 C# 编译会受 namespace 影响。
5. 序列化字段改名要使用 `[FormerlySerializedAs("oldName")]`。
6. 移动后检查 Console 是否有 Missing Script、编译错误、资源加载路径错误。
7. 如果有字符串路径加载脚本相关资源，移动资源时要同步路径；移动脚本通常不影响 YooAsset 路径。

建议每批迁移后做：

```text
1. Unity 编译通过
2. 打开主场景
3. 打开 Map Editor
4. 运行一次加载地图
5. 检查关键 prefab 是否 Missing Script
6. dotnet build Cube.sln
```

## HybridCLR 预留边界

如果后续接 HybridCLR，建议：

```text
AOT Host:
- Bootstrap
- Framework
- ResourceManager / YooAsset
- UI 基础框架
- Data 加载基础
- Hotfix 接口

Hotfix:
- Game/Island
- Game/TowerDefense
- Game/Build
- Game/Story
- 大部分 Game/UI 业务逻辑
```

不要把 Unity 强引用资源和热更逻辑绑得太死。运行时资源通过 YooAsset 加载，热更 DLL 只处理逻辑和配置适配。

## 命名规则

- 文件夹使用清晰英文名，不混用缩写。
- 业务模块用名词：`TowerDefense`、`Island`、`Build`、`Story`。
- Manager 只用于模块入口，不要每个类都叫 Manager。
- 数据类后缀：`Data`、`Config`、`State`、`RuntimeData`。
- 视图类后缀：`View`。
- UI 控制类后缀：`Panel`、`Page`、`Popup`、`Controller`。
- 编辑器工具后缀：`Editor`、`Window`、`Generator`、`Checker`。

## 当前不建议立即做的事

- 不建议现在大规模改 namespace。
- 不建议现在把 `Ability` 和 `Skill` 合并。
- 不建议现在引入大量 asmdef。
- 不建议现在移动 `Assets/Arts`。
- 不建议直接重命名已挂载在 prefab 上的 MonoBehaviour 类。
