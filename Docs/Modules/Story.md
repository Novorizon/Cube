# Story Module

本文记录 Story 模块当前实现。只改剧情播放、剧情触发、剧情存档或 StoryPanel 时，优先读本文。

## 定位

Story 当前是简单文字剧情系统，不是视频系统，也不是复杂 cutscene 系统。

主要用途：

```text
开场漂流到岛上
教程步骤
短剧情过渡
触发第一个任务
Quest 完成后的后续剧情
```

## 当前文件

```text
Assets/Scripts/Game/Story/StoryManager.cs
Assets/Scripts/Game/Story/StoryPresenter.cs
Assets/Scripts/Game/Story/StoryPanel.cs
Assets/Scripts/Game/Story/StoryConfig.cs
Assets/Scripts/Game/Story/StoryEnums.cs
Assets/Scripts/Game/Story/StoryFlags.cs
```

占位文件：

```text
Assets/Scripts/Game/Story/DialogManager.cs
Assets/Scripts/Game/Story/EventTriggerSystem.cs
```

`DialogManager` 和 `EventTriggerSystem` 当前基本为空，不要误认为已有完整对话或事件触发系统。

## Prefab

```text
Assets/Arts/UI/Panels/Story/StoryPanel.prefab
```

`StoryPanel.PrefabPath` 指向这个 prefab。

当前 `StoryPanel` 会在 `BuildIfNeeded()` 里运行时创建遮罩、卡片、标题、正文、进度和 Continue 按钮。也就是说，当前不要假设 prefab 内已经有 `Title`、`Body`、`ContinueButton` 等固定节点。

后续 UI 打磨时更合理的方向是把 StoryPanel 需要的节点做进 prefab，并用序列化字段绑定；这样更符合经营 UI 的整体规则，也方便美术和布局调整。

## 数据表

正式表：

```text
Data/Defines/story.xml
Data/Defines/story_line.xml
Data/Excel/story.xlsx
Data/Excel/story_line.xlsx
Assets/Data/Bin/tbstory.bytes
Assets/Data/Bin/tbstoryline.bytes
Assets/Data/Json/tbstory.json
Assets/Data/Json/tbstoryline.json
```

运行时代码读取：

```text
DataManager.Instance.Story
DataManager.Instance.StoryLine
```

`story.xlsx` 字段：

```text
id
title
triggerMode
triggerTargetId
completeQuestEventType
completeQuestTargetId
nextStoryId
repeatable
enable
```

`story_line.xlsx` 字段：

```text
id
storyId
lineIndex
text
enable
```

Story lines 会按 `lineIndex` 排序；如果 `lineIndex` 一样，再按 `id` 排序。

## 如何配表

新增一段剧情通常改两个表：

1. 在 `Data/Excel/story.xlsx` 新增一行 Story 主配置。
2. 在 `Data/Excel/story_line.xlsx` 新增多行文本。
3. 运行 `Data/gen_all.bat` 或 Unity 菜单 `Luban/Update All`。
4. 检查生成结果：

```text
Assets/Scripts/Game/Data/Generated/StoryTableConfig.cs
Assets/Scripts/Game/Data/Generated/StoryLineTableConfig.cs
Assets/Data/Bin/tbstory.bytes
Assets/Data/Bin/tbstoryline.bytes
Assets/Data/Json/tbstory.json
Assets/Data/Json/tbstoryline.json
```

`story.xlsx` 配法：

```text
id                      StoryId，唯一
title                   标题，目前直接显示文本
triggerMode             StoryTriggerMode 整数
triggerTargetId         触发目标；AutoOnNewGame 可填 0
completeQuestEventType  Story 完成后通知 Quest 的 QuestEventType 整数；不通知填 0
completeQuestTargetId   QuestEvent 的 targetId；不通知填 0
nextStoryId             播完后自动接下一段 Story；没有填 0
repeatable              是否可重复播放
enable                  是否启用
```

`story_line.xlsx` 配法：

```text
id          行 Id，唯一；当前示例用 1000101 / 1000102 这种格式
storyId     所属 StoryId
lineIndex   行顺序，从 1 开始
text        当前行文本，目前直接显示文本
enable      是否启用
```

当前开场示例：

```text
story.xlsx:
10001, Ashore, AutoOnNewGame, 0, CustomFlag, 10001, 0, false, true

story_line.xlsx:
1000101, 10001, 1, You wake to the sound of waves., true
1000102, 10001, 2, The ship is gone. Broken planks are scattered across the beach., true
1000103, 10001, 3, Gather wood and stone. You need shelter before night., true
```

表里实际填写的是整数，不是枚举名。

## 触发方式

```csharp
public enum StoryTriggerMode
{
    Manual = 0,
    AutoOnNewGame = 1,
    QuestCompleted = 2,
    CustomFlag = 3,
    EnterArea = 4,
    TalkNpc = 5,
}
```

