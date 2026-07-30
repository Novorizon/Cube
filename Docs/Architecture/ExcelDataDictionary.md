# Excel Data Dictionary

本文是 `Data/Excel` 的人类数据字典，说明每个 Excel 的用途、每个字段的含义、表间关系和当前代码接入状态。

## 先看结论

- 正式配置源是 `Data/Excel/*.xlsx`，表结构源是 `Data/Defines/*.xml`。
- 统一执行 `Data/gen_all.bat`；运行时读取 `.bytes`，JSON 只用于查看和排错。
- Excel 字段名和大小写必须与对应 XML 一致，不要直接修改生成的 C#、`.bytes` 或 JSON。
- `id` 通常是全局唯一行 Id；`groupId` 是把多行组成一组的业务 Id，两者不要混用。
- `itemId`、`buildingId`、`questId` 等字段都填写目标表的 `id`，不是 Excel 行号。
- `*Location` 必须是 `Assets/` 开头的 Unity 资源路径；`*Seconds` 单位为秒。
- `enable = false` 通常表示保留配置但不参与运行；并非所有旧模块都统一检查此字段，修改后仍需验证调用点。

## 特殊文件状态

`Data/Defines/__root__.xml` 当前正式包含 35 个根目录工作簿，具体文件已经列在页面顶部的“表目录”中。下面只说明不属于普通根表输入或存在异常的文件。

### `tech_node.xlsx`

配置异常。已有 schema、生成代码和运行时代码，但 `tech_node.xml` 当前没有被 `__root__.xml` include。再次完整生成可能让科技表从新生成结果中消失，修复根配置前不要把当前产物当成稳定状态。

### `Wave/wave1.xlsx`

正式的关卡独立波次输入。由 `gen_wave_all_no_overwrite.bat` 生成 `Assets/Data/Bin/Wave/wave1.bytes` 和对应 JSON，不覆盖根目录 `wave.xlsx`；字段与 `wave.xlsx` 相同。

### `skill_luban_excels_fixed/`

该目录下的 `skill.xlsx`、`skill_action.xlsx`、`skill_modifier.xlsx`、`skill_system_enum.xlsx` 都是历史修复/备份副本，不在当前 Luban 输入路径中，不要在这里修改正式技能配置。

## 物品

### `item.xlsx`

物品主表，同时服务塔防临时背包、经营背包、掉落表现和塔防结算转换。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 物品 Id，供奖励、消耗、蓝图、任务等表引用。 |
| `name` | string | 配置名或回退显示名；正式显示优先走 `item.{id}.name` 本地化。 |
| `description` | string | 配置描述或回退描述；正式显示优先走 `item.{id}.description`。 |
| `itemType` | int | 行为分类，见 `ItemType`。它只表示大类，不自动实现使用效果。 |
| `useScope` | int | 塔防结算适用范围，见 `ItemUseScope`；当前不控制经营背包点击使用。 |
| `iconLocation` | string | UI 图标资源完整路径。 |
| `dropPrefabLocation` | string | 地面掉落物 prefab 完整路径，`ItemDropManager` 使用。 |
| `autoPick` | bool | 生成地面掉落后是否立即自动拾取。 |
| `maxStack` | int | 设计上的单格堆叠上限；当前 `BagManager` 尚未执行该上限。 |
| `settlementItemId` | int | 塔防结算时转换成的经营物品 Id；`<= 0` 表示不转换。 |
| `settlementCountPerItem` | int | 每个塔防物品换算出的经营物品数量。 |

#### 枚举值

| 字段 | 数值 | 名称 | 说明 |
| --- | ---: | --- | --- |
| `itemType` | 0 | None | 未分类。 |
| `itemType` | 1 | Currency | 货币。 |
| `itemType` | 2 | Consumable | 消耗品；仍需具体效果实现。 |
| `itemType` | 3 | Blueprint | 蓝图类物品。 |
| `itemType` | 4 | Seed | 种子。 |
| `itemType` | 5 | Material | 材料。 |
| `itemType` | 6 | Tool | 工具。 |
| `useScope` | 0 | None | 不参与塔防结算转换。 |
| `useScope` | 1 | BattleOnly | 仅塔防内使用。 |
| `useScope` | 2 | Settlement | 可结算到经营模式。 |
| `useScope` | 3 | Both | 两种范围都适用。 |

`useScope` 当前只控制塔防结算时是否转换为经营物品，不等同于“背包里能否点击使用”。

当前点击使用状态：工具可被选中，并在采集/耕作等动作成功后确认一次实际使用和发送任务完成事件；工具本身不会因此被消耗。普通消耗品尚无 Excel 效果定义，只有 `itemType = Consumable` 不会自动产生效果。

## 本地化

### `localization.xlsx`

界面文本和配置名称的多语言来源。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `key` | string | 唯一本地化键，例如 `ui.quest.main_title`、`item.20300001.name`。 |
| `zhCn` | string | 简体中文文本。 |
| `en` | string | 英文文本。 |

## 仓库容量

### `storage_capacity.xlsx`

按物品定义不同仓库等级的容量。表已加载到 `DataManager`，当前没有业务代码读取，属于已建表未接入。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `itemId` | int | 物品 Id，同时作为本表主键。 |
| `level1` | int | 1 级仓库对该物品的容量。 |
| `level2` | int | 2 级仓库对该物品的容量。 |
| `level3` | int | 3 级仓库对该物品的容量。 |
| `level4` | int | 4 级仓库对该物品的容量。 |
| `enable` | bool | 是否启用该物品的容量配置。 |

## 地图资源

### `resource.xlsx`

