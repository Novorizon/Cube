# 基于 Odin 的技能编辑器规划

本轮先用可运行 JSON 实现代表英雄技能，再据实际编写、校验和测试中的需求确定编辑器范围。本文件是设计与验收计划；尚未添加编辑器代码、安装 Odin 或完成 Unity 内验证。

编辑器的交付目标是：设计人员通过表单配置技能、Modifier 和弹道，导出的同一份 JSON 经生产 `JsonAbilityDefinitionProvider` 与 `AbilityDefinitionRegistry` 发布后执行。项目仍通过技能包公开 API 接入，自定义技能脚本属于业务程序集。

## 1. 包与依赖边界

建议新增独立可选包 `com.novorizon.ability.editor.odin`，只含 Editor 程序集，命名为 `Game.Ability.Editor.Odin`，`includePlatforms` 限定 Editor。依赖关系为：

```text
项目业务 → Game.Ability
Odin 编辑器 → Game.Ability + UnityEditor + 已安装的 Odin Editor
项目专用编辑扩展 → Odin 编辑器公开扩展接口 + 项目自己的类型
```

`Game.Ability` 不引用 `UnityEditor`、Sirenix、窗口、Drawer 或项目类型。运行时目录不添加 Odin 特性、Odin 序列化基类或编辑器配置资产。安装运行时包的用户无需安装编辑器或 Odin。

Odin 由使用编辑器的 Unity 项目单独安装，不随技能包重分发。Odin 的安装路径、UPM 名称和程序集形态应在实际项目确认后填写，不能凭文档猜一个包 ID 或用目录名硬编码探测。先固定并验收 Unity 2022.3 与项目选定的 Odin 版本；官方文档提供的 API 是设计依据，不代表本工程已经编译验证该版本。MVP 使用 Odin Inspector 的窗口和绘制能力，核心校验继续复用运行时，不把另购 Odin Validator 作为必要条件。

## 2. JSON 与编辑草稿

JSON 是权威可执行配置。编辑器持有自己的 `DocumentSession`、可撤销 `DraftDocument` 和显示模型，转换成包的公开 JSON DTO 后导出；不直接编辑已发布的 `AbilityDefinition` 或正在战斗的运行时实例。

| 数据 | 放置位置 | 约束 |
| --- | --- | --- |
| 技能/Modifier/弹道及引用 | 用户指定的数据目录中的技能 JSON | 字段、枚举值和默认语义由生产 schema 决定 |
| 名称、说明、等级数值 | 对应 JSON 定义 | 显示名可变，稳定 ID 不随显示名变动 |
| 窗口布局、最近打开项、折叠状态 | Editor 本地设置 | 不写入运行时 JSON |
| 编辑项目根目录、启用的 Provider、只读库 | Editor 配置资产 | 不依赖 Cube 的路径、DataManager 或资源单例 |
| 英雄分类、设计备注和参考来源 | 可选编辑元数据旁文件 | 不进入严格技能 JSON；运行时 Provider 清单明确排除旁文件 |
| 撤销、未完成字段、原文、磁盘内容哈希 | Editor 草稿会话 | 保留原文供失败恢复；不把无效草稿当成已发布数据 |

使用 Editor DTO 和 Drawer 添加 Odin 特性，维持字段到 JSON 路径的显式映射。允许复用公开 `JsonAbilityDocument` 等 DTO，但 UI 元数据不能加入运行时 DTO。转换层需要完整字段覆盖测试；仅靠反射画出全部字段还不能满足作者工作流。

缺省、显式零、空数组、`null` 与继承默认值可能具有不同含义，草稿需要记录字段是否存在。例如动作未覆盖 duration 时继承规则，不能因表单初始显示 `0` 就导出永久效果；运行时内部的 NaN sentinel 也不能导出为非法 JSON。导入再导出必须通过语义往返验证。没有修改文件时不自动格式化或回写；主动保存使用规定的确定性格式。

## 3. 窗口与 Odin 用法

