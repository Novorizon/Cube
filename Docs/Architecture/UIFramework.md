# UI Framework

本文记录 UI 框架当前约定。只改业务 UI 时，通常还需要读 `Docs/Modules/ManagementUI.md`；只改框架时优先读本文。

## UI 类型

Page
: 全屏大流程，例如登录、主菜单、经营主界面、塔防主界面、加载流程。当前 `WorldMainPanel` 概念上接近经营主 Page，但短期不为命名重构。

Panel
: 当前 Page 上的浮动功能面板，例如 Build、ToolKit、Production、Quest、TechTree、Menu。当前 Bag 是 BottomBar 内部抽屉节点，不属于框架 Panel。

Popup
: 必须先处理的小确认或详情层，例如 Confirm、ItemDetail、TechUnlock。`TechUnlockPanel` 后续更适合迁到 Popup，使用 `PopupManager` blocker，不再自己创建遮罩。

Overlay
: 全局 Loading、转场黑屏、网络等待、强制输入阻塞。不要用业务 panel 反复自造全屏遮罩替代 Overlay。

Toast
: 自动消失、不中断操作的小提示，例如保存成功、资源不足、获得物品、科技解锁成功。Sound、Language、Save 这类设置界面不是 Toast。

Tooltip
: 鼠标停留一段时间后显示的上下文说明，例如物品名称、工具描述、技能属性和按钮用途。Tooltip 不阻塞输入，不进入 Panel / Popup 关闭栈，也不等同于自动消失的 Toast。

## 条件失败提示

需要告诉玩家“为什么不能执行”的业务功能，统一返回
`Assets/Scripts/Game/Core/RequirementResult.cs`，再由
`Assets/Scripts/Game/UI/Toasts/Toast.cs` 中的 `RequirementToast.TryPass` 转成 Warning
Toast。

职责边界：

```text
RequirementResult
  只描述成功或失败
  携带稳定错误 Code、本地化 key、中英文 fallback 和格式参数

各业务 RequirementChecker
  拥有本业务规则
  检查不产生副作用
  入口 UI 和最终执行路径复用同一检查

RequirementToast
  只负责把失败结果显示为 Toast
  不认识农田、建造、科技等业务
```

调用方式：

```csharp
RequirementResult result = feature.TryExecute();
if (RequirementToast.TryPass(result))
{
    Refresh();
}
```

不要建立一个同时知道农田、建造、生产、科技全部规则的全局条件管理器。通用层只统一
结果结构和展示方式；每个模块继续使用自己的 `FarmRequirementChecker`、
`BuildingRequirementChecker` 等检查器。这样其它功能可以复用 Toast 协议，同时不会把
业务依赖集中成新的大型耦合类。

规则：

- UI 预检查用于尽早反馈，真正产生消耗或状态变化的业务方法必须再次检查并返回同一个
  `RequirementResult`。
- `Code` 用于日志、测试或后续埋点，玩家文本使用 `LocalizationKey`，不要根据显示文本
  判断错误类型。
- 新提示应补入 `Data/Excel/localization.xlsx`；fallback 只保证配表尚未生成或 key 缺失
  时仍能显示可理解的信息。
- 如果禁用按钮会让玩家无法知道原因，可以保留点击能力并只做灰态表现，点击后显示具体
  条件 Toast；是否完全禁用仍由业务交互设计决定。
- 一次操作只显示当前最直接的一条失败原因；Toast 自身通过相同内容的 MergeKey 合并
  连续重复提示。

### Page 内嵌 Panel

一个全屏 Page 可以由多个职责独立的 `UIPanel` 组成。例如塔防采用：

```text
BattlePage : UIPage
  TopPanel : UIPanel
  BuildTowerPanel : UIPanel
  ItemPanel : UIPanel
  InfoPanel : UIPanel
  SkillPanel : UIPanel
  BattleControlPanel : UIPanel
  MiniMapPanel : UIPanel（当前禁用）
```

这些 Panel 与 Page 位于同一个 prefab，由 Page 通过 `UIEmbeddedPanelGroup` 显式传递 `Create / Open / Close` 生命周期，不注册为 `PanelManager` 的独立窗口。Panel 内负责按钮、条目和局部表现的叶子组件使用普通 `MonoBehaviour`。

