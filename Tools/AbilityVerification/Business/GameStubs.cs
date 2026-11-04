using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Ability;
using Game.Ability.Configuration;





namespace Game.Framework
{
    public abstract class Singleton<T>
        where T : new()
    {
        public static T Instance { get; } = new T();
    }

    public static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            if (task.IsFaulted)
                throw task.Exception;
        }
    }

    public sealed class ResourceManager : Singleton<ResourceManager>
    {
        public readonly Dictionary<string, UnityEngine.Object> Assets = new();
        public UnityEngine.GameObject LoadGameObject(string path) => LoadAsset<UnityEngine.GameObject>(path);
        public T LoadAsset<T>(string path)
            where T : UnityEngine.Object => Assets.TryGetValue(path, out var a) ? a as T : null;
    }

    public sealed class EffectManager : Singleton<EffectManager>
    {
        public TaskCompletionSource<UnityEngine.GameObject> NextLoad;
        public Func<string, Task<UnityEngine.GameObject>> LoadEffectOverride;
        public Task<UnityEngine.GameObject> PlayEffectAsync(string name, UnityEngine.Vector3 position, bool autoDestroy = true)
        {
            if (LoadEffectOverride != null) return LoadEffectOverride(name);
            if (NextLoad != null)
            {
                var pending = NextLoad;
                NextLoad = null;
                return pending.Task;
            }

            return Task.FromResult(new UnityEngine.GameObject { transform = new UnityEngine.Transform { position = position } });
        }
    }

    public sealed class AudioManager : Singleton<AudioManager>
    {
        public void PlaySound(string name, AudioPlayOptions options)
        {
        }
    }

    public sealed class AudioPlayOptions
    {
        public UnityEngine.Vector3 Position;
    }
}

namespace Game
{
    public enum ActorType
    {
        Hero,
        Enemy
    }

    public class BattleItemManager : Game.Framework.Singleton<BattleItemManager>
    {
        public int GetCount(int id) => 100;
        public bool HasItem(int id, int n) => true;
        public int Consumed;
        public bool TryConsume(int id, int n)
        {
            Consumed += n;
            return true;
        }

        public void AddItem(int id, int n)
        {
        }

        public object GetAllItems() => null;
    }

    public class SkillConfig
    {
        public bool Enable = true;
        public int CostResourceId;
    }

    public class SkillActionConfig
    {
    }

    public class SkillModifierConfig
    {
    }

    public static class AbilityConfigConverter
    {
        public static string AbilityName(int id) => "skill_" + id;
        public static string ModifierName(int id) => "modifier_" + id;
    }

    public sealed class ExcelAbilityDefinitionProvider : IAbilityDefinitionProvider
    {
        public ExcelAbilityDefinitionProvider(object a, object b, object c)
        {
        }

        public string ProviderName => "stub Excel";
        public AbilityDefinitionSourceType SourceType => AbilityDefinitionSourceType.Excel;

        public AbilityDefinitionBundle Load() => new();
    }
}



namespace TMPro
{
    public class TMP_Text
    {
    }
}

namespace Game
{
    public class Npc
    {
        static int next;
        int id = ++next;
        public NpcData Data = new();
        public NpcConfig Config = new();
        public ActorType ActorType = ActorType.Enemy;
        public UnityEngine.Transform transform = new();
        public UnityEngine.GameObject gameObject = new();
        public string name = "Npc";
        public System.Threading.CancellationToken destroyCancellationToken;
        public int GetInstanceID() => id;
        public void InitializeRaw(NpcConfig c, NpcData d)
        {
            Config = c;
            Data = d;
        }

        public T GetComponentInChildren<T>()
            where T : new() => new();
    }

    public class NpcData
    {
        public int CurrentHp = 100, MaxHp = 100, PathIndex, DamageToBase = 10, RewardGold;
        public bool Dead, Moving, Attacking, ReachedGoal;
        public float AttackTimer, AttackInterval = 1, MoveSpeed = 1, AttackRange = 1;
        public List<UnityEngine.Vector3Int> Path = new();
        public UnityEngine.Animator Animator;
        public void Initialize(NpcConfig config, List<UnityEngine.Vector3Int> path)
        {
            Path = path;
        }
    }

    public class NpcConfig
    {
        public int Id = 1, Kind = (int)GameEntityKind.Actor, ActorType;
        public string Name = "Npc", PrefabLocation = "Assets/Npc.prefab";
        public float ModelScale = 1;
    }

    public class Tower
    {
        static int next;
        int id = ++next;
        public int ConfigId = 1, Level = 1;
        public TowerData Data = new();
        public UnityEngine.Vector3Int Coord;
        public string name = "Tower";
        public UnityEngine.Transform transform = new();
        public UnityEngine.GameObject gameObject = new();
        public int GetInstanceID() => id;
    }

    public class TowerData
    {
        public float AttackTimer;
        public Npc Target;
    }

    public class ConfigTableReader<T>
    {
        public Dictionary<int, T> Values = new();
        public bool TryGet(int id, out T value) => Values.TryGetValue(id, out value);
        public IReadOnlyDictionary<int, T> GetAll() => Values;
    }

