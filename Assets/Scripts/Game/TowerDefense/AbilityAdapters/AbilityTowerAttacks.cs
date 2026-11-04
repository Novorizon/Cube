using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Ability;
using UnityEngine;

namespace Game
{
    public sealed partial class AbilityManager
    {
        private readonly Dictionary<long, PendingTowerAttack> pendingTowerAttacks = new Dictionary<long, PendingTowerAttack>();
        private readonly List<long> staleAttackIds = new List<long>();

        public AbilityAttackHandle BeginTowerAttack(Tower tower, Npc target, float damage, int extraSkillId = 0)
        {
            if (!IsAvailable || IsActionRestricted(tower) || IsAbilityExecuting(tower)) return default;
            TdUnit attacker = GetUnit(tower);
            TdUnit victim = GetUnit(target);
            if (attacker == null || victim == null) return default;
            AttackRecord attack = Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = damage, DamageType = DamageType.Physical });
            if (!IsAvailable || attack == null || !attack.IsPending) return default;
            pendingTowerAttacks[attack.AttackId] = new PendingTowerAttack { Record = attack, Tower = tower, Target = target, ExtraSkillId = extraSkillId };
            return new AbilityAttackHandle(battleSession, attack.AttackId);
        }

        public AbilityAttackHandle LaunchTowerAttack(Tower tower, Npc target, float damage, string projectileEffect, string hitEffect, int extraSkillId = 0)
        {
            AbilityAttackHandle handle = BeginTowerAttack(tower, target, damage, extraSkillId);
            if (!handle.IsValid) return default;
            _ = TravelTowerAttackAsync(handle, projectileEffect, hitEffect, tower.transform.position + Vector3.up * 0.8f, target.transform.position + Vector3.up * 0.6f);
            return handle;
        }

        public AbilityDamageOutcome ResolveTowerAttack(AbilityAttackHandle handle)
        {
            PendingTowerAttack pending = FindPendingTowerAttack(handle);
            if (pending == null) return new AbilityDamageOutcome(0f, false, "AttackUnavailable");
            pendingTowerAttacks.Remove(handle.AttackId);
            long generation = Engine.RuntimeGeneration;
            DamageResult result = Engine.ResolveAttack(pending.Record);
            // The configured skill is an on-hit addition; a delayed cast never runs after battle teardown.
            if (result != null && !result.Blocked && IsAvailable && handle.Session == battleSession && generation == Engine.RuntimeGeneration && pending.ExtraSkillId > 0 && pending.Target != null && pending.Record.Victim.IsAlive && pending.Tower != null)
            {
                AbilityCommandResult cast = TryCastTowerSkill(pending.Tower, pending.ExtraSkillId, pending.Target);
                if (!cast.Accepted && cast.Failure != AbilityCommandFailure.Cooldown) Debug.LogWarning($"Tower on-hit skill failed. skillId: {pending.ExtraSkillId}, reason: {cast.Reason}, message: {cast.Message}");
            }
            return result == null ? new AbilityDamageOutcome(0f, false, "AttackUnavailable") : new AbilityDamageOutcome(result.AppliedAmount, result.Killed, result.BlockReason);
        }

        public bool CancelTowerAttack(AbilityAttackHandle handle)
        {
            if (handle.Session != battleSession || !pendingTowerAttacks.TryGetValue(handle.AttackId, out PendingTowerAttack attack)) return false;
            pendingTowerAttacks.Remove(handle.AttackId);
            return Engine.CancelAttack(attack.Record);
        }

        private PendingTowerAttack FindPendingTowerAttack(AbilityAttackHandle handle)
        {
            if (!IsAvailable || !handle.IsValid || handle.Session != battleSession || !pendingTowerAttacks.TryGetValue(handle.AttackId, out PendingTowerAttack attack)) return null;
            return attack.Record.IsPending ? attack : null;
        }

        private void PruneTowerAttacks()
        {
            staleAttackIds.Clear();
            foreach (var pair in pendingTowerAttacks) if (!pair.Value.Record.IsPending) staleAttackIds.Add(pair.Key);
            foreach (long id in staleAttackIds) pendingTowerAttacks.Remove(id);
        }

        private bool CanTravel(AbilityAttackHandle handle)
        {
            PendingTowerAttack attack = FindPendingTowerAttack(handle);
            return attack != null && attack.Record.Attacker is TdUnit attacker && attacker.IsValidBinding && attack.Record.Victim is TdUnit victim && victim.IsValidBinding && victim.IsAlive;
        }

        private async Task TravelTowerAttackAsync(AbilityAttackHandle handle, string projectileEffect, string hitEffect, Vector3 origin, Vector3 target)
        {
            try
            {
                PendingTowerAttack pending = FindPendingTowerAttack(handle);
                if (pending == null) return;
                bool impacted = false;
                await BattleEffect.PlayProjectileWithHitAsync(projectileEffect, hitEffect, origin, target,
                    () => impacted ? IsAvailable && handle.Session == battleSession && pending.Record.Victim is TdUnit victim && victim.IsValidBinding : CanTravel(handle),
                    () => { if (CanTravel(handle)) { impacted = true; ResolveTowerAttack(handle); } });
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { CancelTowerAttack(handle); }
        }

        private sealed class PendingTowerAttack
        {
            public AttackRecord Record;
            public Tower Tower;
            public Npc Target;
            public int ExtraSkillId;
        }
    }
}
