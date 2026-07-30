# Project Midpoint Audit - 2026-07-09

本文记录项目中期审核结果。目标是帮助后续开发知道当前哪些链路已经闭合，哪些只是能编译但产品规则还没完成，哪些文档或旧代码会误导新对话。

## 审核范围

本次检查覆盖：

```text
Docs
Assets/Scripts/Framework/UI
Assets/Scripts/Game/UI/Management
Assets/Scripts/Game/Story
Assets/Scripts/Game/Quests
Assets/Scripts/Game/Blueprints
Assets/Scripts/Game/Island
Assets/Scripts/Tools/Editor
Assets/Scripts/Game/Data/Editor
Data/Excel
Data/Defines
Assets/Data/Bin
Assets/Data/Json
```

验证命令：

```text
dotnet build Cube.sln
```

结果：构建通过，0 个错误，5 个警告。

## 总体结论

项目当前处于“核心方向已经成形，但若干玩法闭环还没完成”的阶段。

已经比较稳定的部分：

- 文档入口已经按 Codex / 人类阅读拆分。
- Quest / Blueprint / Story 已接入正式 Luban 表。
- UI 框架的 outside close、Exclusive group、Stack group 当前代码和文档基本一致。
- WorldMainPanel 的 Top / Bottom / Entry / Right / BuildingDetail 子模块已改为 prefab 上的 MonoBehaviour 引用，不再由 `WorldMainPanel` 手动 `new`。
- Bag / Build / ToolKit / Production / Quest / TechTree / Menu 已进入同一个经营浮动互斥组。

需要优先补齐的部分：

- Story 中途存档恢复逻辑不完整。
- Quest / Story / Blueprint 缺少配置校验工具。
- 部分 UI 面板仍靠运行时 `Transform.Find` 绑定稳定节点，后续 UI 打磨时应继续改成序列化字段。

## Findings

### P1

当前没有发现阻塞构建或明显会让主流程无法启动的 P1 问题。

### Fixed - Quest 领取奖励

原问题：

```text
Assets/Scripts/Game/Quests/QuestManager.cs:76     QuestConfig.RewardGroupId
TryClaim 只把任务改成 Claimed，没有发放 RewardGroupId
Quest UI 奖励数量固定显示 "x1"
```

处理结果：

- `QuestManager.TryClaim` 使用 `RewardResolver` 解析 `RewardGroupId`。
- 有奖励组时走 `BagManager.TryAddItems`，背包容量不足不会改为 `Claimed`。
- 奖励组为空时领取失败，避免误把任务标记为已领取。
- Quest UI 详情奖励行读取 `reward`，显示固定数量或 `min-max` 范围。

剩余：

- 任务奖励 Toast / 获得反馈还需要产品表现设计。

### P2 - Story 中途读档会恢复 currentStoryId，但不会恢复面板和行号

证据：

```text
Assets/Scripts/Game/Story/StoryManager.cs:49   currentStoryId = data.CurrentStoryId
Assets/Scripts/Game/Story/StoryManager.cs:252  CanStart 要求 currentStoryId == 0
Assets/Scripts/Game/Story/StoryPanel.cs:114    当前 UI 运行时创建，没有存行号
```

影响：

- 如果保存时 `currentStoryId != 0`，读档后不会自动重新打开 StoryPanel，也没有当前行索引。
- 同时 `currentStoryId != 0` 会阻止其它 Story 开始。

建议：

- 短期方案：如果不支持剧情中途保存，就不要把未完成 Story 写入存档，读档时置 0。
- 完整方案：保存 `CurrentStepIndex`，读档后通过 `StoryPresenter` 恢复 UI。

### P2 - Quest / Story / Blueprint 缺少配置校验

证据：

