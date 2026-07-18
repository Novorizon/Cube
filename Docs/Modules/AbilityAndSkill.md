# Ability And Skill

本文记录 Ability / Skill 系统当前状态。

## 定位

Ability 系统主要服务塔防战斗中的技能、Buff、投射物和效果。岛屿经营当前不是 Ability 系统的重点。

## 目录

```text
Assets/Scripts/Ability
Assets/Scripts/Skill
Assets/Scripts/Game/TowerDefense/AbilityAdapters
Assets/Scripts/Game/TowerDefense
```

## 当前数据

正式 Luban 表包括：

```text
Data/Excel/AbilityConfig.xlsx
Data/Excel/AbilityAction.xlsx
Data/Excel/AbilityModifier.xlsx
Data/Excel/AbilityModifierProperty.xlsx
Data/Excel/AbilityProjectile.xlsx
Data/Excel/AbilitySpecialValue.xlsx
Data/Excel/AbilitySystemEnum.xlsx

Data/Excel/skill.xlsx
Data/Excel/skill_action.xlsx
Data/Excel/skill_modifier.xlsx
Data/Excel/skill_system_enum.xlsx
```

生成代码：

```text
Assets/Scripts/Game/Data/Generated
```

当前 `Tables` 同时包含 Ability 表和旧 Skill 表。

当前接入状态不是对等的：`Ability*.xlsx` 目前为空，`DataManager` 也没有把这些表转换成 Ability 运行时定义；现有可运行技能来自 `skill.xlsx`、`skill_action.xlsx`、`skill_modifier.xlsx`，再由 `AbilityConfigConverter` 转入 Ability core。完整字段说明见 `Docs/Architecture/ExcelDataDictionary.md`。

## 当前结构

```text
Ability core
  -> 通用技能运行时

Skill legacy
  -> 较旧技能配置和兼容

Game/TowerDefense/AbilityAdapters
  -> 把基地、防御塔、敌人、地图等业务对象适配到 Ability core
```

## 当前中间层

项目已经存在 Ability 与塔防业务之间的中间层，不需要重新编写一套平行系统：

```text
AbilityManager
  -> 中间层入口；初始化 AbilitySystem、注册配置定义、缓存业务对象适配器并转发施法/战斗请求

TdUnit
  -> 把 Npc、Tower、Base 包装为 Ability core 的 IUnit

TdWorld
  -> 把塔防场景中的动态对象提供给 Ability core 做范围和目标查询

TdResourceOwner
  -> 把业务资源映射为技能消耗资源

TdPresentation
  -> 把技能框架的特效和音效请求映射到业务表现系统

AbilityConfigConverter
  -> 把当前 Excel/Luban 数据转换为 AbilityDefinition、ModifierDefinition 等运行时定义
```

当前绑定由两条路径共同完成：`NpcManager`、`TowerManager` 和 `BaseManager` 发布不包含 Ability 类型的业务生命周期事件，中间层订阅事件后立即注册或注销对应适配器；`AbilityManager.GetUnit(...)` 的延迟绑定和每帧同步继续作为兼容与异常销毁兜底。适配器使用战斗内稳定实体 ID，注销时由 Ability core 统一清理关联技能、Modifier、投射物、Thinker 和持续表现句柄。`BattleFlowManager` 在结算完成后发布业务中立的战斗结束事件，中间层随即清空 Ability Runtime、适配器缓存和实体 ID；统一清理期间不会执行配置型 Modifier 的销毁伤害、治疗或派生动作。状态与现有移动/攻击属性已经通过中间层回传；以后增加新的业务属性时仍须沿用同一边界补充映射。

第二阶段在现有中间层上增量修改，不推倒重写，也不再建立另一套重复的适配系统。只有当现有单个适配器确实无法继续扩展时，才在保持现有接口和调用兼容的前提下拆分实现。

## 解耦与接入原则

以下三条是 Ability 系统后续设计、实现和代码审查必须遵守的边界：

