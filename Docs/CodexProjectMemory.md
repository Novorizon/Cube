# Codex Project Memory

## Project Basics

- Workspace: `D:\Cube`
- Engine: Unity 2022
- Key packages/tools: Odin Inspector 3.1.2, YooAsset, Luban Excel pipeline
- Game direction: the main project is an island survival / island management simulation after drifting ashore; combat is implemented through a tower defense mode.
- Current focus: tower defense module. Map editor and tower defense flow already exist.

## Important Paths

- Luban source data: `D:\Cube\Data`
- Luban Excel tables: `D:\Cube\Data\Excel`
- Luban schema definitions: `D:\Cube\Data\Defines`
- Generated client config code: `D:\Cube\Assets\Scripts\Game\Data\Generated`
- Runtime config bytes: `D:\Cube\Assets\Data\Bin`
- Map json data: `D:\Cube\Assets\Data\Map`
- Art assets: `D:\Cube\Assets\Arts`
- Main scripts: `D:\Cube\Assets\Scripts`
- Project docs: `D:\Cube\Docs`

## Luban / Data Notes

- `Data/luban.conf` uses:
  - `schemaFiles`: `Defines`
  - `dataDir`: `Excel`
  - target manager: `Tables`
  - top module: `Game`
- Main Excel files observed:
  - Ability system: `AbilityConfig.xlsx`, `AbilityAction.xlsx`, `AbilityModifier.xlsx`, `AbilityModifierProperty.xlsx`, `AbilityProjectile.xlsx`, `AbilitySpecialValue.xlsx`, `AbilitySystemEnum.xlsx`
  - Legacy/skill system: `skill.xlsx`, `skill_action.xlsx`, `skill_modifier.xlsx`, `skill_system_enum.xlsx`
  - Tower defense gameplay: `tower.xlsx`, `tower_level.xlsx`, `wave.xlsx`, `Wave/wave1.xlsx`, `map.xlsx`, `npc.xlsx`, `npc_drop.xlsx`, `item.xlsx`, `base.xlsx`
- Runtime data loading goes through `Game.DataManager`.
  - General tables are loaded from `Assets/Data/Bin/{table}.bytes`.
  - Wave data is loaded separately per map through `DataManager.LoadWave(string waveLocation)`.
  - `DataManager.MakeTowerLevelId(towerId, level)` is `towerId * 100 + level`.

## Code Layout

- Entry point: `Assets/Scripts/GameEntry.cs`
  - Initializes `ResourceManager`, input, camera, map input, data, base, npc, tower, wave, ability, battle flow, map manager, tower build, target click, and UI.
  - Main update loop only runs simulation when `BattleFlowManager.Instance.IsRunning`.
- Framework:
  - Resource/YooAsset wrapper: `Assets/Scripts/Framework/Resource`
  - Message/event systems: `Assets/Scripts/Framework/Message`, `Assets/Scripts/Framework/Event`
  - UI framework: `Assets/Scripts/Framework/UI`
  - Input system: `Assets/Scripts/Framework/InputSystem`
- Map editor:
  - Runtime data: `Assets/Scripts/MapEditor/Runtime`
  - Editor windows/generators: `Assets/Scripts/MapEditor/Editor`
  - Notable files: `MapData.cs`, `MapTileData.cs`, `TileData.cs`, `MapTileType.cs`, `MapTilePrefabConfig.cs`, `MapEditorWindow.cs`, `SimpleMapEditorWindow.cs`, `ReferenceMapPrefabGenerator.cs`
- Tower defense runtime:
  - Battle state/settlement/selection: `Assets/Scripts/Game/Battle`
  - Wave spawning: `Assets/Scripts/Game/Wave`
  - Towers/building/upgrading/selling: `Assets/Scripts/Game/Tower`
  - Map runtime/loading/path checks: `Assets/Scripts/Game/Map`
  - NPCs: `Assets/Scripts/Game/Npc`
  - Base: `Assets/Scripts/Game/Base`
  - Items/drops: `Assets/Scripts/Game/Item`, `Assets/Scripts/Game/Drop`
  - Ability integration layer: `Assets/Scripts/Game/AbilityAdapters`
  - Tower defense UI: `Assets/Scripts/Game/UI/TowerDefense`
- Ability systems:
  - Newer ability core: `Assets/Scripts/Ability`
  - Older skill system: `Assets/Scripts/Skill`
  - Ability design doc: `Docs/AbilitySystem.md`

## Tower Defense Flow

- `GameEntry.Initialize()` loads systems, then opens `Assets/Arts/UI/Pages/MainMenuPage.prefab`.
- `MapManager.LoadMap(mapId)`:
  - Reads map config from `DataManager.Map`.
  - Clears previous battle runtime.
  - Adds initial gold from `MapConfig.InitialGold`.
  - Loads map json from `Assets/Data/Map/{mapId}.json`.
  - Creates tile views using `Assets/Data/Cube/Configs/MapTilePrefabConfig.asset`.
  - Shows battle HUD: `Assets/Arts/UI/TowerDefense/Prefabs/BattleHud.prefab`.
  - Starts battle via `BattleFlowManager.BeginBattle(mapConfig)`.
  - Loads base, switches input to battle mode, loads map-specific wave bytes from `mapConfig.WaveNormal`, then calls `WaveManager.StartWave()`.
- `WaveManager`:
  - Loads current wave table from `DataManager.Instance.Wave`.
  - Sorts waves by config id.
  - Spawns enemies through `NpcManager` using `WaveSpawnMode`.
  - Supports auto chaining waves or waiting until enemies are cleared.
  - Calls `BattleFlowManager.CompleteVictory()` when all waves are done and enemies are cleared.