地图资源对象的玩法配置。地图 JSON 中资源对象的 `ConfigId` 应填写这里的 `id`。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 资源配置 Id。 |
| `name` | string | 资源名称。 |
| `resourceType` | int | 资源类别，决定采集动作需要的工具类型。 |
| `interactionType` | int | 交互方式：直接拾取、重复采集或建矿目标。 |
| `gatherConfigId` | int | `gather.id`；仅 `Gather` 交互需要。 |
| `pickupRewardGroupId` | int | `reward.groupId`；`Pickup` 交互完成时发放。 |
| `mineBuildingId` | int | `world_building.id`；`MineTarget` 完成时创建的矿建筑。 |
| `prefabLocation` | string | 资源对象 prefab 完整路径。 |
| `iconLocation` | string | 资源图标完整路径。 |
| `blocksBuild` | bool | 是否阻止所在格建造。 |
| `blocksMove` | bool | 是否阻止寻路通过。 |
| `enable` | bool | 是否允许加载和交互。 |

#### 枚举值

| 字段 | 数值 | 名称 | 说明 |
| --- | ---: | --- | --- |
| `resourceType` | 0 | None | 未分类资源。 |
| `resourceType` | 1 | Tree | 树木，通常使用斧。 |
| `resourceType` | 2 | Stone | 石头，通常使用镐。 |
| `resourceType` | 3 | Ore | 矿石。 |
| `resourceType` | 4 | Plant | 植物。 |
| `interactionType` | 0 | None | 不支持资源交互。 |
| `interactionType` | 1 | Pickup | 播放拾取动作后一次性获得奖励。 |
| `interactionType` | 2 | Gather | 按 `gather.xlsx` 重复采集。 |
| `interactionType` | 3 | MineTarget | 使用建矿工具创建矿建筑。 |

## 采集

### `gather.xlsx`

定义可重复采集节点的次数、单次奖励和刷新。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 采集配置 Id，由 `resource.gatherConfigId` 引用。 |
| `name` | string | 配置名称。 |
| `rewardGroupId` | int | 每次成功采集发放的 `reward.groupId`。 |
| `depleteAfterTimes` | int | 节点耗尽前可成功采集的次数；`<= 0` 表示不按次数耗尽。树配 3 即砍 3 次。 |
| `respawnSeconds` | int | 耗尽后恢复等待秒数；`<= 0` 表示不刷新并可移除地图对象。 |
| `enable` | bool | 是否启用。 |

## 奖励

### `reward.xlsx`

通用奖励组。一个 `groupId` 可以有多行，解析时当前会把该组所有有效行都发放一次。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 奖励明细行 Id。 |
| `groupId` | int | 奖励组 Id，供采集、拾取、任务、建筑产出等引用。 |
| `itemId` | int | 发放的 `item.id`。 |
| `minCount` | int | 随机数量下限，包含该值。 |
| `maxCount` | int | 随机数量上限，包含该值；必须大于 0。树木头 3-5 就在这里配置。 |
| `weight` | int | 预留权重；当前 `RewardResolver` 不读取，不能用它实现多选一。 |

## 成本

### `world_cost.xlsx`

经营系统通用成本组，历史命名保留。一个组的所有有效明细都会被消耗。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 成本明细行 Id。 |
| `groupId` | int | 成本组 Id，供建筑等级、科技等引用。 |
| `itemId` | int | 要消耗的 `item.id`。 |
| `count` | int | 要消耗的数量。 |

## 建筑

### `world_building.xlsx`

经营建筑主表，历史命名保留。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 建筑配置 Id。 |
| `name` | string | 配置名或本地化回退名称。 |
| `buildingType` | int | 建筑主类型，见 `BuildingType`。 |
| `subType` | int | 主类型内细分；农田和矿的枚举不同。 |
| `buildCategory` | int | 建造面板页签分类，见 `BuildCategory`。 |
| `sortOrder` | int | 同一建造分类内排序，越小越靠前。 |
| `sizeX` | int | 建筑占地 X 格数，非正数按 1。 |
| `sizeZ` | int | 建筑占地 Z 格数，非正数按 1。 |
| `prefabLocation` | string | 建筑 prefab 完整路径。 |
| `iconLocation` | string | 建造 UI 图标完整路径。 |
| `desc` | string | 描述或本地化回退文本。 |
| `maxCount` | int | 当前地图允许建造的最大数量；非正数通常表示不限制。 |
| `unlockHouseLevel` | int | 所需住宅最高等级；`<= 0` 表示无此要求。 |
| `unlockBuildingId` | int | 前置建筑 Id。 |
| `unlockBuildingLevel` | int | 前置建筑要求等级。 |
| `enable` | bool | 是否启用。 |
| `showInBuildPanel` | bool | 是否显示在建造面板。 |
| `defaultUnlocked` | bool | 新档是否默认解锁；需与 `unlockSourceType = Default` 一致。 |
| `unlockSourceType` | int | 解锁来源，见 `UnlockSourceType`。Tech 类型必须有科技节点指向该建筑。 |

#### 枚举值

| 字段 | 数值 | 名称 | 说明 |
| --- | ---: | --- | --- |
| `buildingType` | 0 | None | 未分类。 |
| `buildingType` | 1 | House | 住宅。 |
| `buildingType` | 2 | Warehouse | 仓库。 |
| `buildingType` | 3 | Workbench | 工作台。 |
| `buildingType` | 4 | CarpentryBench | 木工台。 |
| `buildingType` | 5 | Furnace | 熔炉。 |
| `buildingType` | 6 | Blacksmith | 铁匠台。 |
| `buildingType` | 7 | Mill | 磨坊。 |
| `buildingType` | 8 | FarmPlot | 农田。 |
| `buildingType` | 9 | Mine | 矿场。 |
| `buildCategory` | 0 | All | UI 查询全部分类使用。 |
| `buildCategory` | 1 | Building | 基础建筑。 |
| `buildCategory` | 2 | Production | 生产建筑。 |
| `buildCategory` | 3 | Resource | 资源建筑。 |
| `buildCategory` | 4 | Farm | 农业建筑。 |
| `buildCategory` | 5 | Decoration | 装饰。 |
| `buildCategory` | 6 | Special | 特殊建筑。 |
| `unlockSourceType` | 0 | None | 未指定解锁来源。 |
| `unlockSourceType` | 1 | Default | 新档默认解锁。 |
| `unlockSourceType` | 2 | Tech | 由科技解锁。 |
| `unlockSourceType` | 3 | Runtime | 由运行时业务解锁。 |
| `subType`（FarmPlot） | 0 | None | 未分类农田。 |
| `subType`（FarmPlot） | 1 | Crop | 作物田。 |
| `subType`（FarmPlot） | 2 | Flower | 花田。 |
| `subType`（FarmPlot） | 3 | Herb | 草药田。 |
| `subType`（Mine） | 0 | None | 未分类矿场。 |
| `subType`（Mine） | 1 | Stone | 石矿。 |
| `subType`（Mine） | 2 | Copper | 铜矿。 |
| `subType`（Mine） | 3 | Iron | 铁矿。 |