内嵌 Panel 必须由序列化引用组成，不允许运行时使用 `GetComponentInChildren`、递归名称查找或遍历 Transform 自动发现。独立浮动 Panel 仍由 `PanelManager` 管理，两种所有权不要混用。

## 身份和路径

当前 UI 框架以 `prefabPath` 作为运行时身份，`Show`、`Hide`、`IsShown` 都按路径工作。

这不是明显性能问题：运行时主要是字典查找和少量字符串 key，比加载 prefab 和实例化 UI 的成本低很多。相比改成脚本类名，`prefabPath` 更能区分同脚本不同 prefab、不同皮肤、不同入口实例。后续如果需要更强管理能力，可以在业务层增加枚举或配置表映射到 `prefabPath`，不建议直接把框架身份改成脚本类名。

## 屏幕与安全区

屏幕适配的统一入口是：

```text
DeviceManager.Instance.Screen       通用设备 / 屏幕事实
UIManager.Instance.Viewport
```

`DeviceManager` 位于 `Assets/Scripts/Framework/Device`，启动时采集平台、设备类别、设备
型号、操作系统、处理器、内存和显卡概况，并持续维护 `DeviceScreenInfo`。只有游戏窗口
尺寸、显示器分辨率、安全区、方向、全屏模式或 DPI 发生变化时才触发 `ScreenChanged`。
不采集设备唯一标识。

`UIViewportService` 由 `UIManager` 持有，只订阅 `DeviceManager.ScreenChanged` 并把通用
屏幕信息转换为 UI 视口语义；UI 框架不再各自读取 `Screen`。当前快照
`UIViewportInfo` 提供：

```text
PixelSize
SafeAreaPixels
SafeAreaNormalized
SafeInsetsPixels
AspectRatio
Orientation
IsPortrait
```

通用安全区组件是 `UISafeAreaFitter`，位于
`Assets/Scripts/Framework/UI/Runtime/UI/Viewport`。原塔防专用安全区脚本已迁入这里并保留
原 `.meta` GUID，因此现有 prefab 引用不需要重绑。需要避开刘海、圆角或系统手势区域的
全屏容器可以直接挂该组件。当前经营 `WorldMainPanel` 与塔防 `BattlePage` 的根节点都已
使用该组件。

职责边界：

- 框架层只提供屏幕度量、变化通知、CanvasScaler 配置和通用安全区约束。
- 业务层决定 Compact / Normal / Wide 等布局语义，以及哪些内容允许换行、缩小或隐藏。
- `UIView` / `UIPanel` 不写具体响应式布局；需要监听时由具体视图或组合组件订阅
  `Viewport.Changed`。
- 普通分辨率适配优先使用 CanvasScaler、锚点、LayoutGroup、ContentSizeFitter 和 TMP
  preferred size，不为每个面板编写坐标调整脚本。

布局输入按以下顺序进入同一次布局求解，不让多个脚本反复改最终坐标：

```text
屏幕尺寸 / 方向 / 安全区
  -> 选择布局档位并确定根容器约束
  -> 写入本地化文本、字体和动态内容
  -> Unity LayoutGroup / TMP 完成一次重排
  -> 根据最终 Rect 定位 Tooltip、引导和其它浮层
```

仅切换语言时只更新内容并重排；仅改变分辨率或方向时只更新屏幕约束并重排。

## Tooltip

Tooltip 使用全局唯一显示层和唯一 `TooltipView`：

```text
UILayer.Panel    25
UILayer.Toast    30
UILayer.Tooltip  35
UILayer.Overlay  40
```

代码和 prefab：

```text
Assets/Scripts/Framework/UI/Runtime/UI/Tooltip
Assets/Arts/UI/Panels/Common/Tooltip.prefab
Assets/Scripts/Tools/Editor/TooltipPrefabBuilder.cs
```

职责：

```text
TooltipManager  延迟、所有权、取消、显示、隐藏和定位
TooltipTrigger  只监听 Pointer 事件，没有视觉节点，不带 UI 前缀
TooltipView     全局唯一的实际显示视图
TooltipData     一次显示需要的标题、描述、图标、Values 和 Footer
TooltipValue    一条结构化表现值，例如“伤害: 25”
```

业务调用入口：

```csharp
UIManager.Instance.Tooltips.Show(owner, anchor, dataProvider, options);
UIManager.Instance.Tooltips.Hide(owner);
```

