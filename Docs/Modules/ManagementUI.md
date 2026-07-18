# Management UI

本文记录经营主界面和浮动面板。框架能力见 `Docs/Architecture/UIFramework.md`。

## 常驻 HUD

常驻 HUD 在 `WorldMainPanel`：

```text
TopBar
BottomBar
RightBar
MiniMap
LeftBar / entry buttons
```

当前子模块：

```text
WorldTopBarPanel.cs
WorldBottomBarPanel.cs
WorldEntryBarPanel.cs
WorldRightBarPanel.cs
WorldBuildingDetailPanel.cs
WorldBagPanel.cs（BottomBar 内部 Bag 抽屉）
```

规则：

- 这些子模块都是挂在 prefab 上的 `MonoBehaviour`。
- `WorldMainPanel` 通过 `[SerializeField]` 引用，不在代码里 `new World*Panel()`。
- 子模块内部按钮、文本、图标、slot 用 prefab 序列化字段绑定。
- 不运行时 `Find("TopBar")`、`Find("Resources")`、`Find("HotPanel")`、`Find("Bag")` 等节点。
- `WorldTopBarView` 已合并回 `WorldTopBarPanel`，后续不要再新增同类只转发的 View 包装层。
- `RightBar/BattleEntry` 由 `WorldRightBarPanel` 绑定，点击后由 `WorldMainPanel` 调用 `MapManager.Instance.LoadBattleMap(30950001)` 进入当前默认塔防关卡；它是模式切换入口，不注册到 `ManagementFloating` 浮层组。

BagPanel prefab 当前作为 `BottomBar` 的子节点嵌在 `WorldMainPanel` 中。`WorldBagPanel` 是本地节点控制组件，不是独立 `UIPanel`。

## 独立浮动面板

```text
BuildPanel
ToolKitPanel
FarmPanel
ProductionPanel
QuestPanel
TechTreePanel
MenuPanel
```

Prefab 当前按模块放在：

```text
Assets/Arts/UI/Panels/Bag
Assets/Arts/UI/Panels/Build
Assets/Arts/UI/Panels/ToolKit
Assets/Arts/UI/Panels/Farm
Assets/Arts/UI/Panels/Production
Assets/Arts/UI/Panels/Quest
Assets/Arts/UI/Panels/TechTree
Assets/Arts/UI/Panels/Menu
```

## 入口互斥

入口统一注册到 `WorldPanelEntryController`：

```text
Build
ToolKit
Production
Quest
TechTree
Menu
```

这些独立 Panel 入口当前注册到同一个 `ManagementFloating` Exclusive group。Bag 入口由 `WorldBottomBarPanel` 本地处理，不注册 `WorldPanelEntry`。

行为：

- 点击已打开入口会关闭自己。
- 点击未打开入口会先关闭同组其它面板，再打开自己。
- 入口状态读取 `UIManager.Instance.Panels.IsShown(prefabPath)`，不要额外记 bool。
- 新增“等等”面板时注册新的 entry 和 group，不写点对点互相关闭逻辑。
- Panel outside close 不会拦截入口按钮射线。点击 Build / ToolKit / Quest / TechTree / Menu 入口时，按钮自身仍能进入同一套 toggle / Exclusive 逻辑。
- 打开 Bag 会先关闭 `ManagementFloating` 组、Farm 和 BuildingDetail；打开这些独立浮层也会关闭 Bag，以保持界面互斥。

## Bag Toggle

BottomBar 的 Bag 展开/收起统一由 Bag 根按钮触发。Bag 是 BottomBar 内部抽屉，通过节点 `SetActive` 开关。

规则：

- `Open` / `Close` 三角图标只显示状态，不接独立点击逻辑。
- `Open` / `Close` 图标的 `Image.raycastTarget` 必须为 `false`。
- `WorldBottomBarPanel.SetBagOpen()` 同时切换 Bag 节点和 Open / Close 图标，不通过 `UIManager.Panels.Show/Hide`。
- `WorldBagPanel` 只负责内部背包格和 Close 按钮；BagChanged 订阅、开关状态和共享拖拽控制器由 `WorldBottomBarPanel` 持有。
- 旧 Bag prefab 内如果仍有 HotBarGrid，由 `WorldBagPanel` 隐藏；实际快捷栏只使用 BottomBar 自身的一份节点。
- Build / ToolKit 当前没有 Open / Close 状态图标设计，不要给它们补图标状态切换。

