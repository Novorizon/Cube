using System.Collections.Generic;

namespace Game.Skill
{
    /// <summary>
    /// 单个单位拥有的技能记录。
    /// 这里保存业务层适配出来的 owner、技能配置，以及该技能释放时使用的资源提供者。
    /// </summary>
    public sealed class SkillAbilityRecord
    {
        public ISkillUnit Owner;
        public ISkillResourceOwner ResourceOwner;
        public SkillConfigData Config;
        public bool IntrinsicModifierAdded;
    }

    /// <summary>
    /// 单位技能持有表。
    /// 它解决“某个单位拥有哪些技能”以及“被动技能自动挂 intrinsic modifier”的问题。
    /// 当前塔防最小闭环可以先直接调用 SkillManager.Cast，不一定必须使用 AbilityBook。
    /// </summary>
    public sealed class SkillAbilityBook
    {
        private readonly Dictionary<int, List<SkillAbilityRecord>> ownerAbilityMap = new Dictionary<int, List<SkillAbilityRecord>>();
        private readonly SkillManager skillManager;

        public SkillAbilityBook(SkillManager skillManager)
        {
            this.skillManager = skillManager;
        }

        /// <summary>
        /// 给单位添加技能。若配置了 intrinsicModifierId，会自动给 owner 添加一个永久 Modifier。
        /// </summary>
        public bool AddAbility(ISkillUnit owner, SkillConfigData config, ISkillResourceOwner resourceOwner = null)
        {
            if (owner == null || config == null)
            {
                return false;
            }

            skillManager.RegisterConfig(config);

            if (!ownerAbilityMap.TryGetValue(owner.RuntimeId, out List<SkillAbilityRecord> abilities))
            {
                abilities = new List<SkillAbilityRecord>();
                ownerAbilityMap.Add(owner.RuntimeId, abilities);
            }

            SkillAbilityRecord existing = FindAbility(abilities, config.Id);

            if (existing != null)
            {
                existing.Config = config;
                existing.ResourceOwner = resourceOwner;
                TryAddIntrinsicModifier(existing);
                return true;
            }

            SkillAbilityRecord record = new SkillAbilityRecord();
            record.Owner = owner;
            record.Config = config;
            record.ResourceOwner = resourceOwner;

            abilities.Add(record);
            TryAddIntrinsicModifier(record);

            return true;
        }

        public bool HasAbility(ISkillUnit owner, int skillId)
        {
            if (owner == null)
            {
                return false;
            }

            if (!ownerAbilityMap.TryGetValue(owner.RuntimeId, out List<SkillAbilityRecord> abilities))
            {
                return false;
            }

            return FindAbility(abilities, skillId) != null;
        }

        /// <summary>
        /// 释放 owner 已拥有的技能。适合英雄技能栏、塔拥有固定技能等场景。
        /// </summary>
        public bool Cast(ISkillUnit owner, int skillId, ISkillUnit targetUnit = null)
        {
            if (!TryGetAbility(owner, skillId, out SkillAbilityRecord record))
            {
                return false;
            }

            SkillCastRequest request = new SkillCastRequest(skillId, owner);
            request.TargetUnit = targetUnit;
            request.TargetPosition = targetUnit != null ? targetUnit.Position : owner.Position;
            request.ResourceOwner = record.ResourceOwner;

            return skillManager.Cast(request);
        }

        public bool TryGetAbility(ISkillUnit owner, int skillId, out SkillAbilityRecord record)
        {
            record = null;

            if (owner == null)
            {
                return false;
            }

            if (!ownerAbilityMap.TryGetValue(owner.RuntimeId, out List<SkillAbilityRecord> abilities))
            {
                return false;
            }

            record = FindAbility(abilities, skillId);
            return record != null;
        }

        public IReadOnlyList<SkillAbilityRecord> GetAbilities(ISkillUnit owner)
        {
            if (owner == null)
            {
                return System.Array.Empty<SkillAbilityRecord>();
            }

            if (!ownerAbilityMap.TryGetValue(owner.RuntimeId, out List<SkillAbilityRecord> abilities))
            {
                return System.Array.Empty<SkillAbilityRecord>();
            }

            return abilities;
        }

        public void Clear()
        {
            ownerAbilityMap.Clear();
        }

        private static SkillAbilityRecord FindAbility(List<SkillAbilityRecord> abilities, int skillId)
        {
            if (abilities == null)
            {
                return null;
            }

            for (int i = 0; i < abilities.Count; i++)
            {
                SkillAbilityRecord record = abilities[i];

                if (record != null && record.Config != null && record.Config.Id == skillId)
                {
                    return record;
                }
            }

            return null;
        }

        private void TryAddIntrinsicModifier(SkillAbilityRecord record)
        {
            if (record == null || record.Owner == null || record.Config == null)
            {
                return;
            }

            if (record.IntrinsicModifierAdded)
            {
                return;
            }

            if (record.Config.IntrinsicModifierId <= 0)
            {
                return;
            }

            bool added = skillManager.AddModifier(record.Owner, record.Owner, record.Config.IntrinsicModifierId, -1f, record.Config.Id);
            record.IntrinsicModifierAdded = added;
        }
    }
}