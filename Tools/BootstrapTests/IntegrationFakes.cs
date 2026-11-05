// Offline boundary fakes only. These files are outside Assets and never enter a Unity player.
// They verify the real bootstrap/storage code without claiming to emulate Unity rendering or YooAsset.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game;

internal static class Boundary
{
    public static readonly List<string> Events = new List<string>();
    public static Task ResourceTask = Task.CompletedTask;
    public static Task<UI.UIHandle> PanelTask;
    public static bool CameraSucceeds = true;
    public static bool MapSucceeds = true;
    public static bool MapConfigSucceeds = true;
    public static bool AbilitySucceeds = true;
    public static string ThrowOnRelease;
    public static string ThrowOnRestore;

    public static void Reset()
    {
        Events.Clear();
        ResourceTask = Task.CompletedTask;
        PanelTask = Task.FromResult(new UI.UIHandle { IsValid = true, View = new WorldMainPanel() });
        CameraSucceeds = MapSucceeds = MapConfigSucceeds = AbilitySucceeds = true;
        ThrowOnRelease = ThrowOnRestore = null;
        GameplayController.Instance = null;
        DataManager.Instance.Item = DataManager.Instance.Map = null;
        CalendarManager.Instance.Value = new SaveCalendarData();
        StorageManager.Instance.EndSession();
    }

    public static void Event(string name) => Events.Add(name);
}

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }
    public class MonoBehaviour { public bool enabled = true; }
    public static class Application { public static string persistentDataPath; }
    public static class Time { public static float deltaTime = 0.02f; public static float unscaledDeltaTime = 0.03f; public static float unscaledTime; }
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
    public static class Debug
    {
        public static void Log(string message) => Boundary.Event("log:" + message);
        public static void LogWarning(string message) => Boundary.Event("warning:" + message);
        public static void LogError(string message, object context = null) => Boundary.Event("error:" + message);
        public static void LogException(Exception error) => Boundary.Event("exception:" + error.GetType().Name);
    }
}

namespace Game.Framework
{
    public enum InputMode { UI, World }
    public sealed class DeviceManager
    {
        public static DeviceManager Instance { get; } = new DeviceManager();
        public void Initialize() => Boundary.Event("device:init");
    }
    public sealed class ResourceManager
    {
        public static ResourceManager Instance { get; } = new ResourceManager();
        public Task InitializeAsync() { Boundary.Event("resources:init"); return Boundary.ResourceTask; }
    }
    public sealed class GameInputManager
    {
        public static GameInputManager Instance { get; } = new GameInputManager();
        public InputMode CurrentMode;
        public void Initialize(InputMode mode) { SetMode(mode); Boundary.Event("input:init"); }
        public void SetMode(InputMode mode) { CurrentMode = mode; Boundary.Event("input:" + mode); }
        public void Release() => Boundary.Event("input:release");
    }
    public static class TaskExtensions
    {
        public static async void Forget(this Task task)
        {
            try { await task; }
            catch (Exception error) { UnityEngine.Debug.LogException(error); }
        }
    }
}

namespace UI
{
    public sealed class UIHandle { public bool IsValid; public object View; }
    public sealed class PanelCollection
    {
        public Task<UIHandle> ShowAsync(string path) { Boundary.Event("ui:show"); return Boundary.PanelTask; }
    }
    public sealed class PageCollection { public void Clear(bool destroy) { } }
    public sealed class UIManager
    {
        public static UIManager Instance { get; } = new UIManager();
        public PanelCollection Panels { get; } = new PanelCollection();
        public PageCollection Pages { get; } = new PageCollection();
        public void UseResourceManagerLoader() => Boundary.Event("ui:loader");
        public void ClearAll(bool destroy) => Boundary.Event("ui:clear");
    }
}

