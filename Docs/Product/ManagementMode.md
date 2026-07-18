# Management Mode

本文记录岛屿经营模式的产品和业务边界。

## 范围

岛屿经营模式负责长期局外进度：

```text
资源和背包
工具
采集
农田
建筑建造 / 升级 / 生产
科技树
任务和剧情入口
塔防战斗前后的奖励和消耗
```

## 当前系统

物品和资源：

```text
Assets/Scripts/Game/Island/Resources
Assets/Scripts/Game/Items
Data/Excel/item.xlsx
```

工具和采集：

```text
Assets/Scripts/Game/Island/Tools
Assets/Scripts/Game/Island/Exploration
Data/Excel/gather.xlsx
Data/Excel/resource.xlsx
```

农田：

```text
Assets/Scripts/Game/Island/Farming
Data/Excel/world_crop.xlsx
```

建筑和生产：

```text
Assets/Scripts/Game/Island/Buildings
Assets/Scripts/Game/Blueprints
Data/Excel/world_building.xlsx
Data/Excel/world_building_level.xlsx
Data/Excel/world_building_income.xlsx
Data/Excel/world_cost.xlsx
Data/Excel/reward.xlsx
```

科技：

```text
Assets/Scripts/Game/Island/Tech
Data/Excel/tech_node.xlsx
```

## 当前缺口

```text
任务奖励 Toast / 获得反馈
新手开局完整体验打磨
更多工具等级：石斧 -> 铜斧 -> 铁斧等
农田产出、成熟、补种等长期规则打磨
科技树和生产链的内容扩展
```

## UI

经营 UI 见 `Docs/Modules/ManagementUI.md`。UI 互斥、返回栈和关闭规则见 `Docs/Architecture/UIFramework.md`。








