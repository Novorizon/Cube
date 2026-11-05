using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game;
using Game.Ability;
using Game.Ability.Configuration;
using Game.Framework;
using UnityEngine;

// Compiles production ability/adapters/changed managers against minimal unrelated-game and Unity stubs.
// This verifies C# contracts and synchronous runtime logic; it is not a Unity play-mode test.
internal static class Program
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        passed++;
        Console.WriteLine("PASS " + name);
    }

    private static async Task Main()
    {
        AbilityManager manager = AbilityManager.Instance;
        Npc npc = new Npc();
        Check(!manager.FindOwner(npc).IsValid && manager.GetUnit(npc) == null, "uninitialized queries and bindings are closed");
        NpcManager.Instance.Initialize();
        TowerManager.Instance.Initialize();
        BaseManager.Instance.Initialize();
        BattleFlowManager.Instance.Initialize();
        DataManager.Instance.Base.Values[1] = new BaseConfig();
        ResourceManager.Instance.Assets["Assets/Base.prefab"] = new GameObject();
        Check(BaseManager.Instance.LoadBase(1), "actual BaseManager resource initialization");
        manager.AddDefinitionProvider(new TestDefinitions());
        Check(manager.Initialize(), "actual game integration initializes with code provider");
        BattleFlowManager.Instance.BeginBattle(new MapConfig());

        var before = new List<AbilityBindingDebugInfo>();
        var after = new List<AbilityBindingDebugInfo>();
        manager.GetBindingDebugSnapshot(before);
        manager.HasState(npc, UnitState.Stunned);
        manager.TryGetBaseAbilityCooldown(99, out _, out _);
        manager.GetBindingDebugSnapshot(after);
        Check(before.Count == after.Count && !manager.BindOwner(npc).IsValid, "reads and unregistered actors cannot manufacture bindings");
        NpcManager.Instance.Register(npc);
        AbilityOwnerHandle owner = manager.FindOwner(npc);
        Check(owner.IsValid, "actual NPC registration creates a controlled binding");
        Check(manager.GrantAbility(owner, AbilityKey.ByStableId("code:test"), 4), "stable ID grants without Excel config");
        manager.GrantAbility(owner, AbilityKey.ByName("test"), 1);
        Check(manager.TryGetAbilityStatus(owner, AbilityKey.ByName("test"), out AbilityStatus status) && status.Level == 4, "idempotent grant and status query preserve trained level");
        AbilityOwnerHandle baseOwner = manager.BindBaseOwner();
        manager.GrantAbility(baseOwner, AbilityKey.FromLegacyId(99), 4);
        Check(manager.TryGetBaseAbilityCooldown(99, out _, out _) && manager.TryGetAbilityStatus(baseOwner, AbilityKey.FromLegacyId(99), out status) && status.Level == 4, "legacy cooldown query preserves a high-level ability");
        manager.GrantAbility(owner, AbilityKey.ByName("point"), resourceItemId: ItemIds.Gold);
        Check(manager.TryGetAbilityMetadata(AbilityKey.ByName("point"), out AbilityMetadata metadata) && (metadata.Usage & AbilityUsage.PositionTarget) != 0 && metadata.ResourceCost == 10f && metadata.CastRange == 10f, "metadata exposes targeting and tuning without mutable definitions");
        int previewEvents = 0;
        Action<RuntimeEvent> previewListener = _ => previewEvents++;
        manager.Engine.EventRaised += previewListener;
        int previewSpent = BattleItemManager.Instance.Consumed;
        AbilityCommandResult preview = manager.CheckCastAtPosition(owner, AbilityKey.ByName("point"), Vector3.right);
        manager.Engine.EventRaised -= previewListener;
        Check(preview.Accepted && preview.CastId == 0 && previewEvents == 0 && BattleItemManager.Instance.Consumed == previewSpent, "facade preview validates without orders events or resource consumption");
        manager.GrantAbility(owner, AbilityKey.ByName("timer"), resourceItemId: ItemIds.Gold);
        Check(manager.TrySetActivated(owner, AbilityKey.ByName("timer"), false) && !manager.CheckCast(owner, AbilityKey.ByName("timer")).Accepted && manager.TrySetActivated(owner, AbilityKey.ByName("timer"), true), "facade controls activation and checks disabled abilities");
        manager.Cast(owner, AbilityKey.ByName("timer"));
        manager.Update(1f);
        AbilityEffectHandle cooldownReduction = manager.ApplyStatusEffect(owner, "cdr");
        Check(manager.TryGetAbilityStatus(owner, AbilityKey.ByName("timer"), out AbilityStatus timer) && timer.CooldownDuration == 10f && timer.CooldownRemaining == 9f && timer.ExecutionPhase == AbilityExecutionPhase.Idle && timer.MaxCharges == 2 && timer.ChargeRestoreDuration == 4f && timer.ChargeRestoreRemaining == 3f && timer.ResourceCost == 2f, "facade status keeps actual cooldown duration while exposing typed phase and charge recovery");
        manager.RemoveStatusEffect(cooldownReduction);
        int spentBefore = BattleItemManager.Instance.Consumed;
        AbilityCommandResult noTarget = manager.CastAtBestTarget(owner, AbilityKey.ByName("point"));
        Check(!noTarget.Accepted && noTarget.Failure == AbilityCommandFailure.InvalidTarget && BattleItemManager.Instance.Consumed == spentBefore, "automatic point cast with no target cannot spend resources");
        int optionalTarget = 0;
        manager.Engine.EventRaised += item => { if (item.EventType == RuntimeEventType.SpellStarted && item.Ability?.Definition.Name == "optional") optionalTarget = item.Target?.EntityId ?? 0; };
        manager.GrantAbility(owner, AbilityKey.ByName("optional"));
        manager.GrantAbility(owner, AbilityKey.ByName("optional.empty"));
        Check(manager.CastAtBestTarget(owner, AbilityKey.ByName("optional")).Accepted && optionalTarget == manager.GetBaseUnit().EntityId && manager.CastAtBestTarget(owner, AbilityKey.ByName("optional.empty")).Accepted, "optional unit AI casts choose available targets and allow empty casts");

        var updates = new List<AbilityCommandUpdate>();
        manager.CommandUpdated += updates.Add;
        AbilityCommandResult instant = manager.Cast(owner, AbilityKey.ByName("test"));
        Check(instant.Accepted && instant.CastId > 0 && updates.Exists(u => u.CastId == instant.CastId && u.Stage == AbilityCommandStage.Executed) && updates.Exists(u => u.CastId == instant.CastId && u.Stage == AbilityCommandStage.Completed), "receipt correlates synchronous execution and completion");
        manager.GrantAbility(owner, AbilityKey.ByName("slow"));
        AbilityCommandResult casting = manager.Cast(owner, AbilityKey.ByName("slow"));
        AbilityCommandResult queued = manager.Cast(owner, AbilityKey.ByName("test"), queue: true);
        Check(casting.Accepted && queued.Accepted && queued.Queued && queued.CastId != casting.CastId, "queued receipt exposes independent command ID");
        manager.Stop(owner);
        Check(updates.Exists(u => u.CastId == casting.CastId && u.Stage == AbilityCommandStage.Interrupted) && updates.Exists(u => u.CastId == queued.CastId && u.Stage == AbilityCommandStage.Interrupted), "stop reports both active and queued interruptions");
        AbilityEffectHandle buff = manager.ApplyStatusEffect(owner, "buff");
        Check(buff.IsValid && manager.TryGetStatusEffect(buff, out AbilityEffectStatus effect) && effect.Stacks == 1 && manager.RemoveStatusEffect(buff), "controlled standalone effect apply/query/removal");
        AbilityEffectHandle disarm = manager.ApplyStatusEffect(owner, "disarm");
        Check(manager.IsAttackRestricted(npc) && !manager.IsMovementRestricted(npc), "NPC disarm blocks attacks while preserving movement");
        manager.RemoveStatusEffect(disarm);

        DamageResult baseHit = manager.Engine.ApplyAttackDamage(new DamageInfo { Attacker = manager.GetUnit(npc), Victim = manager.GetBaseUnit(), Amount = 10, DamageType = DamageType.Physical });
        Check(baseHit.AppliedAmount == 7 && BaseManager.Instance.CurrentLife == 93, "actual BaseManager applies physical block only once");
        npc.Data.CurrentHp = 99;
        HealResult healed = manager.Engine.HealWithResult(new HealInfo { Target = manager.GetUnit(npc), Amount = 5 });
        Check(healed.AppliedAmount == 1 && npc.Data.CurrentHp == 100, "actual NPC healing reports capped HP change");

        // An HP notification can replace the base before ApplyFinalDamage/Heal returns.
        // The committed fact must use the mutation snapshot, not the replacement object's HP.
        Messager.Instance.OnNotify = (topic, _) =>
        {
            if (topic != BattleMessageTopic.BaseLifeChanged) return;
            Messager.Instance.OnNotify = null;
            BaseManager.Instance.LoadBase(1);
        };
        DamageResult replacedDuringDamage = manager.Engine.ApplyDamage(new DamageInfo { Attacker = manager.GetUnit(npc), Victim = manager.GetBaseUnit(), Amount = 10, DamageType = DamageType.Pure });
        Check(replacedDuringDamage.AppliedAmount == 10 && !replacedDuringDamage.Killed && BaseManager.Instance.CurrentLife == 100, "damage fact survives synchronous base replacement callback");
        BaseManager.Instance.ApplyFinalDamage(20, out _);
        Messager.Instance.OnNotify = (topic, _) =>
        {
            if (topic != BattleMessageTopic.BaseLifeChanged) return;
            Messager.Instance.OnNotify = null;
            BaseManager.Instance.LoadBase(1);
        };
        HealResult replacedDuringHeal = manager.Engine.HealWithResult(new HealInfo { Target = manager.GetBaseUnit(), Amount = 5 });
        Check(replacedDuringHeal.AppliedAmount == 5 && BaseManager.Instance.CurrentLife == 100, "heal fact survives synchronous base replacement callback");
        int logged = Debug.LoggedExceptions;
        Messager.Instance.OnNotify = (topic, _) => { if (topic == BattleMessageTopic.BaseLifeChanged) throw new InvalidOperationException("view failure"); };
        DamageResult viewFailure = manager.Engine.ApplyDamage(new DamageInfo { Victim = manager.GetBaseUnit(), Amount = 5, DamageType = DamageType.Pure });
        Messager.Instance.OnNotify = null;
        Check(viewFailure.AppliedAmount == 5 && Debug.LoggedExceptions == logged + 1, "view notification errors preserve committed base damage");

        Npc dying = new Npc();
        dying.Data.CurrentHp = 3;
        NpcManager.Instance.Register(dying);
        int deaths = 0;
        manager.Engine.EventRaised += item => { if (item.EventType == RuntimeEventType.UnitDied && item.Target?.EntityId == manager.FindOwner(dying).EntityId) deaths++; };
        Check(NpcManager.Instance.TakeDamage(dying, 10) && !NpcManager.Instance.TakeDamage(dying, 10) && deaths == 1, "legacy NPC damage reports death once");
        NpcManager.Instance.Remove(dying);
        Check(!manager.FindOwner(dying).IsValid && manager.GetUnit(dying) == null, "actual NPC removal cannot rebind a departed actor");
        Npc directDeath = new Npc();
        NpcManager.Instance.Register(directDeath);
        int directId = manager.FindOwner(directDeath).EntityId;
        int animations = NpcAnimationController.DeathAnimations;
        Action<RuntimeEvent> badDeathListener = item => { if (item.EventType == RuntimeEventType.UnitDied && item.Target?.EntityId == directId) throw new InvalidOperationException("death listener failure"); };
        manager.Engine.EventRaised += badDeathListener;
        Task deathTask = NpcManager.Instance.KillNpcAsync(directDeath);
        manager.Engine.EventRaised -= badDeathListener;
        Check(!deathTask.IsFaulted && NpcAnimationController.DeathAnimations == animations + 1, "direct NPC death continues animation despite a death listener failure");
        NpcManager.Instance.Remove(directDeath);

        Tower tower = new Tower();
        TowerManager.Instance.Register(tower);
        disarm = manager.ApplyStatusEffect(manager.FindOwner(tower), "disarm");
        Check(manager.IsActionRestricted(tower), "tower disarm suppresses attack launch");
        manager.RemoveStatusEffect(disarm);
        int hp = npc.Data.CurrentHp;
        AbilityAttackHandle attack = manager.BeginTowerAttack(tower, npc, 10);
        Check(attack.IsValid && npc.Data.CurrentHp == hp, "delayed attack launch does not settle damage");
        AbilityDamageOutcome impact = manager.ResolveTowerAttack(attack);
        Check(impact.AppliedAmount == 10 && manager.ResolveTowerAttack(attack).AppliedAmount == 0 && npc.Data.CurrentHp == hp - 10, "delayed attack settles at most once");

        var hitRequested = new TaskCompletionSource();
        var hitLoad = new TaskCompletionSource<GameObject>();
        EffectManager.Instance.LoadEffectOverride = name =>
        {
            if (name != "Assets/Hit.prefab") return Task.FromResult(new GameObject());
            hitRequested.TrySetResult();
            return hitLoad.Task;
        };
        hp = npc.Data.CurrentHp;
        manager.LaunchTowerAttack(tower, npc, 10, "Assets/Attack.prefab", "Assets/Hit.prefab");
        await hitRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(npc.Data.CurrentHp == hp - 10 && !hitLoad.Task.IsCompleted, "impact settlement does not wait for the hit effect resource");
        EffectManager.Instance.LoadEffectOverride = null;
        hitLoad.SetResult(new GameObject());

        // Hold the actual BattleEffect helper at its async resource boundary, then end the battle.
        var load = new TaskCompletionSource<GameObject>();
        EffectManager.Instance.NextLoad = load;
        GameObject lateProjectile = new GameObject();
        lateProjectile.DestroyedNotification = new TaskCompletionSource();
        hp = npc.Data.CurrentHp;
        AbilityAttackHandle lateAttack = manager.LaunchTowerAttack(tower, npc, 10, "Assets/Attack.prefab", "Assets/Hit.prefab");
        Check(lateAttack.IsValid && npc.Data.CurrentHp == hp, "actual visual helper waits for launch resource without early damage");
        bool endedDuringNotification = false;
        Messager.Instance.OnNotify = (topic, _) => { if (topic == BattleMessageTopic.BattleStateChanged && BattleFlowManager.Instance.HasEnded) endedDuringNotification = !manager.IsAvailable; };
        BattleFlowManager.Instance.CompleteDefeat("verification");
        Messager.Instance.OnNotify = null;
        Check(endedDuringNotification, "battle end guard closes before completion notifications run");
        load.SetResult(lateProjectile);
        await lateProjectile.DestroyedNotification.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(lateProjectile.Destroyed && npc.Data.CurrentHp == hp && manager.ResolveTowerAttack(lateAttack).AppliedAmount == 0, "battle teardown discards a late-loaded projectile and prevents settlement");
        Check(!manager.IsAvailable && !manager.Cast(owner, AbilityKey.ByName("test")).Accepted && !manager.GrantAbility(owner, AbilityKey.ByName("test")), "completed battle cannot recreate runtime through commands");
        BattleFlowManager.Instance.BeginBattle(new MapConfig());
        AbilityOwnerHandle fresh = manager.FindOwner(npc);
        Check(fresh.IsValid && !fresh.Equals(owner) && !manager.GrantAbility(owner, AbilityKey.ByName("test")), "new battle invalidates old owner handles");
        AbilityEffectHandle oldEffect = manager.ApplyStatusEffect(fresh, "buff");
        BattleFlowManager.Instance.CompleteDefeat("verification");
        BattleFlowManager.Instance.BeginBattle(new MapConfig());
        AbilityEffectHandle freshEffect = manager.ApplyStatusEffect(manager.FindOwner(npc), "buff");
        Check(!manager.RemoveStatusEffect(oldEffect) && manager.TryGetStatusEffect(freshEffect, out _), "stale effect handles cannot remove new effects with reused IDs");

        // Base death ends the battle synchronously inside settlement but still returns the HP fact.
        BaseManager.Instance.ApplyFinalDamage(BaseManager.Instance.CurrentLife - 5, out _);
        DamageResult lethal = manager.Engine.ApplyAttackDamage(new DamageInfo { Attacker = manager.GetUnit(npc), Victim = manager.GetBaseUnit(), Amount = 100, DamageType = DamageType.Pure });
        Check(lethal.AppliedAmount == 5 && lethal.Killed && !manager.IsAvailable, "actual lethal base settlement preserves fact across battle cleanup");
        manager.Release();
        Check(!manager.BindOwner(npc).IsValid && !manager.IsAvailable, "release closes binding creation");

        ResourceManager.Instance.Assets["Assets/Abilities/sources.json"] = new TextAsset("{\"schemaVersion\":1,\"sources\":[{\"enabled\":false,\"assetPath\":\"Assets/Abilities/missing.json\"},{\"enabled\":true,\"assetPath\":\"Assets/Abilities/test.json\"}]}");
        ResourceManager.Instance.Assets["Assets/Abilities/test.json"] = new TextAsset("{\"schemaVersion\":1,\"namespace\":\"verify\",\"abilities\":[{\"name\":\"json.test\",\"id\":\"json:test\"}]}");
        AbilityDefinitionBundle bundle = new TdJsonAbilityManifestProvider("Assets/Abilities/sources.json").Load();
        Check(bundle.Validation.IsValid && bundle.Abilities.Count == 1, "explicit manifest loads only enabled assets");
        ResourceManager.Instance.Assets["Assets/Abilities/bad.json"] = new TextAsset("{\"schemaVersion\":1,\"sources\":[{\"enabled\":true,\"assetPath\":\"Assets/Abilities/missing.json\"}]}");
        Check(!new TdJsonAbilityManifestProvider("Assets/Abilities/bad.json").Load().Validation.IsValid, "missing enabled resource fails manifest validation");
        string[] invalidManifests =
        {
            "{\"schemaVersion\":1,\"sources\":[{\"enable\":true,\"assetPath\":\"Assets/Abilities/test.json\"}]}",
            "{\"schemaVersion\":1,\"sources\":[{\"enabled\":true,\"assetpath\":\"Assets/Abilities/test.json\"}]}",
            "{\"schemaVersion\":1,\"sources\":[{\"enabled\":\"true\",\"assetPath\":\"Assets/Abilities/test.json\"}]}",
            "{\"schemaVersion\":1,\"sources\":[{\"enabled\":true,\"enabled\":false}]}",
            "{\"sources\":[]}",
            "{\"schemaVersion\":1,\"sources\":[],}"
        };
        for (int i = 0; i < invalidManifests.Length; i++)
        {
            ResourceManager.Instance.Assets["Assets/Abilities/invalid.json"] = new TextAsset(invalidManifests[i]);
            Check(!new TdJsonAbilityManifestProvider("Assets/Abilities/invalid.json").Load().Validation.IsValid, "strict manifest rejects unknown/typed/duplicate/version/malformed input " + i);
        }
        Check(!manager.ConfigureJsonSources("Assets/Data/Json/generated.json"), "generated JSON cannot become a hand-authored manifest");
        Check(!new TdResourceOwner(1).HasMana(float.NaN) && !new TdResourceOwner(1).SpendMana(float.PositiveInfinity), "nonfinite resource requests are rejected");
        Console.WriteLine($"Business verification: {passed} passed.");
    }

    private sealed class TestDefinitions : IAbilityDefinitionProvider
    {
        public string ProviderName => "Business verification code definitions";
        public AbilityDefinitionSourceType SourceType => AbilityDefinitionSourceType.Code;
        public AbilityDefinitionBundle Load()
        {
            var bundle = new AbilityDefinitionBundle();
            AddAbility(bundle, new AbilityDefinition { Name = "test", MaxLevel = 5 }, "code:test");
            AddAbility(bundle, new AbilityDefinition { Name = "skill_99", MaxLevel = 5 });
            AddAbility(bundle, new AbilityDefinition { Name = "slow", CastPoint = LevelValue.Constant(1) });
            AddAbility(bundle, new AbilityDefinition { Name = "point", Behavior = AbilityBehavior.PointTarget, TargetType = UnitType.Hero, CastRange = LevelValue.Constant(10), ManaCost = LevelValue.Constant(10) });
            AddAbility(bundle, new AbilityDefinition { Name = "timer", Cooldown = LevelValue.Constant(10), ManaCost = LevelValue.Constant(2), Charges = new ChargeDefinition { MaxCharges = 2, RestoreTime = 4 } });
            AddAbility(bundle, new AbilityDefinition { Name = "optional", Behavior = AbilityBehavior.OptionalUnitTarget, TargetType = UnitType.Building, CastRange = LevelValue.Constant(10) });
            AddAbility(bundle, new AbilityDefinition { Name = "optional.empty", Behavior = AbilityBehavior.OptionalUnitTarget, TargetType = UnitType.Hero, CastRange = LevelValue.Constant(10) });
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = new ModifierDefinition { Name = "buff", Duration = 5 }, Origin = new AbilityDefinitionOrigin { SourceType = AbilityDefinitionSourceType.Code } });
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = new ModifierDefinition { Name = "disarm", Duration = 5, States = UnitState.Disarmed }, Origin = new AbilityDefinitionOrigin { SourceType = AbilityDefinitionSourceType.Code } });
            ModifierDefinition reduction = new ModifierDefinition { Name = "cdr", Duration = 5 };
            reduction.Properties[ModifierProperty.CooldownReductionPercent] = 50f;
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = reduction, Origin = new AbilityDefinitionOrigin { SourceType = AbilityDefinitionSourceType.Code } });
            return bundle;
        }
        private static void AddAbility(AbilityDefinitionBundle bundle, AbilityDefinition definition, string id = null)
        {
            bundle.Abilities.Add(new AbilityDefinitionRegistration { Definition = definition, Origin = new AbilityDefinitionOrigin { SourceType = AbilityDefinitionSourceType.Code, StableId = id } });
        }
    }
}