以 `OdinMenuEditorWindow` 为主窗口，通过 `OdinMenuTree` 展示目录、英雄文件及其中的 Ability / Modifier / Projectile；菜单键使用文件身份和稳定 ID，显示名称不作为引用键。右侧编辑当前草稿，下方显示校验结果、引用关系和只读导出预览。官方窗口教程支持这一组织方式，并说明自定义 IMGUI 可通过 `OnInspectorGUI` 或相应绘制扩展加入；不直接重写并绕过 Odin 的 `OnGUI` 生命周期。[Odin 窗口官方教程](https://odininspector.com/tutorials/custom-editor-windows/odin-editor-windows)

窗口自身按需绘制选中对象，不为整个目录永久创建 PropertyTree。需要手动树时，由会话明确拥有 `PropertyTree`，复用而非每帧创建，在切换文档、关窗和脚本重载时释放；清理更新事件和文件监听。官方 API 提供 `Create`、`Draw` 与 `Dispose`，具体签名以锁定版本为准。[PropertyTree API](https://odininspector.com/documentation/sirenix.odininspector.editor.propertytree)

LevelValue、引用和 flags 采用 `OdinValueDrawer<T>` 等 Editor 自定义 Drawer。所有 Drawer 都要支持只读文档、撤销和可空标签，避免在绘制函数中发布技能、查询实际战斗或写磁盘。具体覆写签名在实现版本编译确认，本文不把文档示例当作可直接运行代码。[自定义 Value Drawer 官方说明](https://odininspector.com/tutorials/how-to-create-custom-drawers-using-odin/how-to-create-a-custom-value-drawer)

## 4. MVP 表单

| 编辑区 | 作者操作 | 导出与约束 |
| --- | --- | --- |
| 文档 | 新建、导入、复制文件、设置命名空间、搜索 | 显示 schemaVersion；只读取明确选择的技能 JSON，不扫描全部 JSON 充当 Provider |
| 技能基础 | 名称、说明、图标字符串、最大等级、目标规则 | Behavior、TargetTeam、UnitType、TargetFlags 显示中文和原枚举名，导出整数值 |
| 等级数值 | 常量/每级值模式；等级列、复制上一等级、增加等级 | 对应 JsonLevelValue；显示超出数组时沿用末项的实际规则，不擅自插值 |
| 施法与成本 | 距离、半径、前后摇、引导、冷却、法力、充能 | UI 提示无关组合，最终仍以生产 validator 为准 |
| 动作列表 | 添加、删除、复制、排序；按 actionType 仅显示相关字段 | 顺序具有执行含义，不能为了格式排序；`actions`、`onProjectileHit`、toggle 各自分组 |
| 动作数值 | 常量/等级值或 Special 引用；事件数值源与倍率 | 列出当前技能 Special；提示对应触发上下文可使用的 EventDamage/EventHealing 等来源 |
| Modifier | 状态 flags、持续时间、interval、层数、驱散/死亡策略、生命周期和事件动作 | 区分 Parent、Caster、PrimaryTarget、EventSource、EventTarget，附上下文说明 |
| 属性 | 固定 `properties` 或 `levelProperties` 的等级值 | 同一 property 两种定义互斥；按来源技能等级求值，不能把所有 Buff 属性默认视为固定 float |
| 光环 | 子 Modifier 引用、半径、维护间隔、滞留、关系和过滤 | 校验环、自引用、来源与子效果；预览维护不叠层及离开滞留语义 |
| 弹道 | 追踪/直线动作引用、速度、半径、射程、寿命、命中删除与目标规则 | 命中动作属于发射技能；不将弹道命中等同于已提交伤害事件 |
| 扩展脚本 | 声明 requiresCustomScript，查看所需业务工厂名称 | 编辑器不扫描后直接实例化任意项目脚本；自定义脚本注册由显式沙盒适配器提供 |

当前 8 种 ActionType 和公开字段是 MVP 上限。召唤、概率 proc、种族分支、百分比生命规则或独立延迟区域等机制，在运行时尚未有相应通用支持时，界面不生成看似可用的伪动作。用户可以保留明确声明的外部脚本扩展，但必须显示运行依赖和沙盒验证状态。

选择 actionType 时不能悄悄丢弃原字段；转换命令预览将清理的无关字段并作为一次可撤销操作执行。隐藏区域中的值不会因 Inspector 折叠而自动重置。

## 5. ID 与引用编辑

为 Ability、Modifier、Projectile、Special 提供带类型的候选选择器，展示名称、稳定 ID、来源文件和是否只读。引用序列化格式必须遵守现有字段语义：例如 `modifierName` 和 `projectileName` 当前引用定义 Name，不能在 UI 中任意改写成稳定 ID。稳定 ID 用于导航和身份，不替换运行时已有解析规则。

只读来源包括代码/Excel Provider 的已导入索引；编辑器允许引用和查看来源，不尝试把它们覆盖为 JSON。跨文件是否公开以及私有定义的可见性由当前注册表校验约定决定，不能根据 `isPrivate` 的名字自行添加另一套链接规则。

MVP 复制定义时生成新 ID 和 Name，并在复制的依赖闭包中重写内部引用；出现外部引用时展示并保留或显式复制。全工程重命名后续实现，需要先计算引用影响、只读命中和跨文件修改清单；禁止仅替换字符串。多文件重构需事务日志和失败回滚，不假装多次单文件写入具有整体原子性。

## 6. 校验与错误定位

编辑器只增加作者提示，生产 `AbilityJsonSchemaValidator`、Provider、Registry 和最终图校验仍是可执行规则的来源。流程如下：

1. **输入提示**：必填、有限数值、枚举/flags、重复属性、每级列、明显无关字段；不运行战斗。
2. **文档校验**：草稿导出到内存字符串，走真实 JSON reader，定位未知字段、重复键、类型和 schemaVersion。
3. **工程校验**：将启用的文件草稿与未修改 Provider 一起加载到新 Registry，检查重复 ID/Name、跨文件引用、创建环及脚本声明；不修改线上注册表。
4. **沙盒发布校验**：只有显式运行验证时才创建隔离引擎、注册选定扩展工厂，调用 `ApplyTo` 并读取 `LastApplyValidation`，检查真实脚本与运行时条件。

结果列表显示 severity、code、message、source 和 reference chain。现有语法错误含 offset，图错误未必有精确 JSON path；Editor codec 维护“草稿字段路径 → ID/属性 → 文本 span”的索引来辅助跳转。没有可靠映射时跳到文件/定义并显示完整链，不伪造行列。错误映射只服务 UI，不在编辑器再实现另一套规则计算。

未知字段或更新的 schemaVersion 以只读方式打开原文，阻止结构化覆盖；通过显式迁移或受支持版本重新读取。MVP 不悄悄忽略、删除或保留并导出运行时仍会拒绝的未知字段。旁文件只存编辑元数据，不作为绕过 schema 的扩展通道。

## 7. 撤销、脏标记与保存

`DocumentSession` 使用独立 Editor ScriptableObject 持有 Unity 可序列化的草稿字段和存在位；普通表单通过序列化/PropertyTree 编辑。批量增删、模板和引用修改在变更前显式建立 Undo 组，统一走命令边界，避免 UI 修改、树修改和磁盘写入各自维护一套撤销。`Undo.RecordObject` 接受 Unity Object 并记录调用后的变化，不能把任意 POCO 当成可撤销对象。[Unity 2022.3 Undo API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Undo.RecordObject.html)

脏状态使用“当前草稿语义指纹”与“最后已加载/保存语义指纹”比较；撤销回已保存内容时恢复干净状态。Undo 只撤销内存草稿，不自动回写已保存文件。关窗、切文件和重载时保留或恢复草稿；磁盘外部变化比较原始文件哈希，存在冲突时保留两个版本并阻止覆盖，不能凭时间戳判断。

保存由独立 `AbilityJsonWriter` 输出契约 JSON：固定字段顺序、固定缩进/换行、文化无关 round-trip float、正确字符串转义，保持动作/等级数组顺序。字典类集合按明确规则稳定排序，禁止 NaN/Infinity。输出再次走生产 Provider + Registry 校验，通过后才发布文件。无效草稿可保存在 Editor 恢复区，不能覆盖正常运行时 JSON。

`EditorJsonUtility` 是 Unity Editor 对象的 JSON 工具，不直接作为运行时技能文件格式；仅可用于适合的 Editor 会话恢复。其字段/引擎引用行为不替代技能 writer 的省略与默认值政策。[Unity EditorJsonUtility](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorJsonUtility.html)

单文件先写同目录临时文件并关闭，复读校验后用目标平台支持的替换/重命名提交；失败保留原文件与可恢复草稿，再刷新 AssetDatabase。各平台的覆盖替换、只读文件、源控制和断电恢复需要专项验收，不能用“写了临时文件”就断言跨平台完全原子。只有真正更改的文件需要刷新。

## 8. 数值预览与运行沙盒

MVP 提供只读等级表、费用/冷却/时序说明、AOE/线弹几何示意和引用链；数值来自 runtime 公开求值，包含来源等级和默认值说明。`EvaluateOrder` 只能在隔离上下文中做无消耗预检，不作为技能伤害已验证的证据，也不保留正式施法资源。

后续测试面板显式创建隔离 `AbilitySystem`，由独立适配器提供测试单位、资源、世界和表现，手动推进 simulation tick，显示 CastId、事件轨迹、实际 HP 和清理后的对象数。预览对象不借用项目正在战斗的实例；退出、失败、重载和 PlayMode 切换时均清理。外部脚本可能有副作用，必须由项目显式选择和提供沙盒注册，不在普通 Inspector 绘制或导入文件时执行。

真正的 PlayMode、物理/表现、IL2CPP 和目标设备性能仍按 [技能验收文档](AbilityValidation.md) 执行。编辑器曲线、数值估算或模拟通过都不能替代这些门槛。

## 9. 开发顺序与验收

| 阶段 | 实现内容 | 完成门槛 |
| --- | --- | --- |
| A：契约与安全保存 | 草稿/JSON映射、确定性 writer、导入、只读错误、语义往返、会话Undo、文件哈希冲突和替换 | 所有英雄/验证 JSON 往返语义一致；动作顺序、缺省duration和levelProperties不丢失；保存失败不损坏原文 |
| B：Odin MVP | 菜单树、基础/每级值/动作/Modifier/弹道/引用 Drawer、生产校验及定位 | 能从空文档完整制作现有英雄技能；不需手改整数枚举；不产生 Sirenix 运行时依赖 |
| C：组合工作流 | 跨文件引用浏览、依赖闭包复制、只读来源索引、重命名事务、模板 | 所有引用均可导航；受影响文件可预览；失败可恢复；无按加载顺序覆盖 |
| D：测试与规模 | 隔离数值沙盒、事件轨迹、回归入口、大目录性能与资源检查 | 关闭沙盒后对象/订阅释放；校验/索引增量执行而非每帧全量；不把合成预览宣称为目标设备测试 |

优先 A 与 B，先让正确配置可以可靠地产出，再做复杂节点图或可视化时间轴。现有有序 Action 列表已经表达多数简单组合；节点式显示可以作为只读依赖图，MVP 不引入独立图执行语言或第二套编译器。

验收至少包含：

- **包边界**：仅安装运行时包时不需要 Odin；安装编辑包且项目有选定 Odin 时编译通过；Player 无 Editor/Sirenix 引用泄漏。
- **金样例**：本轮 Dota 2、Warcraft 3 英雄 JSON 与现有 Validation JSON 执行“读取→草稿→写出→生产发布→原场景断言”；文件依赖闭包、各等级数值、状态和动作顺序保持。
- **schema**：未知/重复字段、非法数值、非法 flags、丢引用、循环、重复属性、缺少外部脚本均显示错误；错误文件不会被自动覆盖。
- **交互**：修改/新增/删除/排序/复制/批量操作均支持 Undo/Redo；撤销回保存点清除脏标记；关窗、域重载和磁盘冲突可恢复。
- **文件**：同内容重复导出字节一致；缺省与显式零区分；只读、I/O失败、替换失败及多文件部分失败不丢原始数据。
- **性能与泄漏**：只绘制当前选择，索引变化时才更新；校验批量合并与节流；反复切文件/开关窗口后 PropertyTree、监听器和草稿会话不持续增长；用代表性文件规模实测交互耗时后确定预算。

当前规划不依赖已存在的编辑器实现。进入实作时首先验收项目使用的 Odin/Unity 版本和程序集引用，然后以英雄 JSON 的实际往返及场景断言作为 MVP 验收资料。