当前已接入：

```text
AutoOnNewGame：GameEntry 在 LoadWorldMap 成功后调用 TryStartAutoStories()
QuestCompleted：StoryManager 订阅 QuestCompletedMessage 后触发
Manual：可通过 StoryManager.TryStart(storyId) 手动开始
```

`NotifyEvent(triggerMode, targetId)` 可以支持 CustomFlag、EnterArea、TalkNpc 等事件型触发，但具体业务触发点需要调用它。

StoryTriggerMode 数值：

```text
0 Manual
1 AutoOnNewGame
2 QuestCompleted
3 CustomFlag
4 EnterArea
5 TalkNpc
```

QuestEventType 常用数值：

```text
0 None
1 StartQuest
2 CustomFlag
3 TalkNpc
4 EnterArea
5 UseItem
6 GainItem
7 BlueprintCompleted
8 BuildBuilding
9 UpgradeBuilding
10 PlantCrop
11 HarvestCrop
```

如果要让 Story 完成后触发任务接取，常见配置是：

```text
completeQuestEventType = 2
completeQuestTargetId = StoryFlags.OpeningFinished 或其它约定 flag id
```

## 播放流程

```text
StoryManager.LoadConfigs()
  -> 从 DataManager 读取 Story / StoryLine
  -> 构建 StoryConfig
  -> TryStartAutoStories / TryStart / NotifyEvent
  -> StoryPresenter.Present(...)
  -> UIManager.Instance.Panels.ShowAsync(StoryPanel.PrefabPath, StoryPanel.Args)
  -> StoryPanel 点击 Continue 逐行推进
  -> 最后一行点击 Continue 后关闭面板并回调完成
```

`StoryPresenter` 打开面板时使用：

```text
UseOutsideClickDetector = false
CacheOnClose = false
```

`StoryPanel.HideOnBack` 当前为 `false`，避免剧情被返回键直接关掉。

## 完成逻辑

Story 完成后：

```text
非 repeatable 的 StoryId 进入 completedStoryIds
currentStoryId 置 0
如果配置了 completeQuestEventType / completeQuestTargetId，通知 QuestManager
标记存档 dirty
如果 nextStoryId > 0，尝试继续播放下一段 Story
```

当前开场 Story：

```text
Id: 10001
Title: Ashore
TriggerMode: AutoOnNewGame
CompleteQuestEventType: CustomFlag
CompleteQuestTargetId: 10001
StoryFlags.OpeningFinished = 10001
```

这条链路用于：

```text
开场 Story 完成
  -> QuestManager.NotifyEvent(QuestEventType.CustomFlag, 10001)
  -> 匹配配置的第一个 Quest 可被接取
```

## 存档

```text
SaveData.Story
StorageManager.StoryData.CurrentStoryId
StorageManager.StoryData.CompletedStoryIds
```

`StoryManager.LoadSaveData()` 会恢复当前 StoryId 和已完成 StoryId。`CreateSaveData()` 会把已完成列表排序后写入存档。

注意：当前保存 `CurrentStoryId`，但读档后不会自动恢复到剧情面板中继续播放当前行；它只会阻止其它 Story 在 `currentStoryId != 0` 时开始。后续如果要支持剧情中途读档续播，需要补充当前行索引和恢复播放逻辑。

## 当前缺口

```text
StoryPanel 仍是运行时构建 UI，后续应改为 prefab 节点 + 序列化字段
Story 文本当前直接来自 story_line 表，还没有明确本地化 key 方案
DialogManager / EventTriggerSystem 只是占位
EnterArea / TalkNpc / CustomFlag 的具体业务触发点还需要逐步接入
剧情中途读档续播未完成
更复杂的头像、角色名、选项、分支、镜头控制都不属于当前实现
```

## 工具和相关文件

已有工具：

```text
Data/gen_all.bat
Data/gen_client.bat
Assets/Scripts/Tools/Editor/LubanTool.cs
```

推荐优先用：

```text
Data/gen_all.bat
```

Unity 菜单：

```text
Luban/Update All
```

`Luban/Update All` 会先根据 Excel 前三行更新 `Data/Defines/*.xml`，再执行 `Data/gen_all.bat` 并刷新 AssetDatabase。它适合新增字段或调整字段时使用。

如果只是改 Story 内容、增删 Story 行或 StoryLine 行，不改字段结构，通常直接跑 `Data/gen_all.bat` 即可。

注意：

```text
Data/Defines/__root__.xml 已 include story.xml 和 story_line.xml
Assets/Scripts/Game/Data/DataManager.cs 已注册 Story 和 StoryLine
WorldConfigValidator 当前校验的是世界建筑 / 科技配置，不覆盖 Story
当前没有专门的 Story 表编辑器或 Story 配表校验器
```