### `world_building_level.xlsx`

每个建筑每一级的成本和建造时间。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 等级配置行 Id。 |
| `buildingId` | int | `world_building.id`。 |
| `level` | int | 建筑等级，从 1 开始。 |
| `buildCostGroupId` | int | 本级建造或升级消耗的 `world_cost.groupId`。 |
| `buildSeconds` | int | 建造/升级完成等待秒数；`<= 0` 立即完成。 |
| `enable` | bool | 是否启用该等级。 |

### `world_building_income.xlsx`

建筑按周期自动产出。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 产出配置行 Id。 |
| `buildingId` | int | `world_building.id`。 |
| `level` | int | 对应建筑等级。 |
| `outputRewardGroupId` | int | 每周期发放的 `reward.groupId`。 |
| `cycleSeconds` | int | 产出周期秒数，必须大于 0。 |
| `enable` | bool | 是否启用该等级产出。 |

## 科技

### `tech_node.xlsx`

科技树节点。**当前必须先修复 `Data/Defines/__root__.xml` 的 include 缺失，再把它视为可稳定重新生成的正式表。**

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 科技节点 Id。 |
| `branch` | int | 科技分支，见 `TechBranch`。 |
| `name` | string | 节点名称或本地化回退文本。 |
| `iconLocation` | string | 科技图标完整路径。 |
| `sortOrder` | int | 分支内排序。 |
| `preTechId` | int | 前置科技 Id；`0` 表示没有。代码会沿此字段检查整条前置链。 |
| `unlockBuildingId` | int | 研究后解锁的 `world_building.id`。 |
| `unlockSystem` | string | 预留的系统解锁标识；当前科技业务未读取。 |
| `costGroupId` | int | 研究消耗的 `world_cost.groupId`。 |
| `desc` | string | 节点说明或本地化回退文本。 |
| `enable` | bool | 是否启用。 |
| `defaultUnlocked` | bool | 新档是否默认已研究/解锁。 |

#### `branch` 枚举值

| 数值 | 名称 | 说明 |
| ---: | --- | --- |
| 0 | None | 未分支。 |
| 1 | Building | 建筑科技。 |
| 2 | Farm | 农业科技。 |
| 3 | Production | 生产科技。 |
| 4 | Resource | 资源科技。 |
| 5 | Special | 特殊科技。 |

## 农田作物

### `world_crop.xlsx`

农田作物定义，历史命名保留。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 作物配置 Id。 |
| `name` | string | 作物名称。 |
| `seedItemId` | int | 播种使用的 `item.id`。 |
| `seedCost` | int | 每个农田格播种消耗的种子数量。 |
| `outputItemId` | int | 成熟后持续产出的 `item.id`。 |
| `growSeconds` | int | 从播种到成熟的秒数。 |
| `outputCountPerSecond` | int | 每格每秒产量，离线结算和每分钟 UI 都据此计算。 |
| `plotColor` | string | 地块颜色字符串，支持 Unity 可解析的十六进制颜色。 |
| `cropColor` | string | 作物表现颜色；未成熟时会与灰色混合。 |
| `enable` | bool | 是否启用。 |

## 蓝图

### `blueprint.xlsx`

制作/生产主表。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 蓝图 Id。 |
| `name` | string | 蓝图名称。 |
| `buildingId` | int | 所需生产建筑 Id；`0` 表示可直接制作。 |
| `unlockTechId` | int | 所需已研究科技 Id；`0` 表示无要求。 |
| `unlockQuestId` | int | 所需任务 Id；`0` 表示无要求。 |
| `durationSeconds` | int | 设计上的制作时长；当前 `BlueprintManager.TryComplete` 为立即完成，字段尚未接入计时队列。 |
| `enable` | bool | 是否启用。 |

### `blueprint_item.xlsx`

蓝图输入和输出明细。一张蓝图通常配置多条输入和至少一条输出。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 明细行 Id。 |
| `blueprintId` | int | 所属 `blueprint.id`。 |
| `itemKind` | int | 明细方向：当前代码按 `1 = Input`、`2 = Output` 解释。 |
| `itemId` | int | 输入或输出的 `item.id`。 |
| `count` | int | 输入消耗量或输出获得量。 |
| `sortOrder` | int | UI/处理排序。 |
| `enable` | bool | 是否启用该明细。 |

## 任务

### `quest.xlsx`

任务主表。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | Quest Id。 |
| `name` | string | 任务名或本地化回退文本。 |
| `description` | string | 任务描述或本地化回退文本。 |
| `questType` | string | 策划分类标签；当前核心状态机不按该字符串分支。 |
| `rewardGroupId` | int | 领取时发放的 `reward.groupId`；`<= 0` 表示无奖励。 |
| `preQuestId1` | int | 前置任务 1；`0` 表示空。 |
| `preQuestId2` | int | 前置任务 2；`0` 表示空。 |
| `preQuestId3` | int | 前置任务 3；`0` 表示空。 |
| `acceptMode` | int | 接取方式，见 `QuestAcceptMode`。 |
| `acceptEventType` | int | Event 接取时匹配的 `QuestEventType`。 |
| `acceptTargetId` | int | Event 接取时匹配的事件目标 Id。 |
| `autoAccept` | bool | 历史兼容字段；当前接取逻辑以 `acceptMode` 为主，新增配置不要只依赖此字段。 |
| `enable` | bool | 是否启用任务。 |

#### `acceptMode` 枚举值