### TooltipData 来源

每个 Tooltip 都必须有一个 `TooltipData` 来源，不存在“只有某种 UI 需要提供数据、其它 UI 自动拥有内容”的例外。区别只在于内容从哪里取得：

```text
固定按钮             TooltipTrigger 的序列化静态内容
动态塔 / 物品 / 技能  复用 View 按 TowerId / ItemId / SkillId 调用 Bind
已有 Pointer 处理组件 直接向 TooltipManager 传入 dataProvider
```

`Bind` 写在可复用的 View 类型中，不为每个运行时实例重复写一套。例如所有建塔卡片共用一次绑定：

```csharp
tooltipTrigger.Bind(CreateTooltipData);

private TooltipData CreateTooltipData()
{
    return new TooltipData
    {
        Title = LocalizedConfigText.TowerName(TowerId),
        Description = LocalizedConfigText.TowerDescription(TowerId),
        Icon = iconImage != null ? iconImage.sprite : null,
    };
}
```

这里的 `TowerId` 属于塔卡片业务，通用 `TooltipTrigger` 不应认识它。物品格和技能格同理，分别由自己的可复用 View 提供内容。写死 `"箭塔"` 的 Lambda 只能作为 API 示例，正式内容必须通过 `localization.xlsx` 和 `LocalizedConfigText` 获取。

规则：

- 每个目标不创建独立 Tooltip UI 层；所有目标共享一个 `TooltipView`。
- 普通按钮可以挂轻量 `TooltipTrigger`；已经实现 `IPointerEnterHandler` / `IPointerExitHandler` 的组件直接调用 `TooltipManager`。
- `TooltipTrigger` 不查询游戏业务数据；业务层通过 `Func<TooltipData>` 提供已经本地化的表现数据。
- Tooltip prefab 的稳定节点使用序列化字段绑定，不在运行时 `Find`。
- Tooltip 的 `CanvasGroup.blocksRaycasts` 和 `interactable` 必须为 `false`，避免出现后抢走源按钮射线并造成闪烁。
- 首次默认延迟 `0.55` 秒；刚查看过 Tooltip 时相邻目标使用 `0.08` 秒 reshow delay；warm window 默认 `0.75` 秒。
- 鼠标离开、点击、开始拖拽、目标禁用、Back、`UIManager.ClearAll`、资源加载器重建时隐藏或取消等待。
- Tooltip 默认锚定到目标旁边，按 Right / Left / Above / Below 自动选择空间并限制在 Canvas 内；它不跟随鼠标遮挡源目标。
- 阻塞型 Overlay 显示期间不打开 Tooltip，Overlay 层级始终高于 Tooltip。

## 关闭方式

框架关闭枚举：

```text
CloseButton
LeftOutside
RightOutside
Back
```

常见经营浮动面板推荐：

```csharp
UICloseTriggers.CloseButton |
UICloseTriggers.Back |
UICloseTriggers.RightOutside
```

规则：

- 桌面端右键 panel 外部区域关闭支持 `RightOutside` 的面板。
- 移动端没有右键，触摸或点击 panel 外部区域会由框架映射为可用的 outside 关闭原因。
- Panel outside close 统一由 `UIOutsideClickDetector` + `PanelManager` 处理，不再为 Panel 创建外部遮罩节点。
- `UIManager` 会统一创建透明的 `PanelOutsideBlocker`，它是 `UICanvasRoot` 的底层 UI，排序低于所有 UI layer。它只接没有命中其它 UI 的空白点击，不挡住任何正常 UI。
- `PanelOutsideBlocker` 只负责让空白处成为 UI raycast 命中和阻止世界输入；真正关闭哪个 panel、是否允许右键/触摸关闭，仍由 `UIOutsideClickDetector` + `PanelManager` 按 top outside target 和 `UICloseTriggers` 判断。
- outside close 会被 `UIManager` 标记为本帧 UI 指针消费；世界输入通过 `WorldPointerPicker.IsPointerOverUi()` 查询时必须把这类点击当作 UI 输入，不能同一帧再触发移动、建造、采集等场景操作。
- 业务 panel 内的 `Overlay` 只作为视觉遮罩，`Image.raycastTarget` 必须为 `false`，不要在 panel 内再接一套遮罩点击关闭。
- Quest、TechTree、Menu、Sound、Language、Save、GM 等大面板应支持空白或遮罩关闭。

