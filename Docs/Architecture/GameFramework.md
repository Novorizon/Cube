# 可扩展客户端框架设计

状态：架构方案。现有生命周期代码已实现；本文的 HybridCLR、资源接口和更新管线尚未实现，也没有安装新的 SDK。目录和接口名称用于定义边界，落地时需与现有 Framework 的公开接口对齐。本文不评审或修改 Framework 内部实现。

## 1. 目标与首期选择

支持同一套启动流程使用内置业务或 HybridCLR 热更业务，按项目选择资源库，保持生命周期、失败处理和资源所有权一致。核心只保留必要的应用机制，UI、音频、输入和具体玩法通过项目组合根接入。

首期选择：

- 沿用项目已有的 YooAsset 2.3.18 作为第一个正式资源后端，不在接入时顺便升级。
- 用本地 Resources/Editor 模式验证替换边界；它们不需要假装具备远程更新能力。
- 提供 BuiltIn 与 HybridCLR 两种代码入口；Editor 默认走静态入口，另提供显式的 DLL 加载测试模式。
- Addressables 作为后续独立可选适配包。未安装的 SDK 不应成为核心程序集的编译依赖。
- 固定并验证 Unity、HybridCLR、资源 SDK 和目标平台组合，再决定首个发布基线；不把不同版本文档中的 API 混用。

## 2. 层次与程序集依赖

```text
Unity Player / AOT 启动场景
  GameEntry：Unity 回调、基础设施 Pump、应用所有权
    -> GameApplication：应用状态、取消、失败和停止协调
      -> ClientBootstrapLifecycle：AOT 启动编排
        -> 本地启动 UI、版本选择、下载和内容验证
        -> ICodeLoader：创建业务入口工厂
          -> IGameLifecycle：本地或 Hotfix 的业务生命周期

稳定 AOT Contracts <--- Core / Unity 桥接 / 各适配包 / Hotfix
资源服务接口       <--- YooAsset / Resources / Addressables 适配包
代码加载接口       <--- BuiltIn / HybridCLR 适配包
```

建议的程序集职责：

| 层 | 内容 | 编译约束 |
| --- | --- | --- |
| Core | 现有 Game.Bootstrap、生命周期与取消机制 | 无 Unity、HybridCLR、资源 SDK 依赖 |
| Contracts | 入口工厂、版本描述、只读上下文等稳定协议 | AOT，不能引用 Hotfix 或某个 SDK |
| Unity Contracts/Bridge | 资源、实例、场景等 Unity 类型接口和宿主 | 可引用 Unity，不能引用业务 |
| Bootstrap | 本地启动 UI、更新编排、启动配置 | AOT，不静态引用热更入口类型 |
| Adapters | 每个资源库、代码运行时、传输实现 | 各自独立程序集/可选包，只在这里引用 SDK |
| Hotfix | 业务入口及需要热更的项目代码 | 可引用稳定协议，不能被 AOT 静态反向引用 |
| Editor/Build | 编译、裁剪、资源构建、版本发布和验证 | 仅 Editor/构建环境 |

含 UnityEngine.Object/Scene 的接口放在 Unity 合同层，保持纯 Core 无引擎依赖。适配器由组合根显式创建并注入，启动完成后配置冻结；首期只支持启动时选择后端，不支持运行中任意切换已加载资源的后端。

现有 CubeGameLifecycle 位于默认项目程序集，许多业务类型还在 Assembly-CSharp。增加 DLL 加载器不会自动使它们可热更。迁移时必须先建立 AOT 服务/协议程序集，再整理 Hotfix 的 asmdef 依赖；命名程序集不能直接依赖 Assembly-CSharp 来绕过边界。

## 3. 启动编排

```text
读取包内 BootProfile，显示 AOT 启动 UI
  -> 初始化选定资源后端和更新传输
  -> 获取发布描述，校验来源、平台与主包兼容性
  -> 固定一个 releaseId，生成下载计划
  -> 下载到候选区，验证完整性和剩余空间
  -> 准备该版本资源目录/资源包
  -> 补充匹配主包基线的 AOT 元数据
  -> 按依赖顺序加载热更程序集
  -> 从入口工厂得到 IGameLifecycle
  -> 初始化项目、恢复会话、等待首个界面就绪
  -> Running，记录该版本启动健康
```

启动 UI、错误提示、下载确认和重试按钮使用包内 AOT 代码与资源，不能依赖尚未加载的热更程序集。下载代码文件可以发生在加载程序集之前；带热更脚本的 Prefab/Scene 必须等对应程序集加载后再反序列化。

应用状态继续使用 Created/Starting/Running/Stopping/Stopped/Failed；检查版本、下载、补元数据等是 Starting 内的启动阶段，用于进度和错误定位，不再创建另一套应用状态。

基础设施 Pump 必须根据服务契约在 Starting/Stopping 中继续工作，以推进下载、主线程调度和异步收尾。只有业务 Tick 受 Running 门控；不能把需要持续推进的下载器全部放进被门控的业务 Tick。

## 4. 热更新与入口边界

以下为接口形状示意，未作为本轮运行时代码实现：

```csharp
public interface ICodeLoader
{
    Task<IGameEntryFactory> LoadAsync(VerifiedCodePackage package, CancellationToken token);
}

// 该接口及其使用的上下文类型由 AOT 保存。
public interface IGameEntryFactory
{
    IGameLifecycle Create(GameContext context);
}
```

- BuiltInCodeLoader 返回静态工厂；HybridClrCodeLoader 获取同一发布版本的代码文件、补元数据、加载程序集并创建工厂。
- 资源文件读取通过注入的内容读取接口完成，HybridCLR 适配器不直接依赖 YooAsset。
- 入口只在创建工厂时解析一次，之后通过稳定接口调用，不按帧反射或发送全局通知。
- Editor 静态入口与动态加载测试采用明确隔离的配置；不能在已经加载同名业务程序集的 Editor 会话里重复 Assembly.Load 来证明热更新可用。
- GameContext 以只读、明确的服务接口提供依赖，避免依赖任意字符串查询的全局服务表。
- 合同程序集固定在 AOT，热更包不得重复携带并加载另一份合同程序集。合同不兼容应要求更新主包。
- AOT 泛型、反射入口与裁剪保留纳入构建验证；不能通过 Editor 能运行推断 IL2CPP 正确。
- 代码与补充元数据字节在加载接口使用完后释放，不长期保留额外副本。

默认采用普通热更新加载模型：第一次不可逆的元数据/程序集加载前完成候选验证；进入加载后若版本不能启动，记录恢复目标，下一次启动选择兼容的已知健康版本。首期不承诺同进程卸载 DLL、加载另一个版本或完整热重载。

若选择官方独立的热重载产品，作为另一种代码运行时能力接入，并另外验证引用释放、异步任务、回调、静态状态和卸载条件，不能仅给普通加载器增加一个 Restart 按钮。

## 5. 资源接口必须统一的语义

统一资源使用契约，不暴露 AssetHandle、AsyncOperationHandle 等 SDK 类型给上层：

| 接口/对象 | 职责 |
| --- | --- |
| IAssetLoader | 异步加载类型化资源，返回 IAssetLease<T> |
| IAssetLease<T> | 持有一次有效的资源引用；Dispose 幂等，释放该引用而非强制卸载全局资源 |
| IInstanceService / IInstanceLease | 实例化和销毁；明确实例存活期间由谁保留 Prefab 依赖 |
| ISceneService / ISceneLease | 加载、激活、卸载场景；异步操作完成后才报告释放完成 |
| IContentSource | 读取 DLL、配置等内容文件，明确字节/文件句柄的所有权 |
| IContentUpdater | 查询计划、下载候选内容和报告下载字节进度；作为独立可选能力 |

必须明确以下规则：