| 数值 | 名称 | 说明 |
| ---: | --- | --- |
| 0 | Auto | 前置满足后自动接取。 |
| 1 | Manual | 由 UI、NPC 或任务板手动接取。 |
| 2 | Event | 匹配 `acceptEventType` 和 `acceptTargetId` 后接取。 |

### `quest_objective.xlsx`

任务目标明细，一项任务可以有多个目标。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 目标明细行 Id。 |
| `questId` | int | 所属 `quest.id`。 |
| `objectiveId` | int | 任务内目标 Id，用于存档和事件进度定位；同一任务内应唯一。 |
| `type` | int | 目标类型，见 `QuestObjectiveType`。 |
| `targetId` | int | 目标对象 Id；按类型解释为物品、蓝图、建筑、科技、NPC、区域或 flag Id。 |
| `targetCount` | int | 完成所需数量，通常至少为 1。 |
| `text` | string | 目标显示文本或回退文本。 |
| `sortOrder` | int | 任务详情中的目标排序。 |
| `enable` | bool | 是否启用该目标。 |

#### `type` 枚举值

| 数值 | 名称 | 说明 |
| ---: | --- | --- |
| 0 | None | 无目标。 |
| 1 | ItemCount | 当前持有物品数量。 |
| 2 | ItemGainCount | 累计获得物品数量。 |
| 3 | ItemUseCount | 累计实际使用物品次数。 |
| 4 | Blueprint | 完成指定蓝图。 |
| 5 | BuildBuilding | 建造指定建筑。 |
| 6 | BuildBuildingType | 建造指定类型建筑。 |
| 7 | UpgradeBuilding | 升级建筑。 |
| 8 | FarmCount | 当前农田数量。 |
| 9 | PlantCrop | 播种作物。 |
| 10 | HarvestCrop | 收获作物。 |
| 11 | TechResearched | 研究指定科技。 |
| 12 | TalkNpc | 与指定 NPC 对话。 |
| 13 | EnterArea | 进入指定区域。 |
| 14 | CustomFlag | 收到自定义标记。 |

#### Quest 事件值

任务接取的 `acceptEventType` 和剧情完成事件共同使用以下数值：

| 数值 | 名称 | 说明 |
| ---: | --- | --- |
| 0 | None | 无事件。 |
| 1 | StartQuest | 开始指定任务。 |
| 2 | CustomFlag | 自定义标记。 |
| 3 | TalkNpc | 与 NPC 对话。 |
| 4 | EnterArea | 进入区域。 |
| 5 | UseItem | 物品效果或业务动作已成功完成。 |
| 6 | GainItem | 获得物品。 |
| 7 | BlueprintCompleted | 完成蓝图。 |
| 8 | BuildBuilding | 建造建筑。 |
| 9 | UpgradeBuilding | 升级建筑。 |
| 10 | PlantCrop | 播种作物。 |
| 11 | HarvestCrop | 收获作物。 |

## 剧情

### `story.xlsx`

简单文字剧情主表。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | Story Id。 |
| `title` | string | 剧情标题，当前直接显示。 |
| `triggerMode` | int | 触发方式，见 `StoryTriggerMode`。 |
| `triggerTargetId` | int | 触发目标；任务完成模式填 quest Id，事件模式填相应目标 Id。 |
| `completeQuestEventType` | int | 剧情结束后发送给 Quest 的 `QuestEventType`；`0` 不发送。 |
| `completeQuestTargetId` | int | 上述 Quest 事件的目标 Id。 |
| `nextStoryId` | int | 完成后自动尝试播放的下一段 Story Id；`0` 表示没有。 |
| `repeatable` | bool | 是否允许重复播放。 |
| `enable` | bool | 是否启用。 |

#### `triggerMode` 枚举值

| 数值 | 名称 | 说明 |
| ---: | --- | --- |
| 0 | Manual | 由代码显式开始。 |
| 1 | AutoOnNewGame | 新游戏加载地图后自动尝试播放。 |
| 2 | QuestCompleted | 指定任务完成后触发。 |
| 3 | CustomFlag | 自定义标记触发。 |
| 4 | EnterArea | 进入区域触发。 |
| 5 | TalkNpc | 与 NPC 对话触发。 |

### `story_step.xlsx`

剧情推进 Step。一个 Story 可以混排文本、静态插画、插画镜头运动和轻量引导。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | Step Id。 |
| `storyId` | int | 所属 `story.id`。 |
| `stepIndex` | int | 播放顺序，越小越先；相同时按 `id`。 |
| `stepType` | int | `0=Text`、`1=Illustration`、`2=Mixed`、`3=Guide`。 |
| `text` | string | 当前 Step 的剧情正文；插画或引导 Step 可留空。 |
| `illustrationPath` | string | 静态剧情插画的 Texture2D 资源路径。 |
| `motionPreset` | int | `0=None`、`1=ZoomOut`、`2=PanLeftToRight`、`3=PanRightToLeft`、`4=ZoomIn`。 |
| `motionDuration` | float | 插画镜头运动时长，单位为秒。 |
| `advanceMode` | int | `0=Click`、`1=MotionComplete`、`2=AutoAfterDelay`、`3=GuideTargetClicked`。 |
| `autoAdvanceDelay` | float | 自动推进等待时间，单位为秒。 |
| `guideTargetId` | string | 轻量引导目标的稳定 Id。 |
| `guideText` | string | 引导提示文本。 |
| `allowTargetInteraction` | bool | 引导时目标是否可以被点击。 |
| `enable` | bool | 是否启用该 Step。 |

## 塔防基地

### `base.xlsx`

塔防基地配置。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 基地配置 Id。 |
| `name` | string | 基地名称。 |
| `description` | string | 基地描述。 |
| `hp` | int | 最大生命值，运行时至少按 1。 |
| `defense` | int | 防御值，运行时至少按 0。 |
| `prefabLocation` | string | 基地 prefab 完整路径。 |
| `iconLocation` | string | 基地图标完整路径。 |
| `hitEffect` | string | 受击特效资源标识/路径。 |
| `deadEffect` | string | 摧毁特效资源标识/路径。 |
| `enable` | bool | 是否启用。 |
| `actionGroupId` | int | 点击基地后显示的目标操作组；`0` 表示没有操作。 |