## Exclusive Group

Exclusive 表示同级互斥，不保留返回历史。适合主界面入口：

```text
Build
ToolKit
Production
Quest
TechTree
Menu
```

API：

```csharp
RegisterExclusivePanel(groupId, prefabPath)
ShowExclusiveAsync(groupId, prefabPath)
HideExclusiveGroup(groupId)
```

一个 group 可以注册多个面板。项目也可以同时存在多个互斥 group，例如经营主界面浮动面板一组、某个编辑器工具局部面板一组。业务层负责决定 `groupId`，框架负责关闭同组其它面板。

BottomBar 内部 Bag 节点不注册 Exclusive group。`WorldMainPanel` 在业务层协调 Bag 与独立浮动面板的互斥：打开一方时关闭另一方。

## Stack Group

Stack 表示 panel 级返回栈。Push 新 panel 时隐藏旧 top，Pop 当前 top 时恢复上一个 top。适合真正独立 prefab、独立生命周期、需要返回历史的流程。

API：

```csharp
PushStackAsync(groupId, prefabPath)
PopStack(groupId)
HideStack(groupId)
```

`PanelOptions.GroupId` 是旧兼容入口，语义按 Stack 处理。新代码优先写 `StackGroupId` 或直接调用 Stack API。

Stack 和 Exclusive 不冲突：Stack 解决“返回上一个 panel”，Exclusive 解决“同级只能开一个”。同一个 panel 是否同时参与两种管理，要由业务明确设计，避免用户看见一个入口组和一个返回栈互相抢状态。

## Panel 内部子节点

框架当前管理的是 `UIPanel` / prefab，不全局管理不带脚本的普通子节点。

如果 Sound、Language、Save 只是 Menu 内部设置页，更合理做法是：

```text
MenuPanel
  Sound node
  Language node
  Save node
  GM node
```

由 `MenuPanel` 通过序列化字段切换 `SetActive`，或在本地维护一个 node stack。不要为了使用框架 Stack 强行把普通子节点拆成多个 `UIPanel`。

当前 Bag 采用同一规则：

```text
WorldMainPanel
  BottomBar
    HotBarGrid
    BagPanel node
```

`WorldBottomBarPanel` 负责 Bag 节点的开关、消息订阅和 HotBar/Bag 共享拖拽控制器；`WorldBagPanel` 只是该节点的本地控制组件。

如果多个 panel 都反复需要“内部节点互斥”或“内部节点返回栈”，再考虑抽 `UINodeExclusive` / `UINodeStack` 这类轻量组件。

## 业务层配置位置

框架提供能力，业务层配置分组。

经营主界面的独立 Panel 入口当前由 `WorldPanelEntryController` 注册 entry：

```text
id
groupId
prefabPath
入口开关状态刷新回调（可选）
```

Bag 入口不在该表中，由 `WorldBottomBarPanel` 本地处理。

节点位置优先在 prefab 里调；需要运行时对齐时使用业务自己的布局工具和序列化引用，不写死某个分辨率像素。

## 当前系统设置

当前代码结构中 Menu、Sound、Language、Save、GM 是独立 `UIPanel`，短期可以走 `WorldMenuPanel.SettingsStackGroupId` 的 Stack。

Sound、Language、Save 子面板各自只绑定一个 prefab 序列化的 `Return` 按钮；点击后优先 `PopStack(SettingsStackGroupId)` 返回设置菜单，栈不存在时才隐藏当前面板。不要同时保留 `Close` 与 `Return` 两套有效监听。

`SoundPanel` 直接使用 prefab 序列化的 `Slider` 组件引用，不在运行时按名称查找或创建控件；因此 Slider 及其子节点允许改名和调整层级。

声音开关使用独立的 `On / Off` Button 状态：点击 On 记录当前音量后静音，点击 Off 恢复该音量；如果由 Slider 或增减按钮把音量调到 0，Off 默认恢复到 50%。

从产品结构看，Sound、Language、Save 更像 Menu 内部子页面。后续如果要整理，优先改成 Menu 内部节点，由 Menu 脚本统一管理；GM 如果仍是调试大面板，可以保留独立 Panel。








