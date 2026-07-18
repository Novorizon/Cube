# 0003 Map Object Vs Marker

## 结论

`MapData.Objects` 表示地图上真实存在、需要实例化或影响占用和交互的对象。

纯 UI 标记、导航、任务提示、商店图标等表现数据，后续应进入 `MapMarkerData` 或等价结构，不要塞进 `MapData.Objects`。

## MapData.Objects

适合：

```text
Decoration
Resource
Building
Interactable
```

这些对象可能需要：

```text
创建场景视图
注册地图占用
参与采集或交互
存档同步
```

运行时规则：

```text
MapData.Objects 会进入 MapManager 的 objectsByCoord 索引
寻路和建造占用看 objectsByCoord，不看 view 是否隐藏
逻辑上消失的对象应 TryRemoveMapObject + MarkMapObjectRemoved
只是 UI 提示或导航点的内容不要放入 MapData.Objects
```

如果对象以后会回来，或只是阶段性隐藏，应使用运行时状态和存档状态表达，不要把纯显示隐藏当成地图逻辑删除。

## MapMarkerData

建议字段：

```text
MarkerId
Type: Shop / Building / Quest / NpcSpawn / Teleport / Custom
Coord 或 X/Y/Z
Icon
VisibleDefault
RelatedQuestId
RelatedNpcId
```

它服务小地图、大地图、导航和任务提示，不一定生成场景对象。