- `BattleFlowManager`:
  - States: `None`, `Running`, `Victory`, `Defeat`, `Settled`.
  - On end: stops waves, cancels tower selection/build, resets time scale, switches input to UI, builds reward on victory, and sends `BattleEnded` message.
- `TowerManager`:
  - Tracks active towers.
  - Pulls per-level stats from `TowerLevelConfig`.
  - Finds nearest enemy in range.
  - Casts configured tower skill when `SkillId > 0`, otherwise plays projectile/hit effects and applies damage.
- `MapManager` also owns runtime tile checks:
  - `IsWalkable`, `IsBuildable`, `TryPlaceTower`, `RemoveTower`, `TryDestroyHill`, `TryPickTile`, `GetWalkableNeighbors`.

## Art Asset Layout

- Map/tile art:
  - `Assets/Arts/Map/Tiles`
  - `Assets/Arts/Map/Prefabs/ReferenceMap_01.prefab`
  - `Assets/Arts/Tile`
- Tower art:
  - `Assets/Arts/Tower/NormalTower.prefab`
  - `Assets/Arts/Tower/kenney_tower-defense-kit`
  - Tower FBX pieces include round/square builds, roofs, tops, weapons, and ammo.
- Tower defense UI:
  - `Assets/Arts/UI/TowerDefense/Prefabs`
  - Important prefabs: `BattleHud.prefab`, `TowerBuildCard.prefab`, `TowerCard.prefab`, `Skill.prefab`, `Item.prefab`, `WorldHpBar.prefab`, `MiniMapIcon.prefab`, `SettingsDialog.prefab`, `SpeedButton.prefab`
  - Shared art: `Assets/Arts/UI/TowerDefense/Common`
  - Sprite atlas: `Assets/Arts/UI/Atlas/TowerDefense.spriteatlasv2`

## Coding Notes / Caveats

- Map editor and related pathfinding files were normalized to UTF-8 without BOM and LF line endings on 2026-05-30. Avoid mass-converting generated Luban files unless explicitly requested.
- Prefer existing singleton/manager style and current paths rather than introducing new architecture.
- For config-driven gameplay changes, update Luban Excel/Defines first, then regenerate generated configs/bytes through the existing batch scripts in `Data`.
- For UI work, use the existing `Framework/UI` manager and tower defense prefabs/assets.
- For resource loading, prefer `ResourceManager` paths already used by the project, especially because YooAsset is in use.

## Recent Progress

- Reworked `Assets/Scripts/MapEditor/Editor/MapEditorWindow.cs` into a cleaner Odin `OdinEditorWindow`.
  - Opens from `Tools/Map/Map Editor`.
  - Uses Odin tabs: `Map`, `Paint`, `Selection`, `Points`, `IO`.
  - Creates grid maps from origin `(0,0,0)` extending in positive X/Y/Z. The editor no longer generates a `y = -1` `Soil` support layer; `y = 0` is the bottom layer.
  - Scene View left-click selects a tile and shows editable tile data in the `Selection` tab.
  - Brush mode supports left-click/drag batch painting of tile types, plus `Raise`/`Lower` modes for building higher terrain after the first layer.
  - `Selection` tab has `Add Tile Above Selected` and `Remove Selected Tile` for manual height editing.
  - Tile prefabs under `Assets/Arts/Map/Tiles` were normalized by the user to `1x1x1`; the map editor no longer performs preview-time bounds fitting/scaling to avoid conflicting with authored prefab dimensions.
  - Supports spawn/goal point editing and JSON import/export.
- Extended `MapTileType` with special tile types `Road` and `Bridge`.
- Updated `MapTileRule` so `Grass`, `Hill`, `Water`, `Snow` remain base tile types and `Road`/`Bridge` are walkable special tiles with default move cost `8`.
- `dotnet build Cube.sln` passed after these map editor changes.
- Added tile overlay editing:
  - `MapTileData.Type` remains the base terrain type.
  - Added `MapTileData.Overlay` (`None`, `Road`, `Bridge`, `Stair`, `Ramp`) and `MapTileData.Direction` (`None`, `North`, `East`, `South`, `West`).
  - Map editor brush modes now separate `Type`, `Overlay`, `Raise`, and `Lower`.
  - Base type buttons only expose `Grass`, `Hill`, `Snow`, `Water`; overlay buttons expose route/bridge/stair/ramp semantics.
  - Editor preview and runtime map loading instantiate base terrain first and overlay visuals second, so `Water + Bridge` preserves water while adding bridge logic/visuals.
  - Runtime pathfinding now requires `Stair` or `Ramp` with matching `Direction` to traverse a height difference of 1; same-height movement remains normal.
- Added map decoration data and editor support:
  - `MapData.Decorations` stores `MapDecorationData` entries in exported map JSON.
  - `MapEditorWindow` has a `Decoration` tab for choosing a prefab, setting local position/euler/scale, adding it to the selected tile, removing decorations on the selected tile, and clearing all decorations.
  - Decoration source resources use `MapDecorationPrefabConfig.asset` (`MapDecorationPrefabConfig`) as the authoritative list. Map JSON decoration entries store only `DecorationId`; runtime resolves prefabs through the config.
  - `MapDecorationPrefabConfig` itself uses Odin (`TableList`, preview/required fields, `NormalizeIds`) to manage the source decoration resource list in the asset Inspector.
  - `MapDecorationPrefabConfig` uses Odin ordinary `ListDrawerSettings`, not `TableList`, because the Inspector is narrow and table columns make prefab fields unusable. Empty prefabs should not show a hard `Required` error while authoring.
  - The Map Editor `Decoration` tab is for placement: left column is config/selection/local transform/actions; right column is a read-only source preview with Select buttons. Source-list add/delete stays only in the config asset Inspector.
