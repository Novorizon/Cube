# Tile Art Production Plan

目标：先达到用户参考图中轻度、卡通、柔和、商业化的地块效果。当前阶段不做相邻融合、不做 Shader 过渡、不做 5 片/9 片切分、不做 `TopSurface` 或 `TopicTop` 顶面覆盖层。

## 当前结论

第一阶段只做这件事：

```text
完整基础地块 prefab
+ 自动顶部装饰 prefab
+ 统一光照和材质
```

基础地块直接相邻也要舒服：

```text
Grass / Water / Snow / Hill / Road
每种地块单独好看
直接拼接不难看
不依赖邻居规则才好看
```

## 地块结构

当前推荐结构：

```text
TileRoot
├── Topic
│   └── TopBody
└── Base
    ├── Soil
    └── Rock
```

规则：

```text
尺寸：1 x 1 x 1
原点：中心底部
外轮廓：规则方形，可稳定拼接
边缘：可以圆角、柔和、有固定风格
相邻：不查邻居，不切片，不动态融合
```

注意：

```text
不要 TopSurface
不要 TopicTop
不要 5 片 / 9 片
不要 Shader 过渡作为第一阶段方案
```

## 顶部细节

顶部花草、石子、草簇等不烘进基础地块模型，改为自动装饰：

```text
Grass -> small flowers / grass clumps / subtle stones
Water -> lily pads / reeds / stones
Snow -> snow lumps / dead grass / blue snow marks
Hill -> pebbles / dry grass
Road -> pebbles / footprints / cracks
```

当前测试资源：

```text
Assets/Arts/Map/Tiles/Generated/Decorations/GrassClump_A.prefab
Assets/Arts/Map/Tiles/Generated/Decorations/SmallFlower_A.prefab
Assets/Arts/Map/Tiles/Generated/Decorations/Pebble_A.prefab
```

## 当前 Grass 原型

当前测试资源：

```text
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile.blend
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile.fbx
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_preview.png
Assets/Arts/Map/Tiles/Generated/ReferenceStyleGrassTile_Test.prefab
```

`ReferenceStyleGrassTile_Test.prefab` 是测试版，没有覆盖原始 `Grass.prefab`。

当前 Blender 生成脚本：

```text
Tools/Art/Blender/create_reference_grass_tile.py
```

该脚本已按当前方案修正：

```text
Topic 下只有 TopBody
没有 TopSurface
没有 TopicTop
没有分片顶面
```

## 当前 Grass 顶面贴图

第一版顶面贴图已经生成：

```text
Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_Albedo_AI_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_Albedo_Tileable_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_Height_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Grass_Top_Normal_1024.png
```

生成与处理脚本：

```text
Tools/Art/Texture/BuildGrassTopTextures.ps1
```

使用原则：

```text
TopBody 的基础质感由 Albedo + Normal 承担
自动装饰只负责稀疏的小花、草簇、石子
隐藏自动装饰后，顶面本身也应该舒服
```

## 当前 Snow / Road 顶面贴图

Snow 第一版：

```text
Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Albedo_AI_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Albedo_Tileable_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Height_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Snow_Top_V1_Normal_1024.png
Assets/Arts/Map/Tiles/Materials/SnowTop_Stylized.mat
```

Road/Sand 第一版：

```text
Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Albedo_AI_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Albedo_Tileable_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Height_1024.png
Assets/Arts/Map/Tiles/Textures/Generated/Road_Top_V1_Normal_1024.png
Assets/Arts/Map/Tiles/Materials/RoadTop_Stylized.mat
```

通用顶面 shader：

```text
Assets/Arts/Map/Tiles/Shaders/TileTopSoftLit.shader
```

Grass / Snow / Road 共用这个 shader；Water 后续单独做水面 shader。

## Unity 工具

当前工具菜单：

```text
Tools/Map Art/Reference Grass/Full Setup And Preview
Tools/Map Art/Reference Grass/Create Prototype Assets
Tools/Map Art/Reference Grass/Use Prototype As Grass
Tools/Map Art/Reference Grass/Restore Original Grass
Tools/Map Art/Reference Grass/Create 6x6 Preview Grid
```

`Full Setup And Preview` 会：

```text
1. 从 FBX 生成测试地块 prefab
2. 从装饰 FBX 生成装饰 prefab
3. 临时把 MapTilePrefabConfig 中 Grass 指向 ReferenceStyleGrassTile_Test.prefab
4. 在当前场景创建 ReferenceGrassTilePreviewGrid 预览根节点
```

还原方式：

```text
Tools/Map Art/Reference Grass/Restore Original Grass
```

## 为什么不做相邻过渡

参考图和效果图都说明，舒服感主要来自：

```text
单块质量
统一颜色
柔和圆角
层次清楚的侧面
适量装饰
统一光照
```

不是来自复杂过渡规则。

所以当前阶段不做：

```text
邻居查表
边/角资源切换
顶面切片
Shader 融合
波浪形轮廓
```

## 下一步

先在 Unity 中查看：

```text
ReferenceGrassTilePreviewGrid
```

重点判断：

```text
1. 单块 Grass 是否够舒服
2. 6x6 重复后是否明显单调
3. 自动装饰密度是否合适
4. 顶面、土层、岩层比例是否符合参考图
5. 是否先继续扩展 Water/Snow/Hill，还是继续精修 Grass
```
