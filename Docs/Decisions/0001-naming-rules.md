# 0001 Naming Rules

## 结论

新代码默认不要加 `World` 或 `Game` 前缀。

只有在确实需要和塔防单局、框架通用能力、存档数据等概念区分时，才使用更明确的业务前缀。

## 旧名字

已有类名短期保留，不因为命名规则单独重命名：

```text
WorldMainPanel
WorldBagPanel
WorldBuildPanel
WorldFarmPanel
WorldProductionPanel
WorldToolKitPanel
WorldTopBarPanel
WorldBottomBarPanel
WorldEntryBarPanel
WorldRightBarPanel
WorldBuildingDetailPanel
WorldBuildingManager
WorldFloatingPanelLayout
```

这些类可能被 Prefab、存档或其它引用绑定。不要为了命名洁癖移动、重命名或删 `.meta`。

原 `WorldGameplayController` 已在职责拆分时迁移为无前缀结构：

```text
GameplayController
NavigationController
ActionController
ResourceInteractionController
PlacementController
CameraController
BuildingPreview
FarmAreaPreview
```

物品管理已完成职责迁移：

```text
原塔防 ItemManager      -> BattleItemManager
原 WorldItemManager     -> ItemManager
原 WorldItem            -> ItemStack
```

`ItemManager` 表示跨场景持久物品；`BattleItemManager` 只保存单场塔防资源。不要新增 `ItemUseManager`，物品使用入口由 `ItemManager.Use` 统一提供。

`GameplayController` 只负责 Unity 生命周期、输入优先级和公开接口转发；不要再把寻路、动作时序、资源结算或预览对象状态写回入口类。

## Luban / Excel

新增 Luban 表、Excel 文件、生成配置类和表访问名也默认不要加 `World` 或 `Game` 前缀。

`resource`、`gather`、`reward` 已经从旧 `world_*` 表名迁移为无前缀命名：

```text
resource.xlsx
gather.xlsx
reward.xlsx
ResourceConfig
GatherConfig
RewardConfig
TbResource
TbGather
TbReward
```

仍保留的 `world_*` Excel 和 `World*Config` 表是历史命名，短期不要因为命名规则单独迁移：

```text
world_building.xlsx
WorldBuildingConfig
```

新增工具配置不要照着旧历史表名写成：

```text
world_tool.xlsx
WorldToolConfig
TbWorldTool
```

应使用：

```text
tool.xlsx
ToolConfig
TbTool
```

如果后续要覆盖刀剑、弓、盾、护甲等完整装备，不要命名为 `WorldEquipment`，优先使用：

```text
equipment.xlsx
EquipmentConfig
TbEquipment
```

## Quest

任务域使用 `Quest`，不要改成 `Task`，也不要加 `World`：

```text
QuestManager
QuestConfig
QuestData
QuestState
QuestEvent
QuestCompletedMessage
QuestToastListener
```

避免：

```text
WorldQuestManager
GameQuestManager
TaskManager
WorldTaskManager
```

## Blueprint

制作和生产配置使用 `Blueprint`：

```text
BlueprintManager
BlueprintConfig
BlueprintItem
QuestObjectiveType.Blueprint
```

不要新增 `Recipe` 或 `WorldRecipe` 作为同义概念。