1. 标识使用 packageId + address，并在缓存/句柄内部固定 providerId、releaseId 和资源类型，避免不同版本混用。
2. 多个调用者加载同一资源，各自持有租约。释放一个调用者的租约不能使其他调用者失效。
3. 取消调用者等待不等于底层加载已停止。底层不能取消时，完成后应释放无人接收的结果，避免悬挂句柄。
4. Scene 卸载、实例销毁、资源引用释放采用明确顺序；释放租约不承诺 Unity 在同一帧立即归还全部内存。
5. 异步加载是共同入口；同步加载仅作为显式能力或已驻留缓存读取，不能以阻塞等待网络加载模拟所有后端都支持同步。
6. Resources/Editor 后端不支持远程内容更新时，在启动配置校验阶段明确报告能力不匹配。
7. 能力差异通过可选接口与启动校验表达，不用大量返回成功的空实现填满统一大接口。

YooAsset 和 Addressables 的引用管理细节留在各自适配包。消费者只管理合同中的租约与作用域，不接管 SDK 内部的 Bundle 引用计数。

## 6. 发布版本、兼容与恢复

一个 ReleaseManifest 协调代码、资源、配置和补充元数据。资源库自己的 catalog/manifest 由适配器解释，不直接充当整个游戏的发布协议。

| 字段组 | 作用 |
| --- | --- |
| releaseId | 一次完整发布的标识，启动后不可被“最新版本”查询偷偷替换 |
| playerCompatibilityId / minPlayerVersion | 与 AOT 主包、合同、IL2CPP/HybridCLR 基线是否兼容，何时要求整包升级 |
| target | 平台、架构和脚本后端 |
| resourcePackages | 每个资源包的后端、确切版本和目录校验信息 |
| assemblies | 程序集名称、文件哈希、依赖关系和入口类型 |
| aotMetadata | 对应已发布主包构建基线的补充文件及哈希 |
| saveCompatibility | 可读/写存档 schema，以及回退时的数据兼容策略 |
| authenticity | 发布描述签名及文件完整性校验信息 |

候选内容与当前健康内容分目录保存；下载和验证完成前不替换健康记录。候选被选中启动与确认健康是两个步骤，主界面及会话就绪后才确认。

断网时是否启动旧版本由 BootProfile/发布策略明确决定。没有兼容的本地版本时给出重试或主包更新路径，不使用半下载文件继续启动。校验损坏、磁盘空间不足、网络超时、主包不兼容和代码加载失败应有不同错误码与恢复动作。

SDK 可能需要先加载候选 catalog 才能计算下载计划。适配器要用独立的候选资源包/目录，或提供已验证的版本隔离策略；不能提前污染运行中的活动目录。无法隔离的后端应限制更新时机或显式声明不支持相应流程。

普通加载模式的回退发生在下一次进程启动；已加载程序集和主包内的 AOT/原生插件不靠替换 DLL 字节原地更新。资源版本回退也不等于用户存档自动倒退，写入新 schema 前需要兼容策略和备份。

## 7. 其他需要的框架能力

| 能力 | 首期职责 | 优先级 |
| --- | --- | --- |
| BootProfile 与平台能力 | 选择代码模式、资源适配器、环境/CDN、超时/重试和离线策略；不从任意服务器数据直接创建任意类型 | 必需 |
| 启动 UI 与错误恢复 | 本地进度、下载字节、错误码、重试/退出；展示阶段而非伪造总百分比 | 必需 |
| 作用域与异步任务所有权 | 应用/会话/场景持有资源、订阅和任务；停止产生新操作，等待收尾，再逆序释放 | 必需 |
| 主线程执行与时间语义 | Unity 调用的主线程边界；区分受缩放时间、未缩放时间和真实 UTC | 必需 |
| 诊断 | 记录阶段耗时、releaseId、包版本、失败原因和未释放租约，方便定位启动问题 | 必需 |
| 存储边界 | 保留会话写入门控；逐步增加原子写入、备份、schema 迁移与发布回退兼容 | 必需 |
| 构建与发布工具 | 生成热更 DLL、匹配 AOT 元数据、资源包、完整发布描述，校验产物关联 | 必需 |
| HTTP/下载传输 | 限制并发与重试，支持候选下载与进度；CDN 切换仍校验同一内容 | 更新模式必需 |
| UI/音频/输入/对象池 | 使用统一资源合同及作用域，与已有 Framework 能力衔接 | 按已有模块接入 |
| 长连接、登录支付、埋点 SDK | 项目需要时增加独立适配器；不成为离线启动核心的强制依赖 | 可选 |