1. **技能框架不能耦合业务层代码。** `Game.Ability` 不得引用 `Npc`、`Tower`、`BaseManager`、`DataManager`、业务单例、业务配置类型或具体资源/表现实现。框架只依赖自身定义和抽象接口。
2. **业务对象不能以耦合方式直接传入技能框架。** `Npc`、`Tower`、`Base`、召唤物、陷阱和其他动态目标不直接实现或持有 Ability 框架类型。它们由中间层注册或绑定，中间层使用适配器将业务对象转换为 `IUnit`、`IResourceOwner`、`IWorld`、`IPresentation` 等框架接口后再交给 Ability core。增加新的业务目标类型时，应增加或注册适配器，不应修改 Ability core 以认识该业务类型。
3. **技能框架产生的属性、状态和表现必须在业务对象上形成闭环。** 中间层负责把业务对象的阵营、类型、位置、存活、基础属性和基础状态提供给框架；再把框架计算出的伤害、治疗、Modifier 属性、眩晕/沉默/魔免等最终状态，以及特效、音效、投射物和持续表现请求，正确应用或映射回对应的业务对象。对象死亡、销毁、出售、升级替换或战斗结束时，中间层必须清理相关技能实例、Modifier、目标引用和持续表现。

依赖方向固定为：

```text
业务对象
  -> 业务中立的战斗调用/生命周期通知
  -> Ability 中间层（注册、绑定、转换、回传）
  -> Ability core

Ability core
  -> 抽象结果和表现请求
  -> Ability 中间层
  -> 业务属性、状态和表现
```

## 使用原则

- 新塔防技能的运行时优先走 Ability core；在原生 Ability Excel 转换链完成前，配置仍写入当前已接通的 `skill*.xlsx`。
- 旧 Skill 表和转换器可保留一段时间作为兼容层。
- 不要把 Ability core 写死到塔防具体类里。
- 优先扩展现有 Ability 中间层，不在业务代码中复制技能规则，也不建立第二套平行适配层。
- 经营系统需要技能或工具效果时，先评估是否真的复用 Ability，不要强行耦合。

## 阶段编号基准

后续阶段沿用最初评审规划的编号，不重新合并或改名：

```text
第二阶段：修复当前会触发的问题
第三阶段：修复 Ability Core
第四阶段：配置验证器
第五阶段：为 JSON 复杂技能预留 Provider
第六阶段：技能编辑器
```

动态业务对象注册/注销、中间层生命周期和战斗结束清理属于第二阶段的补充工作，不替代也不改变第三阶段。

## 第二阶段：已完成的当前问题修复

- `ActionRunner` 不再使用静态共享目标列表；每次动作执行租用独立列表并在 `finally` 归还，嵌套 Modifier/Action 不会覆盖外层目标。
- Poison Cloud 的周期组现在对 Modifier Parent 每秒造成 15 点魔法伤害。首次伤害在 1 秒后发生，持续 6 秒共 6 跳；大帧会补齐已完成周期，但不会执行过期后的额外跳数。
- 战斗 HUD 采用 `BattlePage + UIEmbeddedPanelGroup`，由父页面完整下发嵌入面板的 Create/Open/Close；Skill、Item、Build、Info、Status、Control 和 MiniMap 不再依赖未触发的子 `UIPanel.OnCreate`。
- 技能塔以 `TowerLevelConfig` 作为范围、基础伤害、攻击间隔和弹道表现的唯一来源；Ability 只附加冻结、分裂、眩晕等额外行为。技能失败时普通攻击继续，只有实际攻击后才重置攻击计时；Tower Level 会同步到 Ability Level。
- 同一 Action Group 不再重复配置相同自动特效。`skill_modifier.effectLocation` 不再错误转换为一次性 `OnCreated PlayEffect`，而是映射到由 Modifier 生命周期持有的持续表现句柄。
- 正式修改只写入 `Data/Excel/skill*.xlsx`，随后使用 Luban 同步生成 JSON/Bin；`skill_luban_excels_fixed` 仍只是历史备份。

## 第三阶段：已完成的 Ability Core 修复

第三阶段处理当前简单数据不容易触发、但复杂技能会立即遇到的 Core 语义问题。

### Modifier 事件作用域

- `DamageTaken` 默认只允许事件目标为 Modifier Parent。
- `DamageDealt` 默认只允许事件来源为 Modifier Parent。
- `AbilityExecuted`、`AbilityFullyCast` 默认要求施法者为 Parent 或 Caster。
- 只有显式声明的特殊情况才允许全局事件。
- 配置型 Modifier 不得仅按事件类型接收全局广播后直接执行动作；自定义 C# ModifierScript 仍可自行处理全局事件。

### 统一单位最终状态

`AbilitySystem.HasState` 作为技能框架内唯一最终状态来源，合并业务适配器提供的基础状态与 Modifier 状态。伤害、选目标、施法和业务查询都必须通过统一状态检查：

```text
Invulnerable
MagicImmune
Untargetable
Stunned
Silenced
Rooted
CommandRestricted
```

### 施法前摇与中断

前摇结束时重新检查施法者存活、眩晕/沉默、Ability Activated、冷却与资源、目标存活、距离、视野及自定义 `CastFilter`。目标注销时取消仍锁定该目标的前摇或引导；失败不得消耗资源、充能或冷却。

### 其他已记录 Core 缺陷

- 非删除型线性投射物对同一目标只能命中一次。
- 充能恢复从实际消耗充能后开始，并按完整恢复周期逐层恢复。

### 第三阶段验收

- 对应五项 KnownDefect 测试已全部解除 `Ignore`，并补充前摇结束重跑 `CastFilter`、Modifier 授予 `Untargetable`、显式全局事件、充能大帧补期和 `RootDisables` 行为测试。
- 复杂事件链、Modifier 嵌套、目标中途失效和穿透投射物行为可预测。
- Core 仍只依赖抽象接口，不引入任何塔防业务类型。

实现结果：配置型 Modifier 事件默认按 Parent/Caster 关系过滤，只有 `TriggerEventScope = Global` 才接收无关对象事件；伤害、目标过滤、施法和业务适配查询统一通过 `AbilitySystem.HasState` 合并业务基础状态与 Modifier 状态；施法前摇及引导期间会响应死亡、眩晕、沉默、RootDisables 和 CommandRestricted，前摇结束重新验证冷却、资源、目标、距离、视野和自定义过滤；单位注销会中止仍引用该单位的前摇或引导；非删除投射物记录已命中的实体 ID；充能从首次实际消耗开始计时并保留大帧跨周期余量。自动寻敌也改为使用当前 Ability Level、脚本修正后的 CastRange 和 Modifier CastRangeBonus，不再固定读取 1 级配置。

## 第四阶段：已完成的配置验证器

继续使用 Excel，但技能生成前必须完成语义验证：

```text
ID、内部名称及配置来源重复
Action、Modifier、Projectile、资源和特效引用缺失
UnitTarget 技能缺少合法目标类型
周期 Modifier 没有周期动作
被动技能缺少 Intrinsic Modifier
未使用技能和孤立 Modifier
直接或间接循环引用
塔攻击间隔与技能冷却冲突
同一特效被自动和显式重复播放
配置声明了运行时尚不支持的属性
Excel、生成 JSON/Bin 之间的版本与内容不一致
```

错误分级：`Error` 禁止生成；`Warning` 允许生成但必须在编辑器显示；`Info` 用于未使用配置和样例数据。验证器同时提供生成流程结果和 Unity Editor 可读报告，并能定位引用链。

实现结果：`AbilityConfigurationValidator` 位于 Ability 配置层，只接收来源无关的配置目录和 Source 元数据，不依赖塔防业务类型；Excel 适配器读取当前权威 `skill*.xlsx`、道具/资源表和塔等级绑定，把问题定位到文件、Sheet、行号、字段与引用链。当前规则覆盖重复 ID/名称/Action 顺序、缺失 Action Group/Modifier/Projectile/资源/特效、UnitTarget 目标、周期动作 Parent 语义、被动 Intrinsic、Modifier 直接/循环引用、塔攻速与技能冷却冲突、重复特效、无效枚举/运行时不支持字段、未引用技能/Modifier/Action Group。空的 `Ability*.xlsx` 仍是未接入脚手架，一旦误填数据会按 Error 阻止生成，避免出现“配置成功但运行时完全不读取”。

