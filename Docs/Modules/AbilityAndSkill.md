# Ability And Skill

## 定位与边界

`Assets/Scripts/Ability` 是可独立导入 Unity 的 Dota 风格技能包，提供 UPM package.json。包不引用 Npc、Tower、DataManager、业务单例或资源服务。业务可以直接使用公开 Ability/Modifier/CastResult API，并在自己的程序集实现接口或适配器；不需要为了新项目修改包。当前 Cube 的 `Game/TowerDefense/AbilityAdapters` 是项目自己选择的接入层，不是包运行依赖。依赖方向是业务→技能包，包通过抽象接口请求项目能力。

`Assets/Scripts/Skill` 是历史实现，本次不迁移或删除。新单位类型应增加适配器，不能让 Core 认识业务类型。

## 模块职责

| 模块 | 职责 |
| --- | --- |
| AbilitySystem | 注册、所有权、绑定、更新、快照和统一销毁 |
| AbilitySystem.Execution | 执行预算、注销保护、代际、原子定义替换 |
| AbilitySystem.Casting / Ability / Targeting | 订单、单位仲裁、队列、阶段与合法目标 |
| AbilitySystem.Combat / CombatContracts | 来源类别、计算/提交/通知、HP结果、攻击记录、死亡复活 |
| Modifier / Thinker / ActionRunner | 生命周期、周期时间线、状态/属性、光环及配置动作 |
| Projectile / ProjectileContracts | 扫掠、射程/寿命、命中终止、移动表现抽象 |
| Configuration | Provider、严格JSON、最终图校验、深复制发布 |
| AbilityManager / AbilityBusinessFacade | 来源无关业务键、战斗句柄、业务结果、兼容入口 |
| TdUnit / World / ResourceOwner / Presentation | 实际HP、动态目标、资源、特效与音频闭环 |

partial 文件只分区职责，仍是一个 Engine，不建立第二套适配系统。

## 阶段编号基准

保留此前第二至第六阶段含义：中间层接入、Core基础修复、Excel校验、多来源Provider、编辑器基础。它们是功能基础，不代表完整Dota规则或端到端验收。本轮正确性修正统一为第七阶段，以下为当前状态，不继续累积过期TODO。

## 第七阶段：施法与生命周期

- 订单复制并按无目标/单位/地点归一化。额外目标不能绕过过滤；过滤后的空集合不能回退到非法对象。Caster必须匹配实际Owner。
- 普通施法按单位仲裁，忙碌时明确拒绝；Immediate为例外。Queue保存订单快照，等待当前阶段/后摇结束，每单位最多32条；Stop默认清队列。
- 等级0表示未学习，不提供Intrinsic；训练、升级和重生按生命周期恢复。施法不隐式改等级。
- 前摇、引导、后摇及中断按CastId关联。接受不等于执行，事件可在调用返回前同步发生。回调后检查阶段、登记和代际，终止只发生一次。
- Toggle使用显式On/Off动作；开启执行配置费用/CD，关闭不因资源不足或CD拒绝，仍检查限制状态。普通Actions不能放在Toggle中静默丢弃。
- 构造只绑定，先登记再OnCreated；自删除时创建返回null。移除先撤销登记，旧Ability不能再次发起施法。异常时仍尝试中断、被动移除、清理回调，再聚合报告。
- 注销中的单位不能在回调中重注册；全量清理阻止新建/战斗操作，释放表现并更新RuntimeGeneration。迟到回调不能进入新战斗。
- 一条因果链共享预算，默认深度64、操作2048，超限发ExecutionAborted，下一独立链恢复。单次Modifier/Thinker更新最多128次周期回调，超额历史回调跳过并推进计时，不积累无界债务。

## Modifier、光环与计时

独立Modifier携带等级，常量动作不依赖Ability。有来源技能时查询其等级，脚本需要快照可在OnCreated保存。刷新键区分Parent、Name、Caster、Ability、光环身份及SourceAura，null不是通配符。初始和后续叠层都限制MaxStack。