## 塔防关卡

### `map.xlsx`

塔防关卡入口配置。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 地图配置 Id，UI 和战斗流程使用。 |
| `name` | string | 地图名或本地化回退文本。 |
| `description` | string | 地图描述或本地化回退文本。 |
| `mapId` | int | 地图 JSON 文件号，加载 `Assets/Data/Map/{mapId}.json`。 |
| `initialGold` | int | 进入关卡时加入塔防临时背包的初始金币。 |
| `baseId` | int | `base.id`。 |
| `waveEasy` | string | 简单难度波次 `.bytes` 完整资源路径。 |
| `waveNormal` | string | 普通难度波次 `.bytes` 完整资源路径；当前默认加载此字段。 |
| `waveHard` | string | 困难难度波次 `.bytes` 完整资源路径。 |

## 塔防单位

### `npc.xlsx`

塔防单位配置。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | NPC 配置 Id。 |
| `name` | string | 名称。 |
| `description` | string | 描述。 |
| `kind` | int | 实体大类，塔防生成 NPC 要填 `GameEntityKind.Actor = 100`。 |
| `actorType` | int | Actor 细分，敌人填 `ActorType.Enemy = 1`。 |
| `prefabLocation` | string | 单位 prefab 完整路径。 |
| `moveSpeed` | float | 每秒移动速度。 |
| `damageToBase` | int | 对基地单次攻击伤害。 |
| `modelScale` | float | 实例模型统一缩放；非正数通常回退为 1。 |
| `attackRange` | float | 攻击目标判定距离。 |
| `attackInterval` | float | 两次攻击之间的秒数。 |
| `maxHp` | int | 最大生命值。 |
| `rewardGold` | int | 击杀后直接加入塔防金币的数量。 |
| `actionGroupId` | int | 点击 NPC 后显示的目标操作组；`0` 表示没有操作。 |

#### 枚举值

| 字段 | 数值 | 名称 | 说明 |
| --- | ---: | --- | --- |
| `kind` | 0 | None | 未分类实体。 |
| `kind` | 100 | Actor | 角色实体；塔防 NPC 应使用此值。 |
| `kind` | 200 | Structure | 建筑实体。 |
| `kind` | 300 | Prop | 场景道具。 |
| `kind` | 400 | ItemDrop | 物品掉落。 |
| `kind` | 500 | Projectile | 投射物。 |
| `kind` | 600 | AreaObject | 区域对象。 |
| `kind` | 700 | ResourceNode | 资源节点。 |
| `actorType` | 0 | None | 未分类角色。 |
| `actorType` | 1 | Enemy | 敌人。 |
| `actorType` | 2 | Hero | 英雄。 |
| `actorType` | 3 | Worker | 工人。 |
| `actorType` | 4 | Merchant | 商人。 |
| `actorType` | 5 | QuestNpc | 任务 NPC。 |
| `actorType` | 6 | TrialNpc | 试炼 NPC。 |
| `actorType` | 7 | Resident | 居民。 |

### `npc_drop.xlsx`

NPC 死亡物品掉落。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 掉落明细行 Id。 |
| `npcId` | int | 所属 `npc.id`。 |
| `itemId` | int | 掉落的 `item.id`。 |
| `minCount` | int | 掉落数量下限，包含该值。 |
| `maxCount` | int | 掉落数量上限，包含该值。 |
| `chancePermyriad` | int | 万分比概率，`10000 = 100%`。 |

## 防御塔

### `tower.xlsx`

防御塔主表。名称、类型、图标仍来自本表；战斗数值和 prefab 应优先配置在 `tower_level.xlsx`。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 防御塔 Id。 |
| `name` | string | 名称或本地化回退文本。 |
| `description` | string | 描述或本地化回退文本。 |
| `towerType` | int | 塔类别，见 `TowerType`。 |
| `prefabLocation` | string | 旧版主表 prefab；当前建造优先使用等级表。 |
| `costItemId` | int | 旧版建造消耗物品；当前建造优先使用等级表。 |
| `costCount` | int | 旧版建造数量；UI 在等级配置缺失时可能回退使用。 |
| `range` | float | 旧版攻击范围；当前战斗使用等级表。 |
| `damage` | int | 旧版攻击伤害；当前战斗使用等级表。 |
| `attackInterval` | float | 旧版攻击间隔；当前战斗使用等级表。 |
| `unlockLevel` | int | 解锁等级预留/旧逻辑字段。 |
| `enable` | bool | 是否在建造列表和流程中启用。 |
| `attackEffect` | string | 旧版攻击特效；当前战斗使用等级表。 |
| `hitEffect` | string | 旧版命中特效；当前战斗使用等级表。 |
| `upgradeCost` | int | 旧版升级成本；当前升级使用等级表。 |
| `SellGoldRate` | float | 旧版出售返还比例；字段首字母大写是当前 schema 的历史格式。 |
| `canUpgrade` | bool | 是否允许升级；运行时还要求 `tower_level.xlsx` 存在下一级。 |
| `iconLocation` | string | 防御塔 UI 图标完整路径。 |
| `actionGroupId` | int | 点击塔后显示的目标操作组；`0` 表示没有操作，当前塔统一使用 `100`。 |

#### `towerType` 枚举值

| 数值 | 名称 | 说明 |
| ---: | --- | --- |
| 0 | Normal | 普通塔。 |
| 1 | Arrow | 箭塔。 |
| 2 | Cannon | 炮塔。 |
| 3 | Ice | 冰塔。 |

当前 Excel 中存在 `towerType = 4`，它不在代码枚举中，属于待修正配置。

### `tower_level.xlsx`