Unity 菜单 `Luban/Validate Ability Excel` 可独立校验并把 Error/Warning/Info 输出到 Console；`Luban/Update All` 在修改 XML 或生成前先校验。命令行 `Data/gen_client.bat` 调用同一套验证代码，Error 返回非零退出码并停止 Luban；`gen_all.bat` 也会传播失败，不再带错继续生成 Wave。当前正式 Excel 实测结果为 0 Error、0 Warning、6 Info，Info 均为“未发现配置绑定，可能由 UI/代码显式调用”。

## 第五阶段：已完成的 JSON 复杂技能 Provider 基础

Excel 和 JSON 只负责提供数据，核心继续接收统一的 Ability 定义：

```text
ExcelAbilityDefinitionProvider ┐
                               ├─ AbilityRegistry
JsonAbilityDefinitionProvider  ┘
```

- 一个技能只能来自 Excel 或 JSON 中的一个权威来源。
- 重复 ID 或名称直接报错，不按加载顺序覆盖。
- JSON 源目录与 Excel 生成 JSON/Bin 目录分开。
- 注册记录保留 `SourceType` 和源文件位置，便于报错定位。
- JSON 技能可以引用全局公共 Modifier；私有 Modifier 建议使用技能命名空间。
- Excel 和 JSON 都转换为同一套 `AbilityDefinition`、`ModifierDefinition`、`ActionDefinition`，Runtime 不判断来源。
- 该阶段只建立 Provider 接口、校验和少量复杂技能样例，不急于迁移现有 Excel 技能。

实现结果：Ability core 新增 `IAbilityDefinitionProvider`、`AbilityDefinitionBundle`、`AbilityDefinitionOrigin` 和 `AbilityDefinitionRegistry`。Registry 按技能、Modifier、Projectile 分别检查稳定 ID 与内部名称；Excel/JSON 任一来源发生冲突都会报 Error，保留两侧 SourceType、源文件和引用链，且 `ApplyTo(AbilitySystem)` 直接拒绝整个无效 Registry，不存在“后加载覆盖先加载”。合并后再次验证 Intrinsic、Aura、AddModifier 和 Projectile 引用。

当前 Luban `skill*.xlsx` 由中间层 `ExcelAbilityDefinitionProvider` 转换，不再由 `AbilityManager` 直接逐表写入 Core 注册表；`AbilityManager.AddDefinitionProvider` 可在初始化前追加 JSON 等来源，之后统一合并、校验并一次性应用。JSON 由 `JsonAbilityDefinitionProvider` 转换为相同的 `AbilityDefinition`、`ModifierDefinition`、`ProjectileDefinition` 和 `ActionDefinition`，支持等级数值、Special Value、充能、生命周期动作、事件作用域、属性/状态、Aura、嵌套动作及本地 Projectile。JSON 可以按精确名称引用 Excel 或其他 Provider 的公共 Modifier；私有 Modifier/Projectile 未使用文档 namespace 前缀时会产生 Warning。

手写 JSON 源目录为 `Data/AbilityJsonSources`，与 Luban 生成目录 `Assets/Data/Json` 完全分开；样例 `Samples/chain_reaction.sample.json` 只作为格式和测试数据，不会自动迁移或覆盖现有 Excel 技能。现阶段仍以 Excel Provider 为默认来源，只有显式在初始化前注册的 JSON Provider 才参与运行时。

## 第六阶段：技能编辑器（基础能力已完成）

编辑器分多次实施，不直接从完整节点编辑器开始：

1. 先制作技能配置检查器：搜索、筛选、引用关系、错误定位和只读预览。
2. 再制作普通字段表单和 JSON 树形编辑，支持复制子效果、折叠嵌套和局部校验。
3. 实际复杂技能数量和重复维护成本足够高时，再扩展完整节点编辑器。

编辑器必须修改权威源文件并复用第四、第五阶段的校验和 Provider 管线，不得直接修改生成 JSON/Bin。

同时提供只读运行时调试窗口，查看中间层注册对象、稳定实体 ID、技能阶段/冷却/充能、Modifier 状态与属性贡献、目标过滤与 Action 链、投射物/Thinker/持续表现，以及注销或战斗结束后的残留对象。

