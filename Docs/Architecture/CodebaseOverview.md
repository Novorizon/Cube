# Codebase Overview

本文记录项目代码分层和主要目录。新对话如果只改某个业务模块，不需要读完整文档树；从 `Docs/README.md` 的任务入口跳到对应模块即可。

## 顶层结构

```text
Assets/Scripts
  Bootstrap
  Framework
  Game
  MapEditor
  Ability
  Skill
  Tools
  Sandbox
```

## Bootstrap

```text
Assets/Scripts/Bootstrap/GameEntry.cs
```

职责是初始化资源、数据、管理器和主流程。不要让 `GameEntry` 继续膨胀；复杂流程应下沉到明确的 manager。

## Framework

跨玩法基础设施放在 `Assets/Scripts/Framework`：

```text
Audio
Device
Effect
Event
InputSystem
Logging
Message
Resource
Singleton
Task        C# async helper，不是游戏任务系统
Threading
UI
Logger.cs
NetLogger.cs
```

Framework 不写具体经营、任务、塔防规则。`Audio/AudioManager` 是背景音乐和普通音效的统一入口，
用法见 `Docs/Architecture/AudioSystem.md`。UI 框架细节见 `Docs/Architecture/UIFramework.md`。

`Device/DeviceManager` 是设备与屏幕事实的统一来源：平台、设备类别、系统与显卡概况，
以及当前游戏窗口尺寸、安全区、方向和全屏状态。它不包含画质档位、UI 布局档位或玩法
规则，也不读取或保存设备唯一标识。

## Game

正式游戏运行时代码放在 `Assets/Scripts/Game`：

```text
AbilityAdapters
Animator
Base
Battle
Blueprints
Build
Camera
Characters
Core
Data
Debug
Drop
Effect
Effects
Entity
Input
Island
Item
Items
Localization
Map
Message
Npc
PathFinding
Quests
Save
Story
Tower
TowerDefense
UI
Wave
```

业务文档按主题拆分：

```text
Docs/Modules/Island.md
Docs/Modules/ManagementUI.md
Docs/Modules/QuestStoryBlueprint.md
Docs/Modules/AbilityAndSkill.md
Docs/Modules/TowerDefense.md
Docs/Modules/MapAndTileArt.md
```

## Game/Data

```text
Assets/Scripts/Game/Data
Assets/Scripts/Game/Data/Generated
```

`Generated` 由 Luban 生成，不手动修改。数据流水线见 `Docs/Architecture/DataPipeline.md`。

## Game/UI

业务 UI 放在 `Assets/Scripts/Game/UI`，通用 UI 能力放在 `Assets/Scripts/Framework/UI`。

当前经营 UI 主要在：

```text
Assets/Scripts/Game/UI/Management
Assets/Arts/UI/Panels
Assets/Arts/UI/Icons
```

## MapEditor / Tools / Sandbox

`MapEditor` 只放地图编辑器窗口和编辑器工具。运行时地图逻辑放 `Game/Map`。

`Tools` 放编辑器工具、一次性工具和 prefab 绑定工具。如果只在编辑器用，必须放在 `Editor` 子目录。

`Sandbox` 只用于实验和样例，正式玩法不要依赖 Sandbox。

游戏任务系统使用 `Quests`。不要重新创建 `Tasks` 作为业务任务目录，避免和 C# async `Task` 混淆。

## Unity 安全规则

- 不为了目录整洁随意移动已经挂在 Prefab 或 Scene 上的脚本。
- 移动 `.cs` 必须同时移动 `.meta`，保持 Unity GUID。
- 不删除再新建同名脚本，否则 Prefab 可能 Missing Script。
- 改序列化字段名要考虑 `[FormerlySerializedAs]`。
- Runtime 代码不要放进 `Editor` 目录。
- Luban 生成代码和生成数据不手动改。