防御塔逐级数值，是当前建造、攻击、升级、出售的主要数据源。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 等级配置行 Id。 |
| `towerId` | int | 所属 `tower.id`。 |
| `level` | int | 塔等级，从 1 开始。 |
| `prefabLocation` | string | 本等级 prefab 完整路径。升级时路径变化会替换实例。 |
| `costItemId` | int | 本等级建造消耗物品；非正数回退金币。 |
| `buildCost` | int | 建造 1 级塔的消耗数量。 |
| `upgradeCostItemId` | int | 升到本等级的消耗物品；非正数回退金币，当前升级流程已读取。 |
| `upgradeCost` | int | 从上一级升到本级的成本。 |
| `sellGoldRate` | float | 出售时按累计成本返还的比例，例如 `0.7`。 |
| `range` | float | 攻击范围。 |
| `damage` | int | 每次普通攻击伤害。 |
| `attackInterval` | float | 普攻间隔秒数。 |
| `attackEffect` | string | 投射物/攻击特效资源标识或路径。 |
| `hitEffect` | string | 命中特效资源标识或路径。 |
| `skillId` | int | 本等级使用的 `skill.id`；`0` 表示只普通攻击。 |
| `enable` | bool | 是否启用该等级。 |

### `battle_target_action.xlsx`

目标信息面板的通用操作组。目标主表只引用 `groupId`，动作是否最终显示还会经过对应动作处理器的实时条件判断。该表不保存升级价格、出售金额等业务数值。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 动作配置行 Id。 |
| `groupId` | int | 操作组 Id；同组动作会按顺序显示。 |
| `actionType` | int | 动作类型，见下表。 |
| `name` | string | 动作名称，主要用于策划识别和调试。 |
| `iconLocation` | string | 按钮图标完整资源路径。 |
| `sortOrder` | int | 显示顺序，越小越靠前；相同时按 `id`。 |
| `enable` | bool | 是否启用该动作行。 |

| `actionType` | 名称 | 运行时条件与数据来源 |
| ---: | --- | --- |
| 1 | UpgradeTower | 目标必须是塔，`tower.canUpgrade = true`，且存在下一级；费用读取下一级 `upgradeCostItemId/upgradeCost`。 |
| 2 | SellTower | 目标必须是塔，且当前等级 `sellGoldRate > 0`；返还值按累计建造/升级成本实时计算。 |

## 塔防波次

### `wave.xlsx` / `Wave/wave1.xlsx`

根 `wave.xlsx` 会随主 `Tables` 生成，但 `DataManager.Initialize` 随后把 `Wave` 置空，当前战斗不会使用它。`Wave/wave1.xlsx` 这类文件才是按 `map.wave*` 完整路径独立加载的实际关卡波次数据。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 波次行 Id，也决定行顺序。 |
| `npcConfigId` | int | 本行生成的 `npc.id`。 |
| `count` | int | 生成数量。 |
| `interval` | float | 同一行相邻单位生成间隔秒数。 |
| `startDelay` | float | 开始该行前等待秒数。 |
| `spawnMode` | int | 出生点选择方式，见 `WaveSpawnMode`。 |
| `description` | string | 策划备注/波次描述，不参与生成逻辑。 |

#### `spawnMode` 枚举值

| 数值 | 名称 | 说明 |
| ---: | --- | --- |
| 0 | None | 未指定；运行时回退到第一个出生点。 |
| 1 | FirstSpawnPoint | 使用第一个出生点。 |
| 2 | RandomSpawnPoint | 每次随机选择出生点。 |

## Skill 兼容配置

当前塔防业务实际通过 `AbilityConfigConverter` 把这些 Skill 表转换成 Ability 运行时定义。`skill_luban_excels_fixed` 下同名文件不是正式输入。

### `skill.xlsx`

当前可运行的塔防技能主表，加载后转换为 Ability 运行时定义。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | Skill Id，由 `tower_level.skillId` 等引用。 |
| `name` | string | 名称或本地化回退文本。 |
| `description` | string | 描述或本地化回退文本。 |
| `iconLocation` | string | 技能图标完整路径。 |
| `behavior` | int | 位标记：1 NoTarget、2 UnitTarget、4 PointTarget、8 Passive、16 Toggle、32 Channel、64 Aoe，可相加。 |
| `targetTeam` | int | 目标阵营：0 None、1 Friendly、2 Enemy、3 Both。 |
| `castRange` | float | 施法距离。 |
| `aoeRadius` | float | 范围半径。 |
| `castPoint` | float | 前摇秒数。 |
| `channelTime` | float | 持续施法秒数。 |
| `cooldown` | float | 冷却秒数。 |
| `costResourceId` | int | 消耗资源/物品 Id；技能 UI 用它查可释放次数。 |
| `costCount` | int | 单次消耗数量，也映射为 Ability 的 ManaCost。 |
| `abilityActionGroupId` | int | 释放时执行的 `skill_action.groupId`。 |
| `intrinsicModifierId` | int | 常驻被动 `skill_modifier.id`；`0` 表示无。 |
| `enable` | bool | 是否启用。 |

### `skill_action.xlsx`

定义技能和 Modifier 回调执行的动作组。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 动作行 Id。 |
| `groupId` | int | 动作组 Id，一次技能或 Modifier 回调可执行组内多行。 |
| `order` | int | 组内执行顺序，相同时按 `id`。 |
| `actionType` | int | 0 None、1 Damage、2 Heal、3 ApplyModifier、4 FireEvent；当前 FireEvent 未映射为有效动作。 |
| `targetType` | int | 0 None、1 Caster、2 Unit、3 Point、4 Area、5 CurrentTargets。 |
| `value` | float | 伤害、治疗等动作主数值。 |
| `radius` | float | 设计范围半径；当前兼容转换器没有读取。 |
| `duration` | float | 添加 Modifier 时的覆盖持续时间；0 当前按未指定处理。 |
| `modifierId` | int | 要添加的 `skill_modifier.id`。 |
| `damageType` | int | 0 None、1 Physical、2 Magical、3 Pure。 |
| `effectLocation` | string | 动作完成时自动播放的一次性特效路径；同一动作组不要在多行重复填写相同路径。没有逻辑动作时可单独转成播放特效。 |
| `soundLocation` | string | 动作音效路径；没有逻辑动作时可单独转成播放音效。 |

### `skill_modifier.xlsx`