```text
Assets/Scripts/Game/Data/Editor/WorldConfigValidator.cs  当前主要校验世界建筑 / 科技关系
Docs/Architecture/ModuleWorkMap.md                     Story 备注为没有专门编辑器或校验器
Docs/Modules/QuestStoryBlueprint.md                    奖励和配置规范仍需完善
```

影响：

- 当前表已经数据驱动，但跨表引用错误更容易进入运行时才发现。
- 比如 Quest Objective 指向不存在的 Blueprint、Story 触发事件 target 不存在、RewardGroupId 没有奖励项。

建议：

- 增加轻量 `ProgressionConfigValidator` 或扩展现有 validator。
- 至少校验：Quest 前置、Objective target、RewardGroupId、Blueprint inputs / outputs、Story nextStoryId、Story 完成事件。

### Fixed - Luban 编辑器工具硬编码本机路径

原证据：

```text
LubanTool 使用本机 D 盘绝对路径定位 Data / Excel / Defines / gen_all.bat
```

原影响：

- 当前机器可用，但换目录、换电脑或 CI 会失败。
- 文档建议使用 `Luban/Update All`，工具本身应该能从 Unity project root 推导路径。

处理结果：

- `LubanTool` 已从 `Application.dataPath` 推导 project root。
- `Data`、`Excel`、`Defines`、`gen_all.bat` 都按项目相对路径组合。

### P3 - 管理 UI 面板仍有运行时 Transform.Find

证据示例：

```text
Assets/Scripts/Game/UI/Management/QuestPanel.cs:50
Assets/Scripts/Game/UI/Management/QuestPanel.cs:64
Assets/Scripts/Game/UI/Management/WorldBuildPanel.cs:71
Assets/Scripts/Game/UI/Management/WorldToolKitPanel.cs:71
Assets/Scripts/Game/UI/Management/WorldProductionPanel.cs:68
Assets/Scripts/Game/UI/Management/WorldBagPanel.cs:57
```

影响：

- 当前不一定是 bug，但和“稳定 UI 节点用 prefab 序列化字段绑定”的方向不一致。
- Prefab 节点改名会在运行时才失败。

建议：

- 区分两类节点：
  - 稳定按钮、标题、根节点：改成 `[SerializeField]`。
  - 动态生成列表、slot 内部兼容旧 prefab 的临时适配：可以先保留。
- 后续改某个 panel UI 时顺手收敛，不必一次性大改全部。

### P3 - Menu 子页面当前能走 Stack，但长期结构要再决定

当前代码：

```text
Menu -> Sound
Menu -> Language
Menu -> Save
Menu -> GM
```

实现方式：

```text
WorldMenuPanel.PushStackAsync(SettingsStackGroupId, MenuPanel)
WorldMenuPanel.PushStackAsync(SettingsStackGroupId, childPanel)
Sound / Language / Save / GM close 时 PopStack
```

判断：

- 作为短期方案可运行，符合当前 UI 框架的 panel-level Stack 语义。
- 如果 Sound / Language / Save 只是 Menu 的内部设置页，长期更适合做成 Menu prefab 内序列化子节点，由 Menu 脚本维护本地 node stack。
- GM 如果仍是调试大面板，可以继续保留独立 Panel。

### P3 - StoryPanel 仍是运行时构建 UI

证据：

```text
Assets/Scripts/Game/Story/StoryPanel.cs:114  BuildIfNeeded
Assets/Scripts/Game/Story/StoryPanel.cs:145  CreateText("Title")
Assets/Scripts/Game/Story/StoryPanel.cs:153  CreateText("Body")
Assets/Scripts/Game/Story/StoryPanel.cs:168  CreateButton("ContinueButton")
```

判断：

- 当前功能可用，文档已经说明不要假设 prefab 内有固定节点。
- 后续如果要做正式剧情 UI，应迁移为 prefab 节点 + 序列化字段。

### P3 - 构建警告

`dotnet build Cube.sln` 通过，但有 5 个警告：

