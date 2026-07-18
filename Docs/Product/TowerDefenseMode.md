# Tower Defense Mode

本文记录塔防模式边界。

## 范围

塔防模式负责单局战斗：

```text
波次
敌人
基地
防御塔
战斗内金币
塔防内建塔 / 升级 / 出售
技能和效果
结算
```

## 代码位置

```text
Assets/Scripts/Game/TowerDefense
Assets/Scripts/Game/Tower
Assets/Scripts/Game/Wave
Assets/Scripts/Game/Battle
Assets/Scripts/Game/AbilityAdapters
Assets/Scripts/Ability
Assets/Scripts/Skill
```

## 进入战斗

塔防关卡统一通过实例入口加载：

```csharp
MapManager.Instance.LoadBattleMap(mapConfigId);
```

`mapConfigId` 是 `map.xlsx` 的配置 id。经营大地图使用 `MapManager.Instance.LoadWorldMap(worldMapId)`，不要用无模式语义的 `LoadMap` 混用两种流程。

经营主界面通过 `RightBar/BattleEntry` 进入战斗，当前入口加载默认关卡配置 `30950001`。战斗 UI 位于 `Assets/Arts/UI/Panels/Battle`。

战斗主界面是 `BattlePage`。其内部功能区是由 Page 统一管理生命周期的多个 Panel；设置和战斗结算作为模态 Popup 打开。当前小地图入口保留但默认关闭，其余战斗 HUD 模块进入正式功能闭环。

## 与经营模式的边界

- 塔防单局金币不直接等于长期经营库存。
- 塔防内建塔不直接复用经营建筑管理器。
- 结算奖励需要通过明确接口写回经营长期资源。
- 技能系统优先走 Ability，旧 Skill 作为兼容层保留一段时间。

Ability 细节见 `Docs/Modules/AbilityAndSkill.md`。