现有同步 Shutdown 适用于目前的同步清理；接入确实需要异步卸载的后端时，应演进显式异步停止/作用域协议，等待场景、任务和句柄收尾，不能用 Forget 宣称已完成释放。Unity 退出回调本身不能保证给任意异步工作足够时间，重要保存仍需要检查点策略。

首个包含热更新与资源适配的正式主包发布前，应完成上述停止协议并冻结合同基线，避免上线后再为补齐基本释放语义而破坏 AOT 合同兼容性。

## 8. 构建管线与验收

主包构建固定 Unity/HybridCLR/目标平台/合同版本，保留该构建对应的裁剪 AOT 文件。后续热更发布使用所选主包基线的补充文件，不随意用另一台机器刚生成的 DLL 替代。不同目标平台的产物分别归档。

发布工具至少负责：编译热更程序集、生成官方要求的裁剪/桥接相关产物、排除应动态加载的程序集、构建资源包、检查依赖闭环和入口、生成校验信息、验证 code/content/config 的同版关系、归档符号与产物。

验收必须包含真实 IL2CPP Player：

- BuiltIn + 本地后端、HybridCLR + YooAsset 跑相同的项目生命周期入口。
- 没有安装某个 SDK 时，核心和其他已选适配包仍可编译。
- 多程序集依赖、AOT 泛型/async、带热更脚本的 Prefab/Scene 正确运行。
- AOT 基线或平台不匹配时，在代码加载前拒绝该发布。
- 下载中断可恢复，损坏候选不替换健康版本，代码加载后的失败不在同进程盲目重试另一版本。
- 启动/场景加载中停止，重复加载与取消后完成的操作均不泄漏租约。
- Editor 验证和离线测试保留，但不替代 HybridCLR/YooAsset/Addressables 的真机验证。

## 9. 对现有代码的实施顺序

1. 保留已经实现的生命周期核心，明确 ClientBootstrapLifecycle 与项目入口的边界；不扩大为自动模块发现系统。
2. 定义并验证资源租约、实例、场景与内容读取协议，先实现本地后端，再实现 YooAsset 2.3.18 适配；逐步桥接现有同步 ResourceManager 调用。
3. 整理 AOT 合同和服务程序集依赖，接入 BuiltInCodeLoader 与 HybridCLR，迁移一个最小项目入口/测试模块，验证 IL2CPP 全链路。
4. 接入统一发布描述、候选下载、启动健康确认和下次启动恢复，并完成构建工具。
5. 根据实际项目需要增加 Addressables 和平台 SDK 适配，不预先实现所有第三方库封装。

## 官方依据

- [HybridCLR 加载和运行](https://www.hybridclr.cn/docs/basic/runhotupdatecodes)：字节加载、程序集依赖顺序及 AOT 接口调用热更入口。
- [HybridCLR AOT 泛型](https://www.hybridclr.cn/docs/basic/aotgeneric)：补充元数据与主包构建、平台的关联。
- [HybridCLR 打包工作流](https://www.hybridclr.cn/docs/basic/buildpipeline)：编译、裁剪、桥接与产物生成。
- [HybridCLR 热重载产品](https://www.hybridclr.cn/docs/business/reload/quickstart)：程序集卸载与重新加载属于需要独立接入、验证的能力。
- [Addressables 内存管理](https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/MemoryManagement.html)：加载与释放引用的生命周期责任。

上述是技术依据；本文的分层、版本协议、错误恢复和实施顺序是针对本项目提出的设计。