```text
Assets/Scripts/Framework/UI/Samples/Example/Scripts/SettingsPopup.cs:8  nullable annotation context
Assets/Scripts/Framework/UI/Samples/Example/Scripts/SidePanel.cs:8      nullable annotation context
Assets/Scripts/Game/TowerDefense/Tower/TowerBuildInputController.cs:13  hides MonoSingleton.Initialize()
Assets/Scripts/Game/TowerDefense/Enemy/NpcManager.cs:264               call is not awaited
Assets/Scripts/Framework/Event/Samples/EventView.cs:20                 assigned but unused
```

建议：

- Samples 警告低优先。
- `TowerBuildInputController.Initialize()` 如果是有意隐藏，显式加 `new`；否则改名或调整继承。
- `NpcManager.cs:264` 需要确认是否故意 fire-and-forget，若是则用 `.Forget()` 或显式注释。

## 本次已整理

删除未引用旧代码：

```text
Assets/Scripts/Game/UI/Management/WorldQuestPanel.cs
Assets/Scripts/Game/UI/Management/WorldQuestPanel.cs.meta
Assets/Scripts/Game/Story/StoryData.cs
Assets/Scripts/Game/Story/StoryData.cs.meta
Assets/Scripts/Game/Tasks
Assets/Scripts/Game/Tasks.meta
```

理由：

- `WorldQuestPanel` 没有调用方，也没有 prefab 引用；当前主任务面板是 `QuestPanel`。
- 旧 `Game/Story/StoryData.cs` 没有调用方；实际存档模型是 `StorageManager.StoryData`，通过 `SaveData.Story` 使用。
- `Assets/Scripts/Game/Tasks` 是空旧目录，和当前 `Quests` 业务目录冲突。

更新文档：

```text
Docs/Modules/Story.md
Docs/Architecture/CodebaseOverview.md
Docs/README.md
Docs/CodexProjectMemory.md
Docs/Human/build-human-docs.ps1
```

本轮继续修正：

```text
Assets/Scripts/Game/Save/SaveData.cs
Assets/Scripts/Game/Story/StoryManager.cs
Assets/Scripts/Game/Quests/QuestManager.cs
Assets/Scripts/Game/UI/Management/QuestSlotView.cs
Assets/Scripts/Tools/Editor/LubanTool.cs
Docs/Modules/QuestStoryBlueprint.md
Docs/Product/ProgressionAndQuests.md
Docs/Architecture/DataPipeline.md
Docs/Architecture/SaveSystem.md
```

## 文档一致性检查

已经对齐：

- 新代码默认不加 `World` / `Game` 前缀，旧 `World*` 可保留。
- 游戏任务系统使用 `Quest`，不是 `Task`。
- 制作 / 生产配置使用 `Blueprint`，不是 `Recipe`。
- Quest / Blueprint / Story 的 Excel、Defines、Bin、Json、Generated 代码均存在。
- UI Framework 文档和当前 `PanelManager` 的 Stack / Exclusive 实现基本一致。

仍需注意：

- `Framework/Task` 是 C# async helper，不是业务任务系统。
- 不要重新创建 `Assets/Scripts/Game/Tasks` 作为游戏任务系统入口。
- 新 UI 文本规则要求进入 `localization.xlsx`，但当前 Quest / Story 内容仍有部分配置文本 fallback，这是阶段性过渡。
- 当前 shell 是 Windows PowerShell 5.1；复杂 `rg` 正则优先拆开或用单引号，避免双引号内反斜杠和管道被 PowerShell 预解析。

## 建议的下一轮顺序

1. 先规划 Story 中途保存策略：不保存未完成 Story，或完整恢复当前行。
2. 增加 Progression 配置校验，覆盖 Quest / Story / Blueprint / Reward。
3. 补任务奖励 Toast / 获得反馈。
4. 后续每次改某个管理 UI panel 时，把该 panel 的稳定节点从 `Transform.Find` 收敛到序列化字段。



