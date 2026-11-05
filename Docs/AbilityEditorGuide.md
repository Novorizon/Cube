# Ability Studio 使用与验收

首版实现位于 Packages/com.novorizon.ability.editor.odin，使用真实 Odin PropertyTree 与自定义 JSON DTO Drawer。运行时没有新增 Sirenix 或 UnityEditor 引用。安装要求与常用操作见 [编辑器 README](../Packages/com.novorizon.ability.editor.odin/README.md)。

## 从英雄样例开始

1. 在 Unity 2022.3 项目安装 Ability Runtime 0.2.0、已有授权的 Odin Inspector 3.x 和本编辑器。
2. 从完整验证 ZIP 复制根 Data/AbilityJsonSources/Heroes；或选择项目自己的技能目录。不要把便携验证工具的 Unity/NUnit 替身放进 Assets。
3. 打开 **Tools / Ability / Skill Studio**。默认能发现仓库英雄目录，否则使用“选择技能库”。
4. 选择 lina 的龙破斩，展开每级参数、Special 和命中动作。修改一个等级的 damage，查看 JSON 预览，再运行“校验全部”。
5. 切换“试运行”，选择等级和目标数，点击“开始试运行”。检查伤害、事件和清理后对象数。
6. 保存文件，外部查看 JSON；Undo 回旧值应显示未保存，Redo 回保存点应恢复已保存。

工作台只保存公开 authoring DTO，不序列化运行时定义、当前战斗状态、Odin 会话或 Unity 对象引用。文件本身仍可通过生产 JsonAbilityDefinitionProvider 在没有编辑器的项目执行。

## 首版功能

| 区域 | 已实现 |
| --- | --- |
| 界面 | 深浅主题、三栏布局、文件/条目搜索、分类颜色、分组卡片、状态标签、JSON与引用视图 |
| 编写 | 新建文件和定义、条目复制/删除、动作增删重排、可空覆盖、常量/等级表、中文枚举和 flags、引用选择 |
| 校验 | 生产 schema 与合并图校验、立即创建环、跨文件引用、错误列表及定义跳转 |
| 试运行 | 独立适配器、手动触发、等级/目标/时间/预算、原始中止消息、事件记录、伤害/治疗及清理报告 |
| 文件 | Undo JSON 快照、语义脏标记、字节冲突检查、只读保护、安全替换、SessionState 草稿恢复 |

未实现完整依赖闭包复制、全工程重命名事务、节点图／时间轴、实际项目脚本沙盒适配器的 UI 注册、真实场景预览。名称变更保留原引用并显示错误，不执行隐式字符串替换。

## 静态与运行检测的验收

不能以引用对象数量直接预测调用深度或操作数。请用下面场景分别验证：

- A 的 OnCreated 添加 B、B 的 OnCreated 添加 A：静态发布错误，应包含创建环链路；保存拒绝。
- 合法的双向反伤事件配置、未排除反射标志：静态图可能有效，试运行应触发深度或操作预算，并保留实际 depth/operations 消息。
- 同一无环 AOE，先少目标、再多目标并降低操作预算：前者可通过、后者中止。界面不能把静态通过显示为所有运行场景都安全。
- 试运行时间短于前摇／弹道寿命：报告仍有未完成工作；不会宣称完整覆盖后续路径。
- 每种退出方式，包括配置错误、无技能、缺外部工厂、预算中止：清理后运行对象归零。
- 极长静态引用链：编辑器非递归预检应在生产图校验之前返回作者复杂度诊断；生产图校验本身也采用显式栈。默认 256 的作者预算按凝聚路径的保守上界计算，不等同于运行时 64 层；1000 个独立 Modifier 和普通小事件环仍可校验。

## 真实 Unity 交互验收（尚未运行）

以下为合并/发布前门槛，不是现有自动化已经完成的结果。

| 检查 | 操作与通过条件 |
| --- | --- |
| 导入边界 | 仅 Runtime 不需要 Odin；有 Runtime+Odin 时编辑器正常；移除 Odin 后入口提示可用且无编译错误；Player 无 Editor/Sirenix 泄漏 |
| 视觉 | 深/浅主题、1040×660及常用更大窗口；中文可读，标题/按钮不重叠，三栏与滚动正常，焦点和错误色清楚 |
| 无交互不变更 | 逐个打开全部英雄/验证 JSON，只展开折叠和切换选项卡；文件不回写，DTO不因null/default归一化而变脏 |
| 表单覆盖 | Hero与Validation数据各字段可见；枚举/flags输出整数；等级、动作顺序、缺省duration与显式0语义保持 |
| Undo | 普通字段、列表增删/重排、条目复制/删除、空条目修复均可撤销重做；Undo撤销新增条目后不出现无效选择或越界 |
| 生命周期 | 反复切文件/关窗/域重载/进入退出PlayMode；PropertyTree、事件订阅、Undo临时对象不持续增长，表单不空白 |
| 恢复 | 未保存时重载，先测磁盘不变，再测外部改值、仅改BOM、改成非法JSON/未来schema/只读；草稿不能被悄悄丢失或覆盖外部文件，可另存恢复副本 |
| 文件 | 只读、源被删除、SaveAs目标被抢占、引用文件外部变化、替换失败；原文与草稿保留；同内容重复导出字节稳定 |
| 项目隔离 | 打开窗口、导入和校验不操作场景实体；试运行不自动执行业务脚本；清理不影响实际战斗 |
| 规模 | 用代表性文件库测搜索、切换、输入与校验延迟；目录/字段元数据只在需要时更新，Repaint不逐文件序列化 |

当前恢复缓存使用 SessionState，跨域重载和当前编辑器会话有效，不能作为跨进程崩溃备份。多文件同时改名不是原子事务，相关限制必须保持可见。

## 可重复的源码检查

在完整验证目录使用 .NET 8：

~~~sh
dotnet run --project Tools/AbilityVerification/Editor/EditorTests.csproj
~~~

此入口编译生产 editor core/sandbox 与测试为不同程序集，运行 codec、工作区与沙盒检查。它使用 Unity 数学替身，不是 Unity Editor Test Runner。

本次在完整 ZIP 的全新解压目录运行：codec 22、工作区 28、非递归引用预检 25、沙盒 16，共 91 项通过；运行时 Core 316 项通过。开启和关闭 Odin 的兼容编译均为零警告、零错误。真实 Unity GUI／Undo／视觉验收仍未运行。

UI 编译需要项目本地合法安装的 Odin DLL 引用目录；不会自动下载或分发插件：

~~~sh
dotnet build Tools/AbilityVerification/Editor/EditorUiCompile.csproj -m:1 -p:OdinReferencePath=/your/Odin/Assemblies
dotnet build Tools/AbilityVerification/Editor/EditorUiCompile.csproj -m:1 -p:OdinEnabled=false
~~~

使用真实 Sirenix 程序集约束 API，但 UnityEditor/Core/IMGUI 为最小替身，所以编译通过不能替代上表中的视觉与交互验收。开启/关闭两种配置应串行构建或使用不同输出目录，以免混淆编译产物。

运行时0.2.1复查为Core419、Business49、独立消费7；编辑器自身仍为91项。整体运行时与目标设备门槛见 [技能验收记录](AbilityValidation.md) 和 [最新复查](Audits/AbilityRuntimeAudit-2026-11-05.md)。
