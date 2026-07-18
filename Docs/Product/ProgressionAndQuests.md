# Progression And Quests

本文记录剧情、任务和制作在产品流程中的位置。Story 实现细节见 `Docs/Modules/Story.md`，Quest / Blueprint 实现细节见 `Docs/Modules/QuestStoryBlueprint.md`。

## 推荐开局链路

```text
玩家新开局
  -> StoryManager 自动开始开场 Story
  -> 玩家点击继续
  -> Story 完成
  -> StoryManager 发送 QuestEventType.CustomFlag + StoryFlags.OpeningFinished
  -> QuestManager 接取匹配配置的第一个任务
  -> 玩家拾取开局散落资源
  -> BlueprintManager 完成制作
  -> QuestManager 更新任务
```

第一个正式任务建议由开场剧情结束触发，而不是直接监听建造 House。

当前第一版开局链路：

```text
60000001 Scavenge the Shore      ItemGainCount Wood 6 + Stone 4
60000002 Build a House           BuildBuildingType House
60000003 Craft a Basic Axe       Blueprint 30600001
60000004 Chop Down a Tree        ItemGainCount Wood 6
60000005 Craft a Basic Pickaxe   Blueprint 30600002
60000006 Break Rocks             ItemGainCount Stone 4
60000007 Build a Workbench       BuildBuildingType Workbench
```

开局地图对应：

```text
Assets/Data/Map/1001.json 是 GameEntry 当前默认世界地图。
地图对象使用 MapObjectData.ObjectType = Resource + ConfigId = resource.id。
30300008 Branch      -> pickupRewardGroupId 30902015 -> Wood
30300009 Loose Stone -> pickupRewardGroupId 30902016 -> Stone
30300001 Tree        -> gatherConfigId 30400001 -> Wood，需要斧
30300002 Rock        -> gatherConfigId 30400002 -> Stone，需要镐
```

`ItemGainCount` 统计的是获得物品事件，不绑定地图对象实例。只要地图编辑器放的是对应 `resource.id`，任务就能推进。

开局工具制作：

```text
30600001 Stone Axe     buildingId = 0，不需要 Workbench
30600002 Stone Pickaxe buildingId = 0，不需要 Workbench
```

这类 `buildingId = 0` 的基础工具用于开局教学，直接在 QuestPanel 详情按钮点击“制作”完成。后续需要建筑参与的生产蓝图，再通过对应建筑详情面板制作。

## 任务顺序

不要设计通用的“开始下一个任务”作为核心 API。任务顺序由配置的 `preQuestIds` 和触发条件控制。

## 奖励

任务奖励当前通过 `quest.rewardGroupId` 指向 `reward.groupId`。

领取流程：

```text
QuestManager.TryClaim
  -> RewardResolver.GetRewardGroup
  -> BagManager.TryAddItems
  -> 成功后改为 Claimed
```

当前规则：

- 没有奖励组的任务可以直接领取。
- 配了奖励组但找不到奖励，领取失败。
- 背包容量不足时领取失败，不会提前改为 `Claimed`。

后续仍需继续打磨：

```text
Toast / UI 反馈
奖励配置规范
是否需要任务奖励专用弹窗
```