定义 Buff、Debuff、周期效果、属性修改和单位状态。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | Modifier Id。 |
| `name` | string | 配置名或本地化回退名。 |
| `modifierType` | int | 0 None、1 Property、2 Periodic、3 State、4 Aura；当前转换主要依赖具体字段。 |
| `isDebuff` | bool | 是否负面效果。 |
| `isHidden` | bool | 是否在 Modifier UI 中隐藏。 |
| `isPurgable` | bool | 是否可驱散。 |
| `removeOnDeath` | bool | 单位死亡时是否移除。 |
| `duration` | float | 默认持续秒数。 |
| `interval` | float | 周期触发间隔秒数。 |
| `maxStack` | int | 最大层数；非正数在转换时按 1。 |
| `propertyType` | int | 属性类型，具体值见 `skill_system_enum.xlsx` 的 `SkillModifierPropertyType`。 |
| `propertyValue` | float | 属性修改值。百分比字段按 Ability 运行时约定解释。 |
| `state` | int | 单位状态：0 None、1 Stunned、2 Silenced、3 Rooted、4 Invulnerable。 |
| `triggerEventType` | int | 触发事件，见枚举表 `SkillTriggerEventType`。 |
| `triggerActionGroupId` | int | 事件触发时执行的 `skill_action.groupId`。 |
| `periodicActionGroupId` | int | 每个 interval 执行的动作组。 |
| `onCreatedActionGroupId` | int | Modifier 创建时执行的动作组。 |
| `onDestroyActionGroupId` | int | Modifier 销毁时执行的动作组。 |
| `effectLocation` | string | Modifier 持续表现特效路径；转换为 `SustainedEffectName`，由 Modifier 持有可停止表现句柄，并在 Modifier 移除、对象注销或战斗清理时停止。不会转换成一次性 `OnCreated PlayEffect`。 |

### `skill_system_enum.xlsx`

Skill 配表枚举说明表，本身主要供人和工具查值，运行时转换器仍使用显式数字映射。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 枚举说明行 Id。 |
| `group` | string | 枚举组名，例如 `SkillActionType`。 |
| `name` | string | 枚举项名称。 |
| `value` | int | Excel 实际填写的整数值；位标记组可按位组合。 |
| `description` | string | 枚举项说明。 |

## Ability 新配置

这 7 张表是更完整的 Ability 数据模型，但当前 Excel/JSON 为空，业务侧也没有读取这些配置建立运行时定义。现阶段新增可用塔防技能仍应先走 `skill*.xlsx`；不要因为表已经生成就认为 Ability Excel 链路已完成。

字符串数值字段用于表达逐等级值或特殊值引用，格式必须与未来 Ability 配置转换器约定一致；当前没有已接入的解析流程可作为稳定格式保证。

### `AbilityConfig.xlsx`

新 Ability 数据模型的技能主表；当前尚未接入业务转换。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | Ability Id。 |
| `name` | string | 内部唯一名。 |
| `displayName` | string | 显示名称或本地化标识。 |
| `description` | string | 描述或本地化标识。 |
| `icon` | string | 图标资源路径。 |
| `maxLevel` | int | 最大等级。 |
| `behavior` | int | Ability 行为位标记。 |
| `targetTeam` | int | 可选目标阵营。 |
| `targetType` | int | 可选目标单位类型。 |
| `targetFlags` | int | 额外目标过滤位标记。 |
| `castRange` | string | 各等级施法距离。 |
| `aoeRadius` | string | 各等级范围半径。 |
| `castPoint` | string | 各等级前摇秒数。 |
| `castBackswing` | string | 各等级后摇秒数。 |
| `channelTime` | string | 各等级持续施法秒数。 |
| `cooldown` | string | 各等级冷却秒数。 |
| `manaCost` | string | 各等级资源消耗。 |
| `maxCharges` | int | 最大充能数。 |
| `chargeRestoreTime` | float | 单次充能恢复秒数。 |
| `startFullCharges` | bool | 初始是否满充能。 |
| `chargeUsesCooldown` | bool | 充能恢复是否复用冷却规则。 |
| `actionGroupId` | int | 释放时执行的 `AbilityAction.groupId`。 |
| `intrinsicModifierId` | int | 常驻 `AbilityModifier.id`。 |
| `scriptName` | string | 自定义脚本扩展名。 |
| `enable` | bool | 是否启用。 |

### `AbilityAction.xlsx`

新 Ability 数据模型的动作组明细；当前尚未接入业务转换。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 动作行 Id。 |
| `groupId` | int | 动作组 Id。 |
| `order` | int | 组内执行顺序。 |
| `actionType` | int | 动作类型。 |
| `target` | int | 动作目标来源。 |
| `value` | string | 各等级主数值。 |
| `valueSpecialName` | string | 主数值引用的 `AbilitySpecialValue.name`。 |
| `duration` | string | 各等级持续时间。 |
| `durationSpecialName` | string | 持续时间引用的特殊值名称。 |
| `damageType` | int | 伤害类型。 |
| `damageFlags` | int | 伤害额外标记。 |
| `modifierId` | int | 要添加/移除的 `AbilityModifier.id`。 |
| `projectileId` | int | 要创建的 `AbilityProjectile.id`。 |
| `purgePositiveBuffs` | bool | 驱散动作是否移除正面 Buff。 |
| `purgeDebuffs` | bool | 驱散动作是否移除 Debuff。 |
| `purgeOnlyPurgable` | bool | 是否只移除标记为可驱散的 Modifier。 |
| `effectName` | string | 表现特效资源名/路径。 |
| `soundName` | string | 音效资源名/路径。 |

### `AbilityModifier.xlsx`

