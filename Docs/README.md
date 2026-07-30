# Cube Docs

本目录是项目文档入口。新对话不要默认阅读所有文档；先按任务选入口，只读必要模块。

## Codex 快速入口

先读：

```text
Docs/CodexProjectMemory.md
```

然后按任务继续：

| 任务 | 继续阅读 |
| --- | --- |
| UI 框架、关闭规则、Stack、Exclusive | `Architecture/UIFramework.md` |
| 背景音乐、普通音效、音量设置 | `Architecture/AudioSystem.md` |
| 不确定模块该看哪些表、工具、文件 | `Architecture/ModuleWorkMap.md` |
| 经营主界面、Bag、Build、ToolKit、Quest、TechTree | `Modules/ManagementUI.md` |
| 岛屿经营业务、资源、工具、农田、建筑 | `Product/ManagementMode.md`、`Modules/Island.md` |
| Story 剧情播放和触发 | `Modules/Story.md`、`Product/ProgressionAndQuests.md` |
| Quest / Blueprint | `Modules/QuestStoryBlueprint.md`、`Product/ProgressionAndQuests.md` |
| 数据表、Luban、本地化 | `Architecture/DataPipeline.md`、`Architecture/ExcelDataDictionary.md` |
| 项目级代码参数、时间比例、表现微调 | `Architecture/GameConfig.md` |
| 地图运行时、小地图标记 | `Architecture/MapRuntime.md`、`Decisions/0003-map-object-vs-marker.md` |
| 地块美术 | `Modules/MapAndTileArt.md` |
| 塔防和技能 | `Product/TowerDefenseMode.md`、`Modules/TowerDefense.md`、`Modules/AbilityAndSkill.md` |
| 命名、移动脚本、Prefab 安全 | `Decisions/0001-naming-rules.md`、`Architecture/CodebaseOverview.md` |
| 项目中期审核、风险清单 | `Audits/ProjectMidpointAudit-2026-07-09.md` |
| 项目模块盘点、开发文档排期、飞书表格导入 | `Planning/项目开发模块与文档排期_飞书粘贴版.tsv` |

## 文档结构

```text
Docs
  Architecture   底层框架、数据、地图、存档、消息
  Product        游戏整体和产品流程
  Modules        具体业务模块
  Decisions      需要反复引用的决策
  Audits         阶段性审核、风险清单、整理记录
  Planning       模块清单、排期和可导入协作表格
  Human          人类阅读入口 HTML
```

`Docs/Human/index.html` 是人类阅读入口，Markdown 仍是源文档。HTML 页面由 `Docs/Human/build-human-docs.ps1` 读取 Markdown 后生成，不要把长期内容只改在 HTML 里。

刷新人类阅读页面：

```text
powershell.exe -ExecutionPolicy Bypass -File Docs/Human/build-human-docs.ps1
```

## 当前开发重点

当前重点是岛屿经营主循环：

```text
Story -> Quest -> 采集 -> Blueprint -> 建造 / 农田 / 科技 -> 更多内容
```

塔防模式保留为独立战斗模式，结算后通过明确奖励流程回写经营长期数据。

## 全局硬规则

- 新代码默认不要加 `World` 或 `Game` 前缀；已有 `World*` 类名短期保留。
- 新增 Luban / Excel 表同样不要加 `World` 或 `Game` 前缀；已有 `world_*` 表是历史命名，短期保留，不作为新表命名模板。
- 任务域使用 `Quest`，不要改成 `Task`。
- 制作 / 生产配置使用 `Blueprint`，不要新增 `Recipe` 或 `WorldRecipe`。
- 重新生成配置统一执行 `Data/gen_all.bat`，不要单独建议执行 `Data/gen_client.bat`。
- UI 节点优先通过 prefab 序列化字段绑定，不运行时按名字动态 `Find` 常驻节点。
- 方法声明和方法调用的参数列表尽量保持在同一行；仅在单行明显过长或参数表达式复杂时换行，不要机械地让每个参数单独占一行。
- 移动 Unity 脚本必须保留 `.meta`，不要为了整理随意重命名已挂 Prefab 的脚本。
- 重要框架规则、业务流程、命名约定、易踩坑细节或长期设计决策发生变化时，必须同步更新对应 Markdown 源文档；改完后重新生成 `Docs/Human/index.html`。不要只把长期规则写在聊天记录或 HTML 里。









