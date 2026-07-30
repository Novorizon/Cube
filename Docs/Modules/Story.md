# Story Module

本文记录当前 Story 与轻量新手引导实现。修改剧情播放、静态插画镜头、按钮高亮、剧情存档或 StoryPanel 时，优先读本文。

## 定位

Story 是基于 `StoryStep` 的轻量剧情表现系统，支持文字、静态插画、文字与插画混合，以及简单按钮指引。它不是视频播放器，也不是带时间轴、角色走位、分支选项的完整 Cutscene/Dialog 系统。

```text
StoryManager：加载配置、触发、进度和存档
StoryPresenter：打开 StoryPanel
StoryPanel：按 Step 切换文字、插画、Guide 表现
StoryMotionPlayer：静态插画的平移与缩放
SimpleGuideManager：等待并绑定 GuideTarget
GuideOverlay：遮罩、镂空区域、焦点框和提示文本
GuideTargetRegistry：查找当前可见的按钮目标
```

## 文件

```text
Assets/Scripts/Game/Story/StoryManager.cs
Assets/Scripts/Game/Story/StoryPresenter.cs
Assets/Scripts/Game/Story/StoryPanel.cs
Assets/Scripts/Game/Story/StoryConfig.cs
Assets/Scripts/Game/Story/StoryStep.cs
Assets/Scripts/Game/Story/StoryEnums.cs
Assets/Scripts/Game/Story/StoryMotionPlayer.cs
Assets/Scripts/Game/Story/SimpleGuideManager.cs
Assets/Scripts/Game/Story/GuideOverlay.cs
Assets/Scripts/Game/Story/GuideTarget.cs
Assets/Scripts/Game/Story/GuideTargetRegistry.cs
Assets/Scripts/Tools/Editor/StoryPanelPrefabBuilder.cs
```

`DialogManager` 和 `EventTriggerSystem` 仍是占位文件，不表示已经有完整对话树或通用事件编排系统。

## 数据表

```text
Data/Defines/story.xml
Data/Defines/story_step.xml
Data/Excel/story.xlsx
Data/Excel/story_step.xlsx
Assets/Data/Bin/tbstory.bytes
Assets/Data/Bin/tbstorystep.bytes
Assets/Data/Json/tbstory.json
Assets/Data/Json/tbstorystep.json
```

运行时入口：

```text
DataManager.Instance.Story
DataManager.Instance.StoryStep
```

`story_step.xlsx` 字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | int | Step 唯一 Id。 |
| `storyId` | int | 所属 Story Id。 |
| `stepIndex` | int | Story 内顺序；相同时按 `id`。 |
| `stepType` | int | `StoryStepType`。 |
| `text` | string | Text/Mixed 的正文。 |
| `illustrationPath` | string | Illustration/Mixed 使用的 Texture2D 资源路径。 |
| `motionPreset` | int | `StoryMotionPreset`。 |
| `motionDuration` | float | 镜头运动秒数，使用不受暂停影响的时间。 |
| `advanceMode` | int | `StoryAdvanceMode`。 |
| `autoAdvanceDelay` | float | AutoAfterDelay 等待秒数。 |
| `guideTargetId` | string | Guide 要高亮的稳定目标 Id。 |
| `guideText` | string | Guide 提示语。 |
| `allowTargetInteraction` | bool | 是否允许点击镂空区域下方的真实 UI。 |
| `enable` | bool | 是否启用。 |

## Step 类型

```text
0 Text          只显示现有文本剧情框
1 Illustration 只显示静态剧情插画
2 Mixed         插画与文本剧情框同时显示
3 Guide         高亮某个 GuideTarget，显示简单提示
```

文本框与静态插画不是二选一。不同 Step 可以使用不同类型，`Mixed` 也可在同一个 Step 中同时显示二者。

进度文本由 `GameConfig.Story.ProgressDisplayMode` 控制：

```text
Hidden        不显示进度
DialogueOnly 只统计 Text / Mixed；当前默认值
AllSteps      统计全部 Story Step
```

因此在“1 个纯插画 Step + 3 个 Mixed Step”的开场中，当前第一句文字显示为 `1/3`；纯插画 Step 不占对话序号。内部播放和存档仍使用统一的 `stepIndex`，不会产生两套剧情进度。

## 静态插画镜头

剧情插画使用 `Texture2D + RawImage`。`StoryMotionPlayer` 插值 `RawImage.uvRect`，改变图片的可见区域来模拟虚拟镜头，不修改真实 Unity Camera，也不缩放 StoryPanel UI：

```text
0 None
1 ZoomOut          从局部慢慢拉远到完整画面
2 PanLeftToRight   从左侧缓慢移动到右侧
3 PanRightToLeft   从右侧缓慢移动到左侧
4 ZoomIn           从全图缓慢推进到局部
```

插画保持 Unity `Default Texture`，把资源路径填入 `illustrationPath`。Story 目录必须被 YooAsset 收集；当前规则包含 `Assets/Arts/UI/Texture/Story`。AI 生成的静态图可以直接进入这条流程，镜头运动由 Unity 完成，不需要为每个镜头重新生成图片。