第六阶段完成标准：配置人员可以追踪任意技能的引用链；复杂 JSON 技能经过项目级合并校验后可以安全编辑；运行时调试窗口可以证明对象注销后没有残留技能、Modifier、投射物、Thinker 和表现句柄。是否继续扩大完整图编辑器，由实际技能样本决定。当前只完成单文件 JSON 表单和单 Provider 校验，项目级 Excel + 全部 JSON 合并校验仍列入下方 TODO。

第一轮实现位于 Unity 菜单 `Tools/Ability/Workbench`，包含三个页签：

- **Excel 检查器**：复用第四阶段验证器，显示 Error/Warning/Info 数量，支持搜索、严重级别筛选、源文件定位、技能只读字段、Action Group 及其 Modifier 引用关系。它只读取权威 Excel，不修改 Luban 生成 JSON/Bin。
- **JSON 编辑**：打开或新建 `Data/AbilityJsonSources` 下的文档，以 Unity SerializedProperty 树形表单折叠编辑 Ability、Modifier、Projectile、Special Value 和 Action 数组；数组元素可使用 Unity 标准数组菜单复制。保存前把当前内存文档交给 `JsonAbilityDefinitionProvider + AbilityDefinitionRegistry` 做单文件校验，存在 Error 时拒绝写盘。当前不会同时加载 Excel 和其他 JSON，因此不能在这里最终证明跨来源引用、名称和 Stable ID 没有冲突。保存会格式化 JSON，不保留注释和未知字段，窗口中已明确提示。
- **运行时调试**：Play Mode 下每 0.25 秒显示中间层业务对象 ID 到稳定 Runtime EntityId 的绑定，以及技能 Level/Phase/Cooldown/Charges、Modifier 剩余时间/状态/属性贡献、Projectile、Thinker 和持续表现句柄。`AbilitySystem.CreateRuntimeSnapshot` 只返回框架中立快照；业务对象名称和绑定信息仍由中间层补充。对象注销或战斗结束后，列表应归零，已有回归测试覆盖快照和表现句柄在 `RemoveUnit` 后清空。

持续表现现在通过 `IPresentationHandle` 建立可停止生命周期：Modifier 创建时请求句柄，移除、对象注销和战斗清理时统一停止；`TdPresentation` 即使异步资源在停止后才加载，也会立即销毁实例，避免结算后“迟到特效”；调试快照可枚举活动句柄。至此第六阶段的 Excel 检查、单文件 JSON 表单和只读运行时证明链已经具备。完整节点图编辑器仍按原计划暂不制作，它是根据真实复杂技能数量决定的可选扩展，不是当前完成条件。

## 完整检查后的 TODO（2026-07-18）

本节来自对 Ability Core、配置/Provider、塔防适配层、Npc/Tower/Base 实际调用链、当前 7 个技能及现有测试的完整检查。它是原第二至第六阶段之后的修正和补强清单，不改变前面的阶段编号。当前结论是：简单 Excel 技能的基础链路可继续使用；JSON 只完成定义层基础，尚未端到端接入业务；复杂 Dota 式事件技能还不能直接投产。

### P1：复杂技能投入前必须完成

