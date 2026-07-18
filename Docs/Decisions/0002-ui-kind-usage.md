# 0002 UI Kind Usage

## 结论

UI 类型按功能语义选择，不按当前文件名硬套。

```text
Page     全屏大流程
Panel    当前流程上的浮动功能面板
Popup    必须先处理的小确认或详情
Overlay  全局覆盖、阻塞或转场
Toast    自动消失的小提示
```

## 当前归类

Page：

```text
经营主界面
塔防主界面
登录 / 主菜单 / 加载
```

Panel：

```text
Build
ToolKit
Farm
Production
Quest
TechTree
Menu
```

Popup：

```text
Confirm
ItemDetail
TechUnlock
```

Overlay：

```text
Loading
转场黑屏
网络等待
强制输入阻塞
```

Toast：

```text
保存成功
资源不足
获得物品
科技解锁成功
```

Sound、Language、Save 不是 Toast。

Page 内部子节点：

```text
BottomBar / Bag
```

## Stack 与 Exclusive

Stack 用于 panel 级返回历史。Exclusive 用于同级互斥。两者不等价。

主界面 Build、ToolKit、Production、Quest、TechTree、Menu 使用 Exclusive。Bag 是 BottomBar 内部节点，由经营 UI 业务层在打开其它浮层时协调关闭，不注册框架 Exclusive。

Menu -> Sound -> 返回 Menu 这类，如果 Sound 是独立 `UIPanel`，可以短期使用 Stack。如果 Sound 只是 Menu 内部设置页，应该作为 Menu 内部子节点，由 Menu 脚本控制。

## 子节点

不要为了 Stack 强行把普通子节点拆成多个 `UIPanel`。如果多个 panel 都稳定需要内部节点栈或内部节点互斥，再抽轻量组件。








