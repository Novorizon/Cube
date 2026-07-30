# Module Work Map

本文是“更高一级”的工作索引，用来回答：改某个模块时，需要读哪些文档、看哪些代码、改哪些表、用哪些工具、检查哪些生成物。

具体设计细节仍放在各模块文档里；本文只做总览和入口。

## 使用方式

1. 先在本文找到模块。
2. 按“先读文档”进入细节文档。
3. 修改表时按“配置表”和“生成工具”执行。
4. 修改代码或 prefab 前按“相关文件”确认边界。
5. 如果本文和代码冲突，以代码和实际 prefab 为准，然后更新本文。

## 通用数据工具

配置生成：

```text
Data/gen_all.bat
```

Unity 菜单：

```text
Luban/Update All
```

对应工具代码：

```text
Assets/Scripts/Tools/Editor/LubanTool.cs
```

规则：

- 只改表内容时，通常跑 `Data/gen_all.bat`。
- 新增字段或改字段结构时，可用 `Luban/Update All`，它会根据 Excel 前三行更新 `Data/Defines/*.xml`，再执行 `gen_all.bat`。
- 不要单独建议执行 `Data/gen_client.bat`，除非明确只调试 client 生成脚本本身。

通用生成物：

```text
Assets/Scripts/Game/Data/Generated
Assets/Data/Bin
Assets/Data/Json
```

## 模块总表

| 模块 | 先读文档 | 代码入口 | 配置表 | 工具 / 生成 | 备注 |
| --- | --- | --- | --- | --- | --- |
| Story | `Modules/Story.md`、`Product/ProgressionAndQuests.md` | `Assets/Scripts/Game/Story` | `story.xlsx`、`story_step.xlsx` | `gen_all.bat`、`Luban/Update All`、`StoryPanelPrefabBuilder` | StoryStep 同时承载文字、插画与轻量 Guide；当前没有专门 Story 配表校验器 |
| Quest | `Modules/QuestStoryBlueprint.md`、`Product/ProgressionAndQuests.md` | `Assets/Scripts/Game/Quests`、`Assets/Scripts/Game/UI/Management/QuestPanel.cs` | `quest.xlsx`、`quest_objective.xlsx`、`reward.xlsx` | `gen_all.bat`、`Luban/Update All` | 奖励发放已走 BagManager；Toast / 获得反馈继续打磨 |
| Blueprint | `Modules/QuestStoryBlueprint.md`、`Modules/Island.md` | `Assets/Scripts/Game/Blueprints` | `blueprint.xlsx`、`blueprint_item.xlsx` | `gen_all.bat`、`Luban/Update All` | 不要新增 `Recipe` 同义概念 |
| Management UI | `Architecture/UIFramework.md`、`Modules/ManagementUI.md` | `Assets/Scripts/Game/UI/Management` | 通常不直接改表；文本走 `localization.xlsx` | Prefab + Inspector 绑定；必要时用 `WorldSystemMenuPrefabBuilder` / `WorldLocalizationPrefabBinder` | 不要把稳定 HUD 节点改回运行时 `Find` |
| UI Framework | `Architecture/UIFramework.md`、`Decisions/0002-ui-kind-usage.md` | `Assets/Scripts/Framework/UI`、`Assets/Scripts/Game/Core/RequirementResult.cs`、`Assets/Scripts/Game/UI/Toasts` | 通用 UI 无；业务提示文本走 `localization.xlsx` | 代码验证为主；本地化改动跑 `gen_all.bat` | Stack / Exclusive / outside close 属框架能力；业务条件统一返回 RequirementResult |
| Island Resources / Tools | `Modules/Island.md` | `Assets/Scripts/Game/Island/Resources`、`Assets/Scripts/Game/Island/Tools` | `item.xlsx`、`resource.xlsx`、`gather.xlsx` | `gen_all.bat` | ToolKit UI 另读 `Modules/ManagementUI.md` |
| Farming | `Modules/Island.md`、`Product/ManagementMode.md` | `Assets/Scripts/Game/Island/Farming`、`PlacementController.cs`、`WorldFarmPanel.cs` | `world_crop.xlsx`；锄头获取链路还涉及 `item.xlsx`、`blueprint.xlsx`、Quest / Reward 表 | `gen_all.bat` | QuickFarm 已检查 Active House、锄头拥有与 ToolKit；各失败阶段走 RequirementResult + Toast；锄头正常获取链路仍待补齐 |
| Buildings / Production | `Modules/Island.md`、`Product/ManagementMode.md` | `Assets/Scripts/Game/Island/Buildings`、`WorldBuildingDetailPanel.cs` | `world_building.xlsx`、`world_building_level.xlsx`、`world_building_income.xlsx`、`world_cost.xlsx`、`reward.xlsx` | `gen_all.bat`、`WorldConfigValidator` | `WorldConfigValidator` 主要校验建筑 / 科技关系 |
| Tech | `Modules/Island.md`、`Modules/ManagementUI.md` | `Assets/Scripts/Game/Island/Tech`、`TechTreePanel.cs` | `tech_node.xlsx` | `gen_all.bat`、`WorldConfigValidator` | TechUnlock 后续更适合 Popup |
| Calendar / Save | `Architecture/SaveSystem.md`、`Modules/Island.md` | `CalendarManager.cs`、`StorageManager.cs`、`SaveData.cs` | 无主要表 | `WorldSaveDevTool` 可辅助存档测试 | 存档字段变更要注意兼容 |
| Map Runtime | `Architecture/MapRuntime.md`、`Decisions/0003-map-object-vs-marker.md` | `Assets/Scripts/Game/Map` | `map.xlsx` 及 `Assets/Data/Map/*.json` | MapEditor 工具、`SceneMapDataExporter` | `MapData.Objects` 不是 UI 标记表 |
| Tile Art | `Modules/MapAndTileArt.md` | `Tools/Art`、`Assets/Arts/Map` | 通常无 Luban 表 | Blender / PowerShell 美术脚本 | 第一阶段不做复杂邻接融合 |
| Tower Defense | `Product/TowerDefenseMode.md`、`Modules/TowerDefense.md` | `Assets/Scripts/Game/TowerDefense` | `tower.xlsx`、`tower_level.xlsx`、`wave.xlsx` 等 | `gen_all.bat`、`gen_wave_all_no_overwrite.bat` | 单局状态不要混入经营长期状态 |
| Ability / Skill | `Modules/AbilityAndSkill.md` | `Assets/Scripts/Ability`、`Assets/Scripts/Skill`、`Assets/Scripts/Game/AbilityAdapters` | `Ability*.xlsx`、`skill*.xlsx` | `gen_all.bat` | 新塔防技能优先走 Ability |
| Localization | `Architecture/DataPipeline.md` | `Assets/Scripts/Game/Localization` | `localization.xlsx` | `gen_all.bat` | 新 UI 文本不要写代码 fallback |