光环默认只续期，`auraIncrementsStacks=true`才按脉冲涨层。已有子效果在 Update 开始采样覆盖，覆盖期间保留完整离开滞留时间，离开后的帧才倒计时；维护续租在帧尾执行，不因 source/child 更新顺序重复扣时。不同来源的 Multiple 子效果独立，同一来源维护同一个实例。周期回调按时间线推进，边界刷新不会再扣掉整帧；每次回调后检查死亡/销毁。再生只作用于有效时间。兼容约定仍是Duration<=0不自动到期，永久建议用-1，缺失SpecialValue必须报错。

Update先取其他对象快照再推进Ability；回调新建对象从下一Update老化，不追扣整帧。引导回调裁剪到有效剩余时间，前摇跨帧仅用溢出时间推进引导。未来固定步长调度应保持同一契约。

## 战斗结算与投射物

Spell/Attack/HpLoss来源类别与Physical/Magical/Pure类型分开。物理/纯粹技能同样适用法术增强。Core处理护甲、魔抗和物理格挡，Base只应用最终值，避免重复减防御。

计算期只允许受控数额/阻断调整，不能用可变事件重定向目标；提交后每个监听器获得隔离快照。ICombatUnit返回实际伤害、治疗和击杀；兼容IUnit无法报告裁剪/过量且明确拒绝NonLethal。塔防适配器已实现精确边界，不支持HP的Tower返回未应用。

NonLethal保留至少1HP。HpLoss跳过增减伤、法术增强、防御及DamageCalculated/Taken/Dealt反应，只保留实际HP、Runtime通知和Death；免疫仍受显式flags控制。非法输入/未应用效果不会伪造成功事件。

BeginAttack/ResolveAttack/CancelAttack管理带AttackId及战斗代际的记录，一次攻击最多提交一次。塔普通攻击在发射时建记录、表现到达时结算，取消/注销/结算阻止迟到命中。Death在实际状态转换时只通知一次；Respawn恢复Intrinsic。死亡不等于注销：当前RemoveUnit仍按既有契约清理全部来源引用，要求对象彻底注销后DoT继续时需另加来源快照，不能直接删清理判断。

线性弹在射程内扫掠并按路径距离命中，每目标一次。追踪弹到达后终止，默认maxLifetime=10秒；trackingTargetPolicy=0保持追踪，1重新过滤。到达无敌目标的逻辑是显式政策。销毁事件只有一个容器入口。

IProjectilePresentation提供位置更新/Stop句柄，具体资源由TdPresentation负责。异步加载迟到和加载/移动回调重入也必须释放；Modifier继续用IPresentationHandle。Core不引用具体表现或资源服务。

## 定义、JSON与发布

正式运行入口仍是skill.xlsx/skill_action.xlsx/skill_modifier.xlsx→ExcelProvider。Ability*.xlsx为空且未接入，不能误认为直接填表即可运行；不手改Luban生成物，重新生成用Data/gen_all.bat。

JSON用单次严格CLR读取，DTO公共字段作为schema，省略保留显式默认值；未知/重复字段、错误类型/大小写和非有限数值报错，不依赖JsonUtility静默忽略。所有Provider合并为统一图，最终检查名称/StableId、引用、立即创建环、SpecialValue、等级、充能、动作/目标/事件组合和能力支持。ApplyTo总会复验并深复制后原子替换；有活运行时则拒绝。Initialize只管理服务/运行时，配置重载使用独立替换API。旧Definition访问保留兼容，运行时定义按只读契约使用，不作为编辑入口。

requiresCustomScript必须有实际工厂。Validation是当前图报告，LastApplyValidation含引擎发布失败原因，修正后可重试。跨Provider Projectile可用精确名称解析。

新JSON字段：onToggleOn/onToggleOff；auraIncrementsStacks；maxLifetime/trackingTargetPolicy；Action.valueSource。valueSource=0等级/特殊值，1实际事件伤害，2实际治疗，3事件Value，事件量乘配置Value/SpecialValue因子。目标Parent=5、EventSource=6、EventTarget=7；事件引用仅允许在匹配Trigger使用。