- [ ] **Action 与事件递归保护**：为一次施法/事件链增加最大嵌套深度、最大 Action 数和可诊断的中止原因；为 Modifier 周期补偿增加单帧最大触发数，防止 `DamageTaken -> Damage -> DamageTaken`、ModifierAdded 链和极小 Interval 卡死主线程。
- [ ] **战斗事件入口**：在 Core 提供受控的业务事件通知 API，由中间层接入 `AttackStart`、`AttackLanded` 和 `Death`；当前这些事件可以配置和通过验证，但业务从未广播它们。
- [ ] **点目标空放规则**：`CastAbilityAtBestTarget` 找不到目标时不能默认向 Caster 位置成功释放 Frost Nova、Poison Cloud 等技能并消耗资源。需要返回 `InvalidTarget`，或为允许空地释放的技能增加明确配置。
- [ ] **运行时能力矩阵**：逐项确认并实现或禁用 `ArmorBonus`、`HealthRegen`、`CooldownReductionPercent`、`Disarmed`、`Muted`、`Hexed`、`OutOfGame`、`NoUnitCollision`、`NonLethal/HpLoss/IgnoreBlock`、`CastBackswing`、`AutoCast`、`Immediate`、`Queue` 和 Projectile Vision。未实现能力必须由 Excel/JSON 验证器按 Error 阻止，不能静默加载。
- [ ] **独立 Modifier 动作**：修复 `AddModifierToNpc/Tower/Base` 产生的无 Ability Modifier。当前 `ActionRunner` 要求 `context.Ability != null`，导致这类 Modifier 的 OnCreated、Interval、Trigger 和 OnDestroy 配置动作静默不执行。
- [ ] **自定义脚本清理重入**：战斗清理期间禁止或安全处理自定义 `AbilityScript/ModifierScript.OnRemoved/OnDestroy` 再创建 Ability、Modifier、Thinker 或表现句柄，确保新建对象不会被容器直接丢弃。

### P1：JSON 真正接入业务

- [ ] **Provider 加载清单**：建立明确的 JSON 源清单和启动加载器，在 `AbilityManager.Initialize` 之前注册批准的 JSON Provider；示例目录继续不自动加载。
- [ ] **来源无关的技能键**：业务门面不能只接收 Excel `int skillId`。建立可同时表示 Excel ID 和 JSON Stable ID/Name 的 Ability Key，并提供不泄漏 `IUnit`、Core `CastResult` 的 Npc/Tower/Base 添加与释放接口。
- [ ] **资源和业务绑定**：定义 JSON 技能如何选择业务资源、进入技能栏、绑定 TowerLevel/单位模板，以及如何显示本地化名称、描述和图标。完成之前，JSON Ability 即使进入 Registry 也无法通过正常业务入口使用。
- [ ] **项目级合并校验**：Workbench 和命令行校验必须一次加载权威 Excel、全部启用 JSON 和业务绑定，检查跨来源公共 Modifier 引用、名称/Stable ID 冲突、资源及表现引用。单文件 Provider 校验只能作为局部反馈。
- [ ] **严格 JSON 语义校验**：补 Behavior/Target/State/Attribute/Event/DamageType/DamageFlags、LevelValue、Charges、持续时间、Projectile 参数、被动 Intrinsic、Toggle 行为、Modifier 环和事件反馈环检查；未知字段和拼写错误不能由 JsonUtility 静默忽略。
- [ ] **真实复杂技能验收**：至少制作两个实际由业务加载的 JSON 技能，一个覆盖 Projectile Hit/事件链，一个覆盖 Aura/Thinker/周期区域；不能只使用“创建投射物后立即对原目标伤害”的样例证明投射物技能完成。

### P1：当前业务表现与技能语义

- [ ] **实际音效适配**：`TdPresentation.PlaySound` 当前只输出日志。接入正式音频服务，并统一 Excel 数字声音 ID 与 JSON 资源名的解析规则。
- [ ] **Modifier 表现字段拆分**：将单一 `effectLocation` 拆为 `onApplyEffect`、`loopEffect`、`onRemoveEffect`。当前 Modifier 使用非循环 `IceHitEffect.prefab` 作为持续句柄，粒子结束后空对象会保留到 Modifier 销毁；Action 命中特效与 Modifier 特效还可能重复播放。
- [ ] **Projectile 逻辑与表现绑定**：投射物表现必须跟随 Core Projectile 位置并在销毁时停止；线性投射物改为线段/扫掠检测，避免低帧率或高速穿透；Tracking Projectile 增加最大距离或寿命；处理战斗结束后异步加载的一次性特效。
- [ ] **Poison Cloud 设计确认**：当前实现是“释放瞬间给范围内单位添加 6 秒 DoT”，不是持续地面云，后来进入范围的单位不会中毒。若设计要求真正毒云，应使用 Thinker/区域 Modifier；否则修改名称和说明，避免配置语义误解。
- [ ] **Attack Speed Aura 设计确认**：`50001001` 当前没有业务绑定，也不是真正 Aura，只是拥有者自身的 Intrinsic AttackSpeed Buff。决定是实现范围 Aura，还是更名为 Self Buff 并明确绑定对象。
- [ ] **塔技能等级**：Legacy Excel 转换需要明确等级策略。当前 `AbilityDefinition.MaxLevel` 默认为 1，Tower Level 2/3 虽传入 Ability，最终仍会 Clamp 为 1；在加入等级技能前必须修复或明确全部附加行为保持常量。