namespace Game
{
    public class StubManager
    {
        public virtual void Initialize() => Boundary.Event(GetType().Name + ":init");
        public void Release()
        {
            Boundary.Event(GetType().Name + ":release");
            if (Boundary.ThrowOnRelease == GetType().Name) { throw new InvalidOperationException("release failed"); }
        }
        public void Update() => Boundary.Event(GetType().Name + ":tick");
        public void Update(float deltaTime) => Boundary.Event(GetType().Name + ":tick");
    }
    public class SaveManager<T> : StubManager
    {
        public T Value;
        public void LoadSaveData(T value)
        {
            if (Boundary.ThrowOnRestore == GetType().Name) { throw new InvalidOperationException("restore failed"); }
            Value = value;
        }
        public T CreateSaveData() => Value;
    }
    public sealed class DataManager : StubManager
    {
        public static DataManager Instance { get; } = new DataManager();
        public object Item;
        public object Map;
        public override void Initialize() { base.Initialize(); Item = Map = new object(); }
    }
    public sealed class CameraManager
    {
        public static CameraManager Instance { get; } = new CameraManager();
        public bool Initialize() { Boundary.Event("camera:init"); return Boundary.CameraSucceeds; }
    }
    public sealed class MapInputController : StubManager { public static MapInputController Instance { get; } = new MapInputController(); }
    public sealed class TechManager : SaveManager<SaveTechData> { public static TechManager Instance { get; } = new TechManager(); }
    public sealed class WorldGatherManager : SaveManager<SaveGatherNodeData[]> { public static WorldGatherManager Instance { get; } = new WorldGatherManager(); }
    public sealed class WorldBuildingManager : SaveManager<SaveWorldBuildingData[]>
    {
        public static WorldBuildingManager Instance { get; } = new WorldBuildingManager();
        public void LoadRuntimeUnlockSaveData(int[] ids) { }
        public int[] CreateRuntimeUnlockSaveData() => Array.Empty<int>();
    }
    public sealed class MineManager : StubManager { public static MineManager Instance { get; } = new MineManager(); }
    public sealed class FarmManager : SaveManager<SaveFarmData[]>
    {
        public static FarmManager Instance { get; } = new FarmManager();
        public void LoadSaveData(SaveFarmData[] farms, SaveWorldFarmPlotData[] legacy) => LoadSaveData(farms);
        public void CreateViews() => Boundary.Event("farms:views");
    }
    public sealed class BlueprintManager : StubManager { public static BlueprintManager Instance { get; } = new BlueprintManager(); }
    public sealed class QuestManager : SaveManager<SaveQuestData> { public static QuestManager Instance { get; } = new QuestManager(); }
    public sealed class QuestData { }
    public sealed class StoryManager : SaveManager<StorageManager.StoryData>
    {
        public static StoryManager Instance { get; } = new StoryManager();
        public void TryStartAutoStories() => Boundary.Event("story:start");
    }
    public sealed class ToolKitManager : SaveManager<SaveToolKitData> { public static ToolKitManager Instance { get; } = new ToolKitManager(); }
    public sealed class BagManager : SaveManager<SaveBagData> { public static BagManager Instance { get; } = new BagManager(); }
    public sealed class CalendarManager : SaveManager<SaveCalendarData>
    {
        public static CalendarManager Instance { get; } = new CalendarManager();
        public float RealSecondsPerGameMinute => 1f;
        public void AdvanceMinutes(int minutes) { }
    }
    public sealed class WorldIncomeManager : StubManager { public static WorldIncomeManager Instance { get; } = new WorldIncomeManager(); }
    public sealed class ItemManager : SaveManager<SaveWorldItemData[]> { public static ItemManager Instance { get; } = new ItemManager(); }
    public sealed class BaseManager : StubManager { public static BaseManager Instance { get; } = new BaseManager(); }
    public sealed class NpcManager : StubManager { public static NpcManager Instance { get; } = new NpcManager(); }
    public sealed class TowerManager : StubManager { public static TowerManager Instance { get; } = new TowerManager(); }
    public sealed class WaveManager : StubManager { public static WaveManager Instance { get; } = new WaveManager(); }
    public sealed class AbilityManager
    {
        public static AbilityManager Instance { get; } = new AbilityManager();
        public bool Initialize() { Boundary.Event("ability:init"); return Boundary.AbilitySucceeds; }
        public void Update(float dt) => Boundary.Event("ability:tick");
        public void Release() => Boundary.Event("ability:release");
    }
    public sealed class BattleFlowManager : StubManager
    {
        public static BattleFlowManager Instance { get; } = new BattleFlowManager();
        public bool IsRunning => false;
    }
    public sealed class WorldHpBarManager { public static WorldHpBarManager Instance { get; } = new WorldHpBarManager(); public void Clear() { } public void Update() { } }
    public sealed class TowerBuildManager : StubManager { public static TowerBuildManager Instance { get; } = new TowerBuildManager(); }
    public sealed class TowerBuildInputController : StubManager { public static TowerBuildInputController Instance { get; } = new TowerBuildInputController(); }
    public sealed class BattleTargetClickManager : StubManager { public static BattleTargetClickManager Instance { get; } = new BattleTargetClickManager(); }
    public sealed class QuestToastListener : StubManager { public static QuestToastListener Instance { get; } = new QuestToastListener(); }
    public sealed class GameplayController
    {
        public static GameplayController Instance;
        public SavePlayerData Player;
        public static void Ensure() { Instance = new GameplayController(); Boundary.Event("gameplay:create"); }
        public static void Shutdown() { Instance = null; Boundary.Event("gameplay:release"); }
        public SavePlayerData CreatePlayerSaveData() => Player;
    }
    public sealed class WorldMainPanel
    {
        public const string PrefabPath = "world-main";
        public void RefreshNow() => Boundary.Event("ui:refresh");
    }
    public static class GmCommandController { public static void EnsureExists() { } }
    public partial class MapManager
    {
        public static MapManager Instance { get; } = new MapManager();
        private int currentBattleMapConfigId;
        public int BattleMapId => currentBattleMapConfigId;
        public bool Initialize() => Boundary.MapConfigSucceeds;
        public void ClearMap() => Boundary.Event("map:clear");
        private void ClearBattleRuntime(bool clear) { }
        private bool LoadMapData(string location) => Boundary.MapSucceeds;
        private void CreateMap() => Boundary.Event("map:create");
        private void ReturnToMainMenu() { }
        public void LoadRemovedMapObjectSaveData(SaveRemovedMapObjectData[] data) { }
        public SaveRemovedMapObjectData[] CreateRemovedMapObjectSaveData() => Array.Empty<SaveRemovedMapObjectData>();
    }
}
