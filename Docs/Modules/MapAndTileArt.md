# Map And Tile Art

本文记录地图美术当前方向。地图运行时数据见 `Docs/Architecture/MapRuntime.md`。

## 当前结论

第一阶段不要做复杂邻接融合。

当前方向：

```text
完整基础地块 prefab
+ 自动顶部装饰 prefab
+ 统一材质和柔和光照
```

目标：

```text
单个地块好看
直接相邻也能接受
不依赖邻居规则才好看
先完成可用美术资源，再考虑过渡系统
```

## 暂不继续的旧方案

第一阶段不要继续：

```text
TopSurface
TopicTop
Shader 邻接融合
5-slice / 9-slice 地块切片
按邻居动态换边缘
大量一次性生成菜单工具
```

## 地块结构

推荐结构：

```text
TileRoot
  Topic
    TopBody
  Base
    Soil
    Rock
```

规则：

```text
尺寸：1 x 1 x 1
外轮廓：规则方形，保证稳定拼接
顶部：可以圆角、柔和、有固定风格
细节：靠自动装饰补，不烤进基础模型
```

## 顶部装饰

顶部装饰使用独立 prefab：

```text
Grass -> grass clump / flower / pebble
Water -> lily pad / reed / stone
Snow -> snow lump / dead grass
Hill -> pebble / dry grass
Road -> pebble / footprint / crack
```

当前测试资源：

```text
Assets/Arts/Map/Tiles/Generated/Decorations/GrassClump_A.prefab
Assets/Arts/Map/Tiles/Generated/Decorations/SmallFlower_A.prefab
Assets/Arts/Map/Tiles/Generated/Decorations/Pebble_A.prefab
```

## Grass 原型

```text
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile.blend
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile.fbx
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_preview.png
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_Test.prefab
```

生成脚本：

```text
Tools/Art/Blender/create_reference_grass_tile.py
```

`ReferenceStyleGrassTile_Test.prefab` 是测试 prefab，不是最终 Grass prefab。

## 材质方向

```text
TileTopSoftLit.shader
GrassTop_Stylized.mat
SnowTop_Stylized.mat
RoadTop_Stylized.mat
WaterTop_Stylized.mat
```

目标：

```text
柔和
卡通
商业化
不要过度写实
不要强烈噪声
不同地块颜色要区分，不要一片单调绿
```

## 注意

- 自动装饰要低密度，不能遮住地块本体。
- 基础 tile 本身必须好看，装饰只是加分。
- 地图编辑器和运行时都应以当前正式 prefab 为准。
- 不要为了测试资源直接覆盖正式地块，先做预览确认。