### P2：中间层边界与扩展性

- [ ] **收紧业务门面**：减少公开 `AbilityManager.Engine`、`IUnit`、Core `CastResult`、`AbilityScript` 等类型；MapManager、TowerManager 等业务代码使用中间层自己的结果 DTO 和失败原因，避免 Core 类型继续扩散。
- [ ] **通用动态对象绑定**：把 Npc、Tower、Base 三套硬编码字典和 `TdUnitKind` switch 收敛为适配器/绑定注册表，使召唤物、陷阱和其他动态目标只注册新适配器，不修改多处分支。
- [ ] **统一单位身份语义**：Core 中状态、属性、Purge、Modifier 刷新等路径统一按稳定 EntityId 或明确的身份比较器判断同一单位，不能一部分按引用、一部分按 EntityId。当前 TdUnit 缓存只能保证已有三类对象不触发问题。
- [ ] **移出塔防专属验证**：`TowerAbilityBindingRecord` 和塔攻击间隔规则从 `Game.Ability.Configuration` 移到塔防/Editor 验证扩展，保持 Core 配置层的业务中立。
- [ ] **定义重载/重初始化**：明确 `AbilitySystem.Initialize` 是否清空定义和脚本工厂。当前只清运行时，删除或禁用的定义可能留在 Engine；若支持重新加载配置，需要原子替换 Definition Registry。
- [ ] **死亡和治疗闭环**：死亡事件应在 HP 首次降为 0 时立即进入技能事件链，而不是等待异步死亡动画结束后注销；治疗通过业务 Manager/事件更新，而不是由 TdUnit 直接修改 NpcData，确保 UI、统计和任务系统收到一致通知。

### P2：规模与性能

- [ ] Modifier 按 Parent EntityId 建索引，避免每个单位每帧查询状态/属性时扫描全局 Modifier。
- [ ] `IWorld` 接入空间索引；Projectile、Aura、AOE 不再每次扫描全部 Npc/Tower/Base。
- [ ] 复用 Targeting、FindUnits、Projectile 和 Ability Update 的临时集合，减少战斗帧 GC。
- [ ] 统一 Modifier、Thinker、Charge 在大帧下的周期补偿语义，并为补偿次数设置上限。

### 测试与验收 TODO

- [ ] 为事件递归预算、AttackLanded/Death、独立 Modifier、点目标空放、扫掠投射物、Tracking Projectile 生命周期和自定义清理重入增加 Core 回归测试。
- [ ] 建立 `AbilityManager + TdUnit + TdWorld + TdPresentation` 的中间层测试，覆盖注册、对象替换、死亡、出售、升级替换、Base 清理和战斗结束。
- [ ] 对 Fireball、Frost Nova、Ice Tower Shot、Heal、Poison Cloud、Stun Bolt、Attack Speed Aura 建立数据到业务结果的端到端验收，不能只验证表行和转换结果。
- [ ] JSON 测试必须覆盖 Excel + 多 JSON 同时加载、跨来源引用/冲突、业务资源消耗、UI/Tower 绑定和 Release 后零残留。
- [ ] 当前相关测试源码共有 44 个，项目编译与配置验证通过；按此前约定尚未执行 Unity Test Runner。正式发布前仍需在 Unity 中实际执行 EditMode/Integration 测试并保存结果。

### 延后事项

- [ ] 完整节点技能编辑器继续延后。先完成运行时安全、JSON 业务接入和项目级校验，再根据真实复杂技能数量决定是否制作。
- [ ] `Assets/Scripts/Skill` 旧框架当前没有业务调用。待 Ability 端到端测试稳定并确认没有外部反射/资源引用后，再标记 Obsolete、迁移文档并最终删除，避免两套技能概念重新进入业务。
