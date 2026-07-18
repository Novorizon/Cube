# Quest / Blueprint

本文记录任务、蓝图以及它们和剧情的衔接。Story 模块自身实现见 `Docs/Modules/Story.md`。

## 核心结论

- 任务系统叫 `Quest`，不要改成 `Task`。
- Quest 域新代码默认不要加 `World` 或 `Game` 前缀。
- 制作/生产配置叫 `Blueprint`，不要再叫 `Recipe`。
- Quest / Blueprint / Story 已接入正式 Excel / Luban 表，运行时从 `DataManager` 读取。
- JSON 只是导出查看，正式运行以 `.bytes` 为准。

## 数据来源

当前正式表：

```text
Data/Defines/quest.xml
Data/Defines/quest_objective.xml
Data/Defines/blueprint.xml
Data/Defines/blueprint_item.xml
Data/Defines/story.xml
Data/Defines/story_line.xml

Data/Excel/quest.xlsx
Data/Excel/quest_objective.xlsx
Data/Excel/blueprint.xlsx
Data/Excel/blueprint_item.xlsx
Data/Excel/story.xlsx
Data/Excel/story_line.xlsx

Assets/Data/Bin/tbquest.bytes
Assets/Data/Bin/tbquestobjective.bytes
Assets/Data/Bin/tbblueprint.bytes
Assets/Data/Bin/tbblueprintitem.bytes
Assets/Data/Bin/tbstory.bytes
Assets/Data/Bin/tbstoryline.bytes
```

运行时代码：

```text
QuestManager      DataManager.Instance.Quest + QuestObjective
BlueprintManager  DataManager.Instance.Blueprint + BlueprintItem
StoryManager      DataManager.Instance.Story + StoryLine
```

## 当前文件

```text
Assets/Scripts/Game/Quests/QuestManager.cs
Assets/Scripts/Game/Quests/QuestToastListener.cs
Assets/Scripts/Game/Blueprints/BlueprintManager.cs
Assets/Scripts/Game/Story/StoryManager.cs
Assets/Scripts/Game/Story/StoryPresenter.cs
Assets/Scripts/Game/Story/StoryPanel.cs
Assets/Scripts/Game/UI/Management/QuestPanel.cs
Assets/Scripts/Game/UI/Management/QuestSlotView.cs
```

Prefab：

```text
Assets/Arts/UI/Panels/Quest/QuestPanel.prefab
Assets/Arts/UI/Panels/Quest/QuestSlot.prefab
Assets/Arts/UI/Panels/Story/StoryPanel.prefab
```

## Quest 状态

```csharp
public enum QuestState
{
    Locked = 0,
    Available = 1,
    Accepted = 2,
    Completed = 3,
    Claimed = 4,
}
```

显示规则：

```text
Locked 隐藏
Event-only Available 隐藏
Accepted 显示
Completed 显示
Claimed 领奖后隐藏
```

## QuestData

使用 `QuestData`，不要新增 `QuestRuntimeData` 作为同义概念。

```csharp
public sealed class QuestData
{
    public int QuestId;
    public QuestState State;
    public QuestObjectiveData[] Objectives;
}
```

只有事件型目标需要保存进度。查询型目标从当前系统实时计算。

## 接取方式

```csharp
public enum QuestAcceptMode
{
    Auto = 0,
    Manual = 1,
    Event = 2,
}
```

建议：

```text
Auto    前置满足后自动接取
Manual  UI / NPC / 任务板接取
Event   剧情 / 对话 / 区域 / 自定义事件触发
```

开局链路见 `Docs/Product/ProgressionAndQuests.md`。

## 目标类型

```csharp
public enum QuestObjectiveType
{
    None = 0,
    ItemCount = 1,
    ItemGainCount = 2,
    ItemUseCount = 3,
    Blueprint = 4,
    BuildBuilding = 5,
    BuildBuildingType = 6,
    UpgradeBuilding = 7,
    FarmCount = 8,
    PlantCrop = 9,
    HarvestCrop = 10,
    TechResearched = 11,
    TalkNpc = 12,
    EnterArea = 13,
    CustomFlag = 14,
}
```

查询型目标：

```text
ItemCount
BuildBuilding
BuildBuildingType
FarmCount
TechResearched
```

事件型目标：

```text
ItemGainCount
ItemUseCount
Blueprint
UpgradeBuilding
PlantCrop
HarvestCrop
TalkNpc
EnterArea
CustomFlag
```

## QuestEvent

统一入口示例：

```csharp
QuestManager.Instance.StartQuest(questId);
QuestManager.Instance.NotifyEvent(QuestEventType.CustomFlag, flagId);
QuestManager.Instance.NotifyEvent(QuestEventType.TalkNpc, npcId);
ItemManager.Instance.NotifyUseCompleted(itemId, count);
QuestManager.Instance.NotifyEvent(QuestEventType.GainItem, itemId, count);
QuestManager.Instance.NotifyEvent(QuestEventType.BlueprintCompleted, blueprintId);
```

`UseItem` 是完成通知，不是物品效果处理入口。UI 点击、工具选择、种子选择、进入目标模式都不能直接累计；只有效果或对应业务动作提交成功后才调用 `ItemManager.NotifyUseCompleted`。

不要设计“开始下一个任务”作为核心 API。任务顺序由配置的 `preQuestIds` 和触发条件控制。

## Blueprint

`Blueprint` 有两个含义：

```text
制作 / 生产定义
任务目标类型，表示完成某个蓝图
```

当前 `BlueprintManager` 支持：

```text
Inputs
Outputs
BuildingId
UnlockTechId
UnlockQuestId
CanComplete
TryComplete
完成后发送 QuestEventType.BlueprintCompleted
```