## 工具索引

数据生成：

```text
Data/gen_all.bat
Data/gen_client.bat
Data/gen_wave_all_no_overwrite.bat
Assets/Scripts/Tools/Editor/LubanTool.cs
```

配置校验：

```text
Assets/Scripts/Game/Data/Editor/WorldConfigValidator.cs
```

Prefab / UI 辅助：

```text
Assets/Scripts/Tools/Editor/WorldSystemMenuPrefabBuilder.cs
Assets/Scripts/Tools/Editor/WorldLocalizationPrefabBinder.cs
```

地图编辑：

```text
Assets/Scripts/MapEditor/Editor/MapEditorWindow.cs
Assets/Scripts/MapEditor/Editor/WorldMapEditorWindow.cs
Assets/Scripts/MapEditor/Editor/TowerDefenseMapEditorWindow.cs
Assets/Scripts/MapEditor/Editor/SceneMapDataExporter.cs
Assets/Scripts/MapEditor/Editor/MapJsonService.cs
Assets/Scripts/MapEditor/Editor/MapEditorValidator.cs
```

存档辅助：

```text
Assets/Scripts/Game/Save/Editor/WorldSaveDevTool.cs
```

美术生成：

```text
Tools/Art
Tools/Art/Blender
Tools/Art/Texture
```

## 每个模块文档应包含

后续新增或整理模块文档时，尽量包含这些段落：

```text
定位
当前文件
Prefab / UI
配置表
如何配表
生成工具
运行时链路
存档 / 消息
当前缺口
```

不是每个模块都必须有每一项。例如 UI Framework 没有配置表，Tile Art 不一定有 Luban 表，但文档里要明确“没有”或“当前不覆盖”，避免下次误找。




