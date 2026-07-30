# 管理地图小地图

## 当前范围

- 只接入经营/探索世界地图。
- 塔防小地图保持原实现，暂不修改。
- 小地图内容来自现有 `MapObjectData`，不增加独立 Marker 数据。
- 地图本体仍由 JSON 在运行时创建 Grid；固化的只是小地图 UI 结构，不是地图内容。
- 玩家、镜头可视范围和右键导航点属于运行时状态，不写入地图 JSON 或存档。
- 当前版本不实现战争迷雾，也不保存探索/揭示状态。

## 当前实现状态

- `MiniMapPanel.prefab` 已包含完整的固定 UI 层级和非空序列化引用。
- 玩家、导航及四种物体类型的全局默认图标已绑定临时资源，后续可在根组件替换。
- 地图底图、对象图标、玩家位置、镜头范围及导航位置继续在运行时生成或刷新。
- 地图对象图标继续使用对象池，不需要也不允许为小地图单独维护一份 Marker 列表。
- 小地图已接入 `UIManager.Instance.Viewport`，会根据当前设备视口切换尺寸并重新投影内容。

## 数据归属

### 地图实例

`MapObjectData.MiniMapVisibility` 提供三态覆盖：

- `Inherit`：继承对象类型配置。
- `Show`：该实例强制显示。
- `Hide`：该实例强制隐藏，优先级最高。

旧地图没有该字段时会按 `Inherit` 读取，不需要迁移。

### 装饰物

`MapDecorationPrefabConfig.DecorationPrefabItem` 保存：

- `ShowOnMiniMap`
- `MiniMapIcon`

这两个字段属于装饰物资源定义，直接由地图编辑器或配置资产 Inspector 编辑。

### 资源与建筑

`resource.xlsx` 和 `world_building.xlsx` 已增加并经 Luban 生成：

- `showOnMiniMap: bool`
- `miniMapIconLocation: string`

运行时直接读取生成后的强类型字段：

- `showOnMiniMap` 控制类型默认显示。
- 优先使用 `miniMapIconLocation`。
- 没有专用图标时回退到现有 `iconLocation`。
- 没有任何内容专用图标时，先使用小地图根组件配置的物体类型默认图标。
- 类型默认图标也为空时，最后使用类型颜色块。

不要只改 `Data/Defines/*.xml` 而不改 Excel 表头，否则 Luban 生成会失败。应在工作簿和 XML 定义同时完成后，再运行 `Data/gen_client.bat`。

## 可见性优先级

从高到低：

1. 实例 `Hide`
2. 当前追踪任务目标强制高亮
3. 实例 `Show`
4. 类型配置默认值

失效或已被移除的地图对象不会显示。资源对象通过 `MapManager` 移除后，小地图图标会同步回收。

## 运行时表现

- 底图由 `MapData.Cells` 一次生成一张点采样纹理，不为每个格子创建 UI 节点。
- 底图表现包含高度明暗、坡差/悬崖边线、水岸、道路边缘及稳定的地表色差。
- 对象图标来自现有地图对象，并使用对象池复用。
- 玩家、镜头范围和导航点以 10 Hz 更新。
- 北方固定朝上。
- 左键点击小地图：切换为自由镜头并聚焦目标位置。
- 右键点击可行走格：让玩家寻路到目标，并显示临时导航点。
- 当前追踪任务如果目标对应现有建筑或交互对象，只高亮该对象，不生成额外 Marker。

### 全局表现配置

`ManagementMiniMapPanel` 根组件保存与具体地图内容无关的表现配置：

- 玩家朝向图标
- 导航目标图标
- 装饰物、资源、建筑、交互物的类型默认图标
- 玩家和导航图标尺寸、对象默认图标缩放
- Compact / Normal / Wide 面板尺寸和边框宽度

内容专用图标仍由装饰物配置或 Excel 提供。根组件的类型默认图标只负责兜底，不写入地图
JSON。

物体最终使用的显示资源按以下顺序解析：

1. 物体类型配置中的专用图标
2. `ManagementMiniMapPanel` 根组件中的类型默认图标
3. 类型颜色块

类型颜色块不是额外的图片或 Marker。它仍然使用对象池中的同一个 `Image` 节点，只是在没有
可用 Sprite 时将 `sprite` 设为空并使用类型颜色。

## 地图编辑器

地图编辑器提供：

- 装饰物默认显示开关与默认图标。
- 装饰物和资源放置时的 `Mini Map Override`。
- 选中格子内每个对象的 `Mini Map` 三态覆盖。
- 资源配置默认值和专用图标路径的只读预览。
- 右侧面板提供底图与可见对象预览、手动刷新和配置校验；对象点直接来自地图对象，不生成 Marker 数据。

地图导出仍只导出对象本身及实例覆盖，不维护第二份小地图对象列表。

## UI 接入