新 Ability 数据模型的 Modifier 主表；当前尚未接入业务转换。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | Modifier Id。 |
| `name` | string | 内部唯一名。 |
| `displayName` | string | 显示名称。 |
| `isHidden` | bool | 是否在 UI 中隐藏。 |
| `isDebuff` | bool | 是否为负面效果。 |
| `isPurgable` | bool | 是否可驱散。 |
| `removeOnDeath` | bool | 死亡时是否移除。 |
| `duration` | float | 默认持续秒数。 |
| `interval` | float | 周期触发间隔秒数。 |
| `maxStack` | int | 最大叠加层数。 |
| `attributes` | int | Modifier 属性位标记。 |
| `states` | int | 施加的单位状态位标记。 |
| `propertyGroupId` | int | `AbilityModifierProperty.groupId`。 |
| `onCreatedActionGroupId` | int | 创建时动作组。 |
| `onRefreshActionGroupId` | int | 刷新/叠层时动作组。 |
| `onDestroyActionGroupId` | int | 销毁时动作组。 |
| `intervalActionGroupId` | int | 周期动作组。 |
| `triggerEventType` | int | 监听的战斗事件类型。 |
| `triggerActionGroupId` | int | 事件触发动作组。 |
| `auraModifierId` | int | 光环向目标施加的 Modifier Id。 |
| `auraRadius` | float | 光环半径。 |
| `auraDuration` | float | 光环子 Modifier 持续时间。 |
| `auraThinkInterval` | float | 光环扫描间隔。 |
| `auraTargetTeam` | int | 光环目标阵营。 |
| `auraTargetType` | int | 光环目标类型。 |
| `auraTargetFlags` | int | 光环额外过滤标记。 |
| `scriptName` | string | 自定义脚本扩展名。 |

### `AbilityModifierProperty.xlsx`

把多条属性修改组成 Modifier 可引用的属性组；当前尚未接入业务转换。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 属性明细行 Id。 |
| `groupId` | int | 属性组 Id，由 `AbilityModifier.propertyGroupId` 引用。 |
| `property` | int | 要修改的属性枚举值。 |
| `value` | string | 各等级属性修改值。 |

### `AbilityProjectile.xlsx`

新 Ability 数据模型的投射物参数；当前尚未接入业务转换。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 投射物 Id。 |
| `name` | string | 内部名称。 |
| `speed` | float | 飞行速度。 |
| `radius` | float | 碰撞/命中半径。 |
| `distance` | float | 最大飞行距离。 |
| `deleteOnHit` | bool | 首次命中后是否销毁。 |
| `providesVision` | bool | 是否提供视野。 |
| `visionRadius` | float | 视野半径。 |
| `targetTeam` | int | 可命中阵营。 |
| `targetType` | int | 可命中单位类型。 |
| `targetFlags` | int | 额外目标过滤标记。 |
| `effectName` | string | 投射物特效资源名/路径。 |
| `soundName` | string | 飞行/创建音效资源名/路径。 |

### `AbilitySpecialValue.xlsx`

保存 Ability 可按名称引用的逐等级特殊数值；当前尚未接入业务转换。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 特殊值行 Id。 |
| `abilityId` | int | 所属 `AbilityConfig.id`。 |
| `name` | string | 特殊值名称，供 Action 等按名引用。 |
| `values` | string | 各等级数值序列。 |

### `AbilitySystemEnum.xlsx`

新 Ability 配表使用的枚举说明表；当前数据为空。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `id` | int | 枚举说明行 Id。 |
| `group` | string | 枚举组名。 |
| `name` | string | 枚举项名称。 |
| `value` | int | Excel 中实际填写的数值。 |
| `description` | string | 枚举项说明。 |

## 配表关系速查

| 分类 | 来源字段 | 关联目标 |
| --- | --- | --- |
| 地图资源与采集 | `resource.gatherConfigId` | `gather.id` |
|  | `gather.rewardGroupId` | `reward.groupId` → `item.id` |
|  | `resource.pickupRewardGroupId` | `reward.groupId` → `item.id` |
|  | `resource.mineBuildingId` | `world_building.id` |
| 建筑与科技 | `world_building.id` | `world_building_level.buildingId` |
|  | `world_building.id` | `world_building_income.buildingId` |
|  | `world_building_level.buildCostGroupId` | `world_cost.groupId` → `item.id` |
|  | `tech_node.costGroupId` | `world_cost.groupId` |
|  | `tech_node.unlockBuildingId` | `world_building.id` |
| 蓝图、任务与剧情 | `blueprint.id` | `blueprint_item.blueprintId` → `item.id` |
|  | `quest.id` | `quest_objective.questId` |
|  | `quest.rewardGroupId` | `reward.groupId` |
|  | `story.id` | `story_step.storyId` |
| 塔防与技能 | `map.baseId` | `base.id` |
|  | `wave.npcConfigId` | `npc.id` |
|  | `npc.id` | `npc_drop.npcId` → `item.id` |
|  | `tower.id` | `tower_level.towerId` |
|  | `tower.actionGroupId` / `npc.actionGroupId` / `base.actionGroupId` | `battle_target_action.groupId` |
|  | `tower_level.skillId` | `skill.id` |
|  | `skill.abilityActionGroupId` | `skill_action.groupId` |
|  | `skill.intrinsicModifierId` | `skill_modifier.id` |

## 当前配置风险

1. `tech_node.xml` 未被根 schema include，完整重新生成存在删除科技表产物的风险。
2. `reward.weight` 当前未实现，所有同组奖励行都会发放，不能配置权重池。
3. `item.maxStack` 和 `storage_capacity.xlsx` 当前没有落实到背包/仓库容量逻辑。
4. `item.xlsx` 没有“使用动作/效果/消耗数量”字段，普通 Consumable 仍无法仅靠配表使用。
5. `tower.xlsx` 与 `tower_level.xlsx` 有重复战斗字段，当前应以等级表为准，长期应收敛旧字段。
6. Ability 新表目前为空且未接业务转换；Skill 表才是当前可运行输入。
7. `skill_luban_excels_fixed` 是重复副本，容易被误改；确认无需保留后应移出正式数据目录或明确归档。
8. 当前 `tower.xlsx` 存在 `towerType = 4`，但代码 `TowerType` 只定义到 `3 Ice`；应补枚举或改回合法值。
9. 根 `wave.xlsx` 当前生成但不被战斗加载，继续保留会让策划误以为修改它能改变关卡波次。