开局基础工具蓝图：

```text
30600001 Stone Axe     buildingId = 0
30600002 Stone Pickaxe buildingId = 0
```

`buildingId = 0` 表示不需要建筑。当前用于开局教学，QuestPanel 详情按钮会在任务存在未完成 Blueprint 目标时显示“制作”，点击后直接调用 `BlueprintManager.TryComplete`。建筑生产蓝图仍通过 `WorldBuildingDetailPanel` 的制作按钮完成。

## Quest 奖励

任务表字段：

```text
quest.rewardGroupId -> reward.groupId
```

领取流程：

```text
QuestManager.TryClaim
  -> RewardResolver.GetRewardGroup
  -> BagManager.TryAddItems
  -> 成功后状态改为 Claimed
```

规则：

- `rewardGroupId <= 0` 表示没有奖励，可以直接领取完成。
- `rewardGroupId > 0` 但奖励组为空时，领取失败，不改 `Claimed`。
- 背包容量不足时，`BagManager.TryAddItems` 返回失败，不改 `Claimed`。
- Quest UI 详情奖励行从 `reward` 读取，显示固定数量或 `min-max` 范围。

当前开局任务奖励：

```text
60000001 Scavenge the Shore      -> 60090001 -> Food x3
60000002 Build a House           -> 60090002 -> Wheat Seed x5
60000003 Craft a Basic Axe       -> 60090003 -> Food x2
60000004 Chop Down a Tree        -> 60090004 -> Food x2
60000005 Craft a Basic Pickaxe   -> 60090005 -> Food x2
60000006 Break Rocks             -> 60090006 -> Stone x2
60000007 Build a Workbench       -> 60090007 -> Plank x2
```

示例蓝图：

```text
30600001  Stone Axe       Wood x1 + Stone x1 -> Stone Axe x1
30600002  Stone Pickaxe   Wood x1 + Stone x1 -> Stone Pickaxe x1
30600003  Stone Hoe       Wood x2 + Stone x1 -> Stone Hoe x1
30601001  Saw Planks      Wood x5            -> Plank x1
30601002  Mill Wheat      Wheat x5           -> Food x1
30601003  Smelt Copper    CopperOre x5       -> CopperIngot x1
30601004  Smelt Iron      IronOre x5         -> IronIngot x1
```

当前开局任务链：

```text
开场 Story 10001
  -> 60000001 Scavenge the Shore
  -> 60000002 Build a House
  -> 60000003 Craft a Basic Axe
  -> 60000004 Chop Down a Tree
  -> 60000005 Craft a Basic Pickaxe
  -> 60000006 Break Rocks
  -> 60000007 Build a Workbench
```

地图对象对应：

```text
任务目标用 ItemGainCount / Blueprint / BuildBuildingType，不直接绑定地图对象实例。
地图编辑器放置 Resource 对象时，ConfigId 应填写 resource.id：

30300008 Branch      -> 拾取后获得 Wood
30300009 Loose Stone -> 拾取后获得 Stone
30300001 Tree        -> 斧采集后获得 Wood
30300002 Rock        -> 镐采集后获得 Stone

当前默认世界地图 Assets/Data/Map/1001.json 已放置开局资源：
Branch x3、Loose Stone x2、Tree x1、Rock x1 在地图中心附近。
```

## Story 衔接

当前 Story 是简单文字剧情系统，不是视频系统或复杂 cutscene 系统。更完整的 Story 实现细节见 `Docs/Modules/Story.md`。

流程：

```text
StoryManager 加载 StoryConfig
StoryPresenter 打开 StoryPanel
StoryPanel 显示几行文字
玩家点击继续
Story 完成
Story 完成后可通知 QuestManager
```

当前正式开场 Story：

```text
Id: 10001
Title: Ashore
TriggerMode: AutoOnNewGame
完成事件: QuestEventType.CustomFlag + 10001
```

## Quest UI

```text
Assets/Arts/UI/Panels/Quest/QuestPanel.prefab
Assets/Arts/UI/Panels/Quest/QuestSlot.prefab
Assets/Scripts/Game/UI/Management/QuestPanel.cs
Assets/Scripts/Game/UI/Management/QuestSlotView.cs
```

行为：

```text
左侧显示当前可见任务
点击左侧 slot，右侧显示任务详情
Track 按钮根据是否当前追踪改变文字
右侧目标行显示图标和数量，不显示目标类型名
右侧按钮根据任务状态改变文字
领奖后任务从列表隐藏
```

注意：`QuestSlotView` 不应在运行时强改 slot 子节点布局。Prefab 布局、`LayoutElement`、`VerticalLayoutGroup` 负责尺寸和排列；代码只绑定数据、进度、选中态和按钮回调。

## 当前状态

已完成：

```text
Quest 多目标 snapshot
Quest state / save / event / message
Quest 前置任务判断
Quest 追踪任务
Quest 完成和领取状态
Quest 完成消息与 Toast 监听器
Story 简单文字流程
Story 完成触发 QuestEvent
Blueprint 完成触发 QuestEvent
QuestPanel 左列表右详情
QuestSlot 点击切换详情
Track / Tracking 文本状态
右侧按钮状态文本
Claimed 任务隐藏
Quest 领取奖励发放到 BagManager / ItemManager
Quest UI 详情奖励行读取 reward
Quest / Blueprint / Story 正式 Luban 表读取
```

仍需完善：

```text
任务配置编辑规范
更多目标类型接入具体玩法
Quest / Blueprint / Story 的多语言内容继续补进 localization.xlsx
任务奖励 Toast / 获得反馈继续打磨
```








