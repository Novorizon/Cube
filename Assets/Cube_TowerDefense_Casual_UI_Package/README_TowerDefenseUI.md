# Tower Defense Casual UI Package

目标：给 Cube 项目生成一套偏轻度游戏风格的塔防战斗 UI。

## 包含内容

- Runtime UI 类：
  - `BattleHudController`
  - `StatusPanel`
  - `BuildTowerPanel`
  - `TowerBuildCardView`
  - `TowerInfoPanel`
  - `SkillPanel`
  - `SkillSlotView`
  - `BattleControlPanel`
  - `MiniMapPanel`
  - `WorldHpBarView`
  - `UIProgressBar`
- Editor 生成器：
  - `Tools/Game/Tower Defense UI/Generate All UI Assets`
- 自动生成 UI 配置：
  - `Assets/GameRes/UI/TowerDefense/TowerDefenseUIConfig.asset`
- 自动生成 Prefab：
  - `Assets/GameRes/Prefabs/UI/TowerDefense/BattleHud.prefab`
  - `TowerBuildCard.prefab`
  - `SkillSlot.prefab`
  - `WorldHpBar.prefab`
- PNG UI 资源：
  - 圆角面板、按钮、塔图标、技能图标、状态图标等

## 使用方式

1. 将压缩包内容解压到 Unity 项目根目录。
2. 打开 Unity，等待脚本编译。
3. 执行菜单：`Tools/Game/Tower Defense UI/Generate All UI Assets`。
4. 将 `Assets/GameRes/Prefabs/UI/TowerDefense/BattleHud.prefab` 拖到战斗场景。
5. 在你的 Manager 中拿到 `BattleHudController`，调用：

```csharp
battleHud.SetBaseLife(currentLife, maxLife);
battleHud.SetGold(gold);
battleHud.SetWave(currentWave, totalWave);
battleHud.SetEnemyCount(aliveEnemyCount, totalEnemyCount);
```

## 和现有系统对接建议

`BattleHudController` 不直接依赖 `BaseManager / ItemManager / WaveManager / TowerManager`，避免因为项目当前类名和字段变化导致编译失败。

推荐在你的 `UIManager` 或 `GameEntry` 中做桥接：

```csharp
battleHud.TowerBuildClicked += towerId => TowerBuildManager.Instance.StartBuild(towerId);
battleHud.SkillClicked += itemId => ItemManager.Instance.UseItem(itemId);
battleHud.TowerUpgradeClicked += towerId => TowerManager.Instance.UpgradeSelectedTower();
battleHud.TowerSellClicked += towerId => TowerManager.Instance.SellSelectedTower();
battleHud.AutoNextWaveChanged += value => WaveManager.Instance.SetAutoNextWave(value);
```

`TowerInfoPanel` 的显示数据用 `TdTowerRuntimeInfo` 传入：

```csharp
battleHud.ShowTowerInfo(new TdTowerRuntimeInfo
{
    TowerId = tower.ConfigId,
    Name = config.Name,
    Icon = icon,
    Level = tower.Level,
    Attack = tower.Attack,
    AttackAdd = nextAttack - tower.Attack,
    Range = tower.Range,
    AttackInterval = tower.AttackInterval,
    UpgradeCost = upgradeCost,
    SellGold = sellGold,
    CanUpgrade = canUpgrade
});
```

## 注意

- 这套 UI 使用 UGUI + TextMeshPro。
- 项目里已经有 UGUI 和 TextMeshPro 包，因此不需要额外插件。
- Prefab 通过 Unity Editor 生成，原因是 MonoBehaviour / Sprite 引用需要 Unity 按本项目 `.meta` 和 GUID 正确写入。
- 图片资源是轻量占位风格，后续可以替换成正式美术，脚本和 Prefab 结构不需要大改。
