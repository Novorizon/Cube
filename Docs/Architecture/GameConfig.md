# GameConfig

`Assets/Scripts/Game/Config/GameConfig.cs` 是项目级、开发阶段可调整参数的统一代码入口。当前先采用强类型 C# class，不使用横向 Excel 常量表。

## 命名边界

```text
GameConfig    开发者定义的游戏规则、数值比例和表现参数；运行时只读
GameSettings  玩家或运行环境可修改并保存的选项，例如音量、语言、画质
```

当前项目使用 `GameConfig`。只有以后实现玩家设置菜单及其存档时，才新增 `GameSettings`；不要用 `GameSetting` 代替项目级规则配置。

## 当前分类

```text
GameConfig.Story     对话进度显示方式、静态插画镜头取景范围
GameConfig.World     农田预览高度、网格和边缘表现参数
GameConfig.Calendar  一天时长、日历结构、昼夜时间点
```

新增参数时按业务域添加嵌套静态类和有单位含义的字段名。例如秒数使用 `Seconds`，相对地块尺寸使用 `InTiles`。

## 收纳规则

适合放入 `GameConfig`：

- 策划或开发阶段会反复调整的全局数值。
- 多个模块需要共享的规则或比例。
- 不值得单独制作数据表、但不应散落在业务代码里的表现参数。

继续留在所属模块：

- 算法不变量和只服务一段实现的局部常量。
- Prefab、Shader 属性等资源内部约定。
- Luban 生成的 Id、表数据和资源内容。
- 玩家可修改且要进入存档的设置。

业务模块可以保留原有公共常量作为兼容入口，但值应引用 `GameConfig`，避免形成第二份真相来源。