## Bag / HotBar Slots

Bag 和 HotBar 共用 `BagManager` 的 slot：

```text
0-9   HotBar / 快捷栏
10+   Bag / 背包
```

规则：

- 快捷栏和背包 slot 可以互相拖动。
- BottomBar 持有唯一 `BagDragController`，只绑定自身 HotBar 与 Bag 子节点的 slots，因此拖拽不会跨到其它 Panel。
- 拖拽时显示跟随鼠标的物品图标和数量，源格图标暂时变淡；结束、关闭 Bag 或销毁 BottomBar 时必须清理拖拽状态。
- 拖到空格是移动，拖到已有物品格是交换。
- slot 只保存 `itemId`；物品数量由 `ItemManager` 统一保存。
- 点击 slot 走 `BagManager.TryUseSlot -> ItemManager.Use`；UI 不按物品类型分支。
- 工具点击只选择或装入 ToolKit，不立即累计 `UseItem` 任务进度。
- 未配置使用效果的物品返回失败，不扣数量，也不发送任务事件。
- 任务 `UseItem` 只在采集、开垦、种植或消耗效果真正完成后累计。
- 不拆堆；当前同一种物品只占一个 slot。

## 布局

布局规则：

```text
Build / ToolKit / Farm / Production：下边贴 BottomBar 快捷栏上边
Bag：作为 BottomBar 子节点，位置直接由 prefab 布局决定
TechTree：居中
QuestPanel：居中，大面板，左列表右详情
```

运行时对齐工具：

```text
Assets/Scripts/Game/UI/Management/WorldFloatingPanelLayout.cs
```

位置优先通过 prefab 节点和序列化引用调整，不写死单分辨率像素。

## TopBar

Prefab：

```text
Assets/Arts/UI/Panels/World/TopBar.prefab
```

绑定脚本：`WorldTopBarPanel`。日期、时间、天气、季节图、日夜图、菜单按钮都通过序列化字段引用。

显示规则：

```text
日期：第N年 / 季节 / M月D日
英文日期：Year N / Season / M/D
天气：天气 / 晴朗 22C
日夜圆图：12:00 为初始外观
季节横图：随年内进度平滑横移
```

## 系统设置

当前代码中 Menu、Sound、Language、Save、GM 是独立 `UIPanel`，短期可用 Stack 保持 Menu -> 子面板 -> 返回 Menu。

后续更推荐：

```text
MenuPanel
  Sound
  Language
  Save
  GM
```

如果 Sound、Language、Save 只是设置页，就作为 Menu 内部节点由 `WorldMenuPanel` 通过序列化字段切换。不要为了 Stack 强行拆 panel。

## 关闭规则

独立经营大面板默认支持：

```text
CloseButton
Back
RightOutside
```

Quest、TechTree、Menu、Sound、Language、Save、GM 等有空白或遮罩的大面板，右键外部区域应关闭。移动端点击外部区域即可，不要求右键。

经营 panel 的 outside close 统一走框架创建的 `PanelOutsideBlocker` + `UIOutsideClickDetector` + `PanelManager`。Panel prefab 内如果有半透明 `Overlay`，它只负责视觉变暗，`Image.raycastTarget` 必须关闭，脚本里不要再 `Find("Overlay")` 绑定点击关闭。

Bag 不是独立 Panel，不参与框架 outside / Back close；通过 BottomBar 的 Bag 按钮、Bag 内 Close 按钮，或打开其它互斥业务浮层时关闭。

`PanelOutsideBlocker` 是 `UICanvasRoot` 的底层 UI，排序低于所有 UI layer，不挡住任何正常 UI。它只让空白处命中 UI 并阻止世界输入，点击后仍由 `PanelManager` 关闭最上层可 outside close 的 panel。世界输入必须通过 `WorldPointerPicker.IsPointerOverUi()` 过滤，避免右键关闭 Quest / TechTree 等面板时同一帧又让玩家移动到鼠标位置。








