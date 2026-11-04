# Game Lifecycle

游戏入口负责从启动到停止的生命周期。玩法内容继续放在原有项目代码中；`Framework` 的资源、输入和 UI 接口保持原样。

## 边界

| 代码 | 职责 |
| --- | --- |
| `Bootstrap/Runtime` / `Game.Bootstrap` | 无 Unity 依赖的状态、启动门控、取消协调和一次性清理 |
| `Bootstrap/GameEntry.cs` | Unity 回调和单个应用实例的所有权，保留原脚本 GUID 与初始地图序列化字段 |
| `Bootstrap/CubeGameLifecycle.cs` | Cube 的组合根，明确写出初始化、更新、进入世界与清理顺序 |

不要求每个 Manager 实现模块接口，不自动扫描注册，不通过消息分发每一帧更新。PureMVC 可按项目需要用于业务交互组织，生命周期驱动不依赖它。

## 状态与契约

```text
Created -> Starting -> Running -> Stopping -> Stopped
               |          |
               +--失败-----+--> Stopping -> Failed
```

- 同一个 `GameApplication` 只运行一次。Starting/Running 中重复 Start 返回同一任务；停止后需要创建新实例。
- `StartAsync()` 成功后才允许 Tick。启动失败会先清理，再把原异常交给调用者。
- `StopAsync()` 立即进入 Stopping。重复调用返回同一任务；停止过程中不能再进入 Tick。
- 生命周期回调及 Start/Tick/Stop 必须从同一拥有者上下文调用，Unity 中即主线程；不支持后台线程并发调用。异步等待保留同步上下文。
- 不可取消的资源/UI 操作需要先结束，才能安全清理。取消令牌与等待后的检查负责阻止后续组装。框架不会把“停止等待”冒充“底层操作已结束”。
- Tick 内请求停止时，先让当前回调退出，再清理；取消和清理回调重入 Stop 也不会重复释放。
- Shutdown 可以收到 Requested、StartupFailure、RuntimeFailure。项目据此决定是否保存。
- Stop 是尽力完成清理的操作，清理异常通过 `LastError` 与诊断回调报告，结束状态为 Failed。启动异常仍由 StartAsync 抛出。

调用者必须观察 StartAsync 的结果。Unity 中只有事件边界 `GameEntry.Start` 使用 async void，并捕获异常；核心与项目接入使用 Task。

## Cube 启动顺序

```text
设备与资源
  -> 输入保持 UI 模式、相机、配置和项目管理器
  -> 恢复存档，明确区分缺档与恢复失败
  -> 地图表现配置与场景对象
  -> 等待经营主面板加载并刷新
  -> 初始剧情与存档会话就绪
  -> Running，开始项目 Tick
```

相机、必要配置、能力适配、地图或主界面初始化失败会终止启动。当前初始地图仍由 GameEntry 的 `startupWorldMapId` 决定。

`MapManager.LoadWorldMapAsync` 是启动使用的可等待入口；保留 `LoadWorldMap` 供原有同步调用点使用。新入口不会同时发起第二次主界面加载，等待中保持 UI 输入模式。角色实际生成前保存时，会回退到已缓存的玩家存档。

`GameCleanupScope` 逆序执行已登记动作，单项失败不会跳过剩余动作。对可能部分完成的初始化，应先登记对部分状态也安全的清理动作，再开始初始化。项目没有释放契约的外部共享服务不由核心擅自销毁。

## 存档会话门控

`Initialize -> LoadSession -> BeginSession -> Save -> EndSession`

- LoadSession 返回 NotFound、Loaded 或 Failed；恢复失败保留旧文件并终止启动。
- BeginSession 只接受成功恢复或缺档的新会话，在世界和主界面就绪后调用。
- Save 和自动保存都要求 SessionReady。启动一半、读档失败、会话关闭后不会写入文件。
- 正常停止时先保存，再关闭写入权限并清理。启动或运行故障停止时不额外保存故障状态。
- 编辑器主动删除存档时，原有“本次会话禁止保存”的规则继续生效。
- 该门控不提供存档版本迁移、业务恢复事务或持久化算法替换；这些仍由存档模块负责。

Unity 正常退出时同步完成已经运行的会话清理；启动尚在异步等待时不能要求 Unity 退出回调阻塞进程。日常自动保存仍需持续运行。若项目接入移动端后台切换，应在项目层增加对应的检查点策略。

## 复用

复制 Runtime 程序集，实现 IGameLifecycle，并由目标引擎/平台的宿主转发回调即可。无需带入 CubeGameLifecycle、世界地图或存档实现。新的项目实现应显式确定自己的构造顺序、停止策略和会话重置规则。

## 验证

```sh
dotnet run --project Tools/BootstrapTests/BootstrapTests.csproj --configuration Release
```

该离线验证器直接编译本次 Runtime、GameEntry、CubeGameLifecycle、地图进入与存档源文件，使用 .NET 8 SDK 内置 Json.NET，不下载 NuGet 包。Unity、资源和业务边界使用测试替身；它验证生命周期时序与存档文件保护，不代替 Unity/YooAsset 的实际运行验证。

Unity EditMode 核心测试位于 `Assets/Tests/EditMode/Bootstrap`。在 Unity 2022.3 中还应验证：实际资源异步加载、主界面加载失败、加载期间停止 Play Mode、正常退出保存，以及场景/Prefab 上 GameEntry 引用未丢失。
