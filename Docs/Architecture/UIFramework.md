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

从产品结构看，Sound、Language、Save 更像 Menu 内部子页面。后续如果要整理，优先改成 Menu 内部节点，由 Menu 脚本统一管理；GM 如果仍是调试大面板，可以保留独立 Panel。