    public class DataManager : Game.Framework.Singleton<DataManager>
    {
        public ConfigTableReader<SkillConfig> Skill = new();
        public ConfigTableReader<SkillActionConfig> SkillAction = new();
        public ConfigTableReader<SkillModifierConfig> SkillModifier = new();
        public ConfigTableReader<BaseConfig> Base = new();
        public ConfigTableReader<NpcConfig> Npc = new();
        public TowerLevelConfig TowerLevel = new();
        public TowerLevelConfig GetTowerLevel(int id, int level) => TowerLevel;
        public bool TryGetTowerLevel(int id, int level, out TowerLevelConfig value)
        {
            value = TowerLevel;
            return true;
        }

        public IReadOnlyList<NpcDropConfig> GetNpcDrops(int id) => Array.Empty<NpcDropConfig>();
    }

    public class BaseConfig
    {
        public int Id = 1, Hp = 100, Defense = 3;
        public bool Enable = true;
        public string Name = "Base", PrefabLocation = "Assets/Base.prefab";
    }

    public class BaseView
    {
    }

    public class TileView
    {
        public UnityEngine.Transform transform = new();
    }

    public class MapPathFinder
    {
        public bool TryFindPath(UnityEngine.Vector3Int a, UnityEngine.Vector3Int b, out List<UnityEngine.Vector3Int> path)
        {
            path = new()
            {
                a,
                b
            };
            return true;
        }
    }

    public class MapManager : Game.Framework.Singleton<MapManager>
    {
        public IReadOnlyList<UnityEngine.Vector3Int> SpawnPoints = new List<UnityEngine.Vector3Int>
        {
            new()
        };
        public bool TryGetGoalPoint(out UnityEngine.Vector3Int goal)
        {
            goal = new();
            return true;
        }

        public bool TryGetTileView(UnityEngine.Vector3Int c, out TileView v)
        {
            v = null;
            return false;
        }

        public UnityEngine.Vector3 GetTileWorldPosition(UnityEngine.Vector3Int c) => new();
    }

    public enum GameEntityKind
    {
        Actor
    }

    public class NpcDropConfig
    {
        public int ItemId, ChancePermyriad, MinCount, MaxCount;
    }

    public class ItemDropManager : Game.Framework.Singleton<ItemDropManager>
    {
        public void DropItem(int id, int count, UnityEngine.Vector3 pos, bool value)
        {
        }
    }

    public static class NpcAnimationController
    {
        public static int DeathAnimations;
        public static Task PlayDeathAsync(UnityEngine.Animator a, System.Threading.CancellationToken ct, float duration)
        {
            DeathAnimations++;
            return new TaskCompletionSource().Task;
        }
        public static void SetWalking(UnityEngine.Animator a, bool walk)
        {
        }

        public static void PlayAttack(UnityEngine.Animator a)
        {
        }
    }

    public class GoldFlyMessage
    {
        public UnityEngine.Vector3 WorldPosition;
        public int Count;
    }

    public class BaseLifeMessage
    {
        public int CurrentLife, MaxLife;
    }

    public enum BattleMessageTopic
    {
        GoldFlyRequested,
        BaseLifeChanged,
        BattleEnded,
        BattleStateChanged
    }

    public class Messager : Game.Framework.Singleton<Messager>
    {
        public Action<BattleMessageTopic, object> OnNotify;
        public void Notify(BattleMessageTopic topic, object message) => OnNotify?.Invoke(topic, message);
    }

    public class WaveManager : Game.Framework.Singleton<WaveManager>
    {
        public void NotifyEnemyKilled(Npc n)
        {
        }

        public void Stop()
        {
        }
    }

    public class TowerLevelConfig
    {
        public int SkillId, CostItemId = 1, BuildCost = 1;
        public float Range = 100, AttackInterval = 1, Damage = 10;
        public string AttackEffect = "Assets/Attack.prefab", HitEffect = "Assets/Hit.prefab";
    }

    public static class ItemIds
    {
        public const int Gold = 1;
    }

    public class BattleSettlementBuilder
    {
        public BattleSettlementReward BuildReward(object items) => new();
        public void PrintReward(BattleSettlementReward reward)
        {
        }
    }

    public class BattleSettlementReward
    {
    }

    public enum BattleState
    {
        None,
        Running,
        Victory,
        Defeat,
        Settled
    }

    public class BattleEndedMessage
    {
        public BattleState State;
        public bool Victory;
        public int MapId;
        public string MapName, Reason;
        public BattleSettlementReward Reward;
    }

    public class BattleStateMessage
    {
        public BattleState State;
        public int MapId;
        public string MapName;
    }

    public class MapConfig
    {
        public int Id;
    }

    public static class LocalizedConfigText
    {
        public static string MapName(int id) => "Map";
    }

    public static class LocalizationManager
    {
        public static string Get(string key) => key;
    }

    public class TowerBuildManager : Game.Framework.Singleton<TowerBuildManager>
    {
        public void CancelSelect()
        {
        }
    }

    public class BattleTargetClickManager : Game.Framework.Singleton<BattleTargetClickManager>
    {
        public void ClearSelection()
        {
        }
    }

    public class GameInputManager : Game.Framework.Singleton<GameInputManager>
    {
        public static bool IsCreated => false;

        public void SetMode(InputMode mode)
        {
        }
    }

    public enum InputMode
    {
        UI
    }

    public class VisualProjectileEffect
    {
        public float Duration = 0.1f;
        public void Play(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
        {
        }
    }
}