ZoomOut、ZoomIn 和横向平移的默认取景范围集中在 `GameConfig.Story`，无需到 `StoryMotionPlayer` 内寻找散落数值。

## 推进方式

```text
0 Click               点击 Continue
1 MotionComplete      插画运动完成后进入下一 Step
2 AutoAfterDelay      等待 autoAdvanceDelay 后进入下一 Step
3 GuideTargetClicked  玩家点击高亮的真实 UI 后进入下一 Step
```

GuideTargetClicked 会自动允许目标交互，避免配置成不可点击后卡住。

## 新手引导

新手引导不另建复杂流程图系统，由 Story 的 `Guide` Step 负责流程；Guide 子系统只负责“找到目标、遮罩、高亮和监听点击”。

当前经营模式底栏会在运行时注册：

```text
world.bottomBar.bag
world.bottomBar.build
world.bottomBar.toolKit
world.bottomBar.tech
world.hotBar.slot.1 ... world.hotBar.slot.N
```

其他按钮需要指引时，在按钮 GameObject 上添加 `GuideTarget` 并配置稳定 Id，或在初始化代码中调用：

```csharp
GuideTarget.Attach(button.gameObject, "module.panel.action");
```

GuideOverlay 使用四块遮罩围出镂空区域；目标允许交互时，点击会穿过镂空区域到真实按钮。目标尚未出现时，SimpleGuideManager 会等待注册，而不是立即失败。

## 配表示例

现有开场三行已迁移为 Text Step：

```text
1000100,10001,0,Illustration,"",...,ZoomOut,5,AutoAfterDelay,6,...
1000101,10001,1,Mixed,"You wake to the sound of waves.",...,Click,...
1000102,10001,2,Mixed,"The ship is gone...",...,Click,...
1000103,10001,3,Mixed,"Gather wood and stone...",...,Click,...
```

插画拉远并在运动结束后推进：

```text
stepType=Illustration
illustrationPath=Assets/Arts/UI/Texture/Story/chapter1/4a9f3a69-3782-4806-81de-860315f46645.png
motionPreset=ZoomOut
motionDuration=5
advanceMode=AutoAfterDelay
autoAdvanceDelay=6
```

插画加文字、由玩家点击推进：

```text
stepType=Mixed
text=The island finally comes into view.
illustrationPath=Assets/Arts/UI/Texture/Story/chapter1/4a9f3a69-3782-4806-81de-860315f46645.png
motionPreset=PanLeftToRight
motionDuration=5
advanceMode=Click
```

简单指示建造按钮：

```text
stepType=Guide
advanceMode=GuideTargetClicked
guideTargetId=world.bottomBar.build
guideText=Open the build menu.
allowTargetInteraction=true
```

表内填写的是枚举整数，不是枚举名。

## 触发与完成

`StoryTriggerMode` 保持不变：Manual、AutoOnNewGame、QuestCompleted、CustomFlag、EnterArea、TalkNpc。

```text
StoryManager.LoadConfigs()
  -> 读取 Story / StoryStep
  -> 按 stepIndex、id 排序
  -> TryStartAutoStories / TryStart / NotifyEvent
  -> StoryPresenter.Present
  -> StoryPanel 按 Step 表现和推进
  -> 最后一 Step 完成
  -> 写入 completedStoryIds、通知 Quest、尝试 nextStoryId
```

## 存档

存档类型名称是现有的 `StorageManager.StoryData`，不要创建带 `Save` 前缀的替代类型。

```text
StoryData.CurrentStoryId
StoryData.CurrentStepIndex
StoryData.CompletedStoryIds
```

Step 改变时会标记存档 dirty。读档后，`TryStartAutoStories()` 会先调用 `TryResumeCurrentStory()`，从 `CurrentStepIndex` 重新打开未完成剧情；旧存档没有该字段时默认从第 0 个 Step 开始。

## Prefab

```text
Assets/Arts/UI/Panels/Story/StoryPanel.prefab
```

Prefab 使用序列化字段绑定插画视口、文本卡片、Continue 按钮和 GuideOverlay，不再依赖 StoryPanel 在运行时临时创建整套节点。

重新生成 prefab：

```text
Unity 菜单：Tools/Story/Rebuild Story Panel Prefab
批处理：StoryPanelPrefabBuilder.BuildForBatch
```

生成器是初始结构和绑定工具。美术调整 prefab 后，不要无故重新运行生成器覆盖布局。

## 生成配置

修改表后执行：

```text
Data/gen_all.bat
```

字段结构发生变化时，也可以使用 Unity 菜单 `Luban/Update All`。生成后应检查 `StoryStepTableConfig`、`TbStoryStep`、`tbstorystep.bytes` 与 `tbstorystep.json`。

## 当前边界

```text
没有分支选择、条件跳转和对话树
没有角色立绘槽位、口型、配音时间轴
没有 Timeline/Cinemachine 场景演出编排
插画镜头当前使用预设，不是任意关键帧曲线
文本与 Guide 提示尚未切换为本地化 key
```

这些能力以后可以继续增加新的 Step 类型或演出播放器，不需要把轻量 Guide 扩展成独立复杂剧情系统。
