# Data Pipeline

本文记录配置数据的当前来源和生成规则。

## 正式流水线

```text
Data/Excel/*.xlsx
  -> Luban
  -> Assets/Scripts/Game/Data/Generated
  -> Assets/Data/Bin/*.bytes
  -> Assets/Data/Json/*.json
```

运行时以 `Assets/Data/Bin/*.bytes` 为准。`Assets/Data/Json/*.json` 主要用于查看和调试，不作为长期正式运行来源。

## 生成命令

重新生成配置时统一执行：

```text
Data/gen_all.bat
```

不要单独建议执行 `Data/gen_client.bat`。`gen_all.bat` 会串起 client 数据和 Wave 数据生成。

Unity 菜单：

```text
Luban/Update All
```

`Assets/Scripts/Tools/Editor/LubanTool.cs` 会从 Unity project root 推导 `Data` 路径，不应硬编码本机盘符。

生成后重点检查：

```text
Assets/Scripts/Game/Data/Generated
Assets/Data/Bin
Assets/Data/Json
Assets/Data/Bin/Wave
Assets/Data/Json/Wave
```

## 当前重要表

`Tables` 当前包含 Ability、Skill、Item、Localization、Map、Npc、Tower、Wave、TechNode、WorldBuilding、WorldCost、WorldCrop、Gather、Resource、Reward、Quest、QuestObjective、Blueprint、BlueprintItem、Story、StoryLine 等表。

每个 Excel 的用途、全部字段、关联关系和当前代码接入状态见：

```text
Docs/Architecture/ExcelDataDictionary.md
```

注意：`tech_node.xml` 当前没有被 `Data/Defines/__root__.xml` include。虽然仓库里仍有生成代码和运行时读取代码，但再次完整生成可能使 TechNode 产物消失；应先修复根配置。

命名规则：

- 新增 Luban 表、Excel 文件、生成配置类和表访问名默认不要加 `World` 或 `Game` 前缀。
- `resource.xlsx`、`gather.xlsx`、`reward.xlsx` 已经从旧 `world_*` 表名迁移为无前缀命名。
- 仍保留的 `world_*` Excel 和 `World*Config` 表是历史命名，短期不要因为命名规则单独迁移。
- 新增工具配置应使用 `tool.xlsx` / `ToolConfig` / `TbTool`，不是 `world_tool.xlsx` / `WorldToolConfig` / `TbWorldTool`。

Quest / Blueprint / Story 已接入正式 Luban 表：

```text
Data/Excel/quest.xlsx
Data/Excel/quest_objective.xlsx
Data/Excel/blueprint.xlsx
Data/Excel/blueprint_item.xlsx
Data/Excel/story.xlsx
Data/Excel/story_line.xlsx
```

## 本地化

本地化正式来源：

```text
Data/Excel/localization.xlsx
Assets/Data/Bin/tblocalization.bytes
Assets/Data/Json/tblocalization.json
```

规则：

- 新 UI 文本先写入 `localization.xlsx`。
- 代码使用 `LocalizationManager.Get(key)` 或格式化 helper。
- 不为新 UI 文本在代码里写临时中英文 fallback。
- 运行时以 `tblocalization.bytes` 为准。

当前 Quest UI 已补的 key：

```text
ui.quest.main_title
ui.quest.current_goals
ui.quest.none
ui.quest.tracker_title
ui.quest.track
ui.quest.tracking
ui.quest.incomplete
ui.quest.not_started
ui.quest.claim
ui.quest.claimed
```

## 生成文件规则

- 不手动改 `Assets/Scripts/Game/Data/Generated`。
- 不手动改由 Luban 生成的 `.bytes` 作为长期方案。
- 表结构变更要同步 `Data/Defines/*.xml`、`Data/Excel/*.xlsx` 和生成结果。