`onProjectileHit` 是所属技能的命中动作，施法生成弹体后不会立即伤害，多技能弹道互不串触发。Modifier 的 `triggerExcludedDamageFlags` 可过滤 Reflected 等标志，避免反伤依靠熔断结束。九种JSON组合技能、混合来源及外部脚本连锁闪电见 `Data/AbilityJsonSources/Validation`，场景测试直接加载同一文件。

严格读取的DTO通过link.xml保留反射构造/字段，避免IL2CPP剥离；实际目标平台仍需构建验证。

Initialize前可显式ConfigureJsonSources(manifestAssetPath)。manifest schemaVersion=1，sources项包含enabled/完整assetPath，从ResourceManager已配置位置加载。手写资产不能放生成的Assets/Data/Json；启用项缺失初始化失败，禁用项不加载。不扫描示例目录、不假定YooAsset收集配置存在。

## 当前项目业务接入（非包依赖）

Cube 可选择 AbilityKey（名称/StableId）、战斗session句柄、AbilityStatus和业务DTO。授予、升级、查询、施法、停止分离；TryGet不创建/改等级。CastId/Queued及CommandUpdated连接回执和后续结果。业务直接依赖包的公开类型同样是有效接入，不要求再套一层“不认识技能类型”的接口。Actor绑定与UI选择哪些技能属于项目职责。

包的 EvaluateOrder 与真实施法共享预校验，不触发 OrderIssued、前摇钩子、ID分配、支付或队列变更。CastFilter/getter须遵守只读扩展契约。CooldownDuration记录本次启动的时长；ChargeRestoreRemaining提供恢复倒计时。

JSON消耗通过业务授予时资源绑定提供，技能栏、本地化/图标和Tower模板仍由业务配置决定。来源无关接口不代表旧Excel TowerLevel已支持字符串键，不能私改生成列或把JSON伪装数字表行。

旧Excel技能只有常量数值，兼容入口默认技能等级1，不再把Tower.Level隐式当技能等级。需要等级成长时，使用JSON/代码的MaxLevel与按级数值，并通过授予/升级接口显式设置；不要虚构MaxLevel=99或手改生成表类。

## 当前能力边界

| 能力 | 处理 |
| --- | --- |
| AutoCast、StackIndependent、Projectile Vision | 没有对应统一策略/独立到期/视野服务，配置Error |
| NoUnitCollision、Muted、负HealthRegen | 必须显式脚本负责碰撞/物品/生命流失策略 |
| 强弱驱散、状态抗性、完整装备/技能窃取 | 尚非当前简化运行时承诺，须独立设计验收 |

Hidden默认禁止普通订单，受控脚本可AllowHidden；OutOfGame不作为合法目标，Hexed/Disarmed等已有动作消费者。未支持能力不能静默成功。

## 编辑器与验证

Workbench检查器/表单/快照沿用现有管线；快照查询脚本真实属性/状态。项目级Excel+全部JSON+业务绑定加载和资源收集仍需配置验收；完整节点编辑器继续延后。

新增EditMode回归覆盖订单、仲裁/队列、异常重入、计时/再生、独立Modifier/光环、预算/清理、结算、攻击/死亡、投射物和严格发布。便携.NET源码宿主使用最小Unity/NUnit兼容层，只验证这些源码断言和编译，不等于Unity Test Runner。

包内现按单位建立Modifier索引并用成员集合判断登记；事件只派发给实际监听者，自定义脚本保持原注册顺序及全局事件契约。快照池保持嵌套隔离与异常释放，执行作用域是值类型；只对内置静态读取跳过上下文分配，不缓存动态结果。

`Tools/AbilityVerification/Performance` 提供完整基准、环境、源码指纹和原始前后数据。1000单位×3个Modifier的合成查询+更新由36.849ms/48,528,104B降至0.543ms/24B，所有优化后场景无执行预算截断。它使用固定网格世界替身、Release/.NET8、关闭分层JIT以稳定对比，不是Unity帧率；事件/光环基线有截断，不能用其宣称严格等价加速。

发布前必须在Unity2022实际执行EditMode/PlayMode、异步表现、真实场景和IL2CPP验证，并在目标设备用Profiler测CPU/GC。项目自己的IWorld若线性扫描全部角色，仍需按规模实现空间索引；真实渲染、资源和物理成本未包含在合成结果中。