`WorldMainPanel` 会找到现有 `MiniMapPanel` 节点并确保管理小地图组件存在。正常运行时直接使用
`Assets/Arts/UI/Panels/World/MiniMapPanel.prefab` 中已经固化的以下层级：

```text
MiniMapPanel                         Image + ManagementMiniMapPanel
└─ MapViewport                      Image + RectMask2D
   ├─ BaseMap                       RawImage
   └─ IconRoot                      RectTransform
      ├─ IconTemplate               Image + Outline（禁用的对象池模板）
      ├─ CameraViewport             Image + Outline
      ├─ NavigationMarker           Image
      └─ PlayerMarker               Image
         └─ Forward                 Image（玩家图标为空时的朝向兜底）
```

这些节点和组件引用已经序列化到 Prefab。`EnsureRuntimeLayout()` 仍保留，但只用于兼容旧 Prefab、
引用损坏或测试场景；当所有引用有效时，它不会创建任何节点，只会应用全局表现配置。

固定与动态内容的边界如下：

| 内容 | 归属 | 创建方式 |
| --- | --- | --- |
| 裁剪区域、图标容器、模板、玩家、导航、镜头框 | UI Prefab | 编辑器中固定 |
| 玩家/导航/类型默认图标和尺寸 | Prefab 根组件 | Inspector 配置 |
| Grid、格子和地图对象 | 地图 JSON | 运行时创建 |
| 小地图底图纹理 | `MapData.Cells` | 绑定地图时生成 |
| 装饰物、资源、建筑、交互物图标节点 | `MapObjectData` | 运行时对象池获取和回收 |
| 玩家位置、导航位置、镜头范围 | 游戏状态 | 10 Hz 刷新 |

小地图订阅 `UIManager.Instance.Viewport.Changed`。屏幕宽高比、方向或安全区变化后，会重新选择
Compact / Normal / Wide 尺寸，并依次重新计算底图显示区域、对象图标、玩家、镜头范围、导航点
和点击投影。`MapData`、底图纹理内容和地图 JSON 不会因此改变。

## 编辑器操作

### 替换全局图标

1. 在 Unity 中打开 `Assets/Arts/UI/Panels/World/MiniMapPanel.prefab`。
2. 选中根节点 `MiniMapPanel`。
3. 在 `ManagementMiniMapPanel > Global Icons` 中替换：
   - `Player Direction Icon`
   - `Navigation Icon`
   - `Default Decoration Icon`
   - `Default Resource Icon`
   - `Default Building Icon`
   - `Default Interactable Icon`
4. 按需调整 `Player Icon Size`、`Navigation Icon Size` 和
   `Default Object Icon Scale`。
5. 保存 Prefab，并分别在 Compact / Normal / Wide 视口下检查显示。

这里只配置跨地图共用的图标。物体专用图标仍在装饰物配置或 Excel 中配置，不要在
`IconRoot` 下为每个地图对象手工增加节点。

### 响应式尺寸

根组件的 `Responsive Layout` 提供：

- `Compact Panel Side`
- `Normal Panel Side`
- `Wide Panel Side`
- `Viewport Border`
- Compact / Wide 的宽高比阈值

设备变化只会改变面板和 `MapViewport` 的显示尺寸，并触发重新投影，不会裁切、增删或修改
地图 JSON 内容。

### 重建固定层级

需要恢复或重新生成完整 UI 层级时，在 Unity 执行：

`Tools/UI/Rebuild Management Mini Map Prefab`

该命令会把 `MapViewport`、`BaseMap`、`IconRoot`、图标模板、玩家、导航和镜头范围节点写进
`Assets/Arts/UI/Panels/World/MiniMapPanel.prefab` 并绑定序列化引用，不会改塔防 prefab。
固化后固定节点可在 Prefab Mode 中直接预览和调整；地图对象图标仍然按实际对象数量动态池化。

重建器会重新创建整个 `MiniMapPanel.prefab`，不是增量修补。因此：

- 手工调整过的 Prefab 节点样式会被重建器默认值覆盖。
- 在根组件手工替换的全局图标也会恢复为重建器中的临时图标。
- 如果某项配置需要在以后每次重建后仍然保留，应同步修改
  `Assets/Scripts/Tools/Editor/ManagementMiniMapPrefabBuilder.cs` 中的默认值或资源路径。
- 只替换最终美术图标且不需要重建结构时，直接修改 Prefab 根组件并保存即可，不必执行重建命令。

## 后续统一塔防的边界

可复用：

- 坐标投影
- 底图纹理生成策略
- 图标池
- 图标样式/可见性解析接口

不直接复用：

- 塔、防守目标、敌人和路线等战斗语义
- 塔防交互和刷新节奏
- 塔防专属图标数据来源

统一时应共享底层投影和图标容器，不强行合并两种业务数据模型。
