using UnityEngine;

namespace Skill2
{
    public abstract class SkillAbilityScript
    {
        public SkillAbility Ability { get; private set; }
        public Skill2Engine Engine => Ability != null ? Ability.Engine : null;
        public ISkill2Unit Caster => Ability != null ? Ability.Owner : null;

        internal void Bind(SkillAbility ability)
        {
            Ability = ability;
        }

        public virtual void OnCreated()
        {
        }

        public virtual void OnRemoved()
        {
        }

        public virtual void OnUpgrade()
        {
        }

        public virtual string GetIntrinsicModifierName()
        {
            return Ability != null ? Ability.Definition.IntrinsicModifierName : null;
        }

        public virtual SkillCastResult CastFilter(SkillCastContext context)
        {
            return SkillCastResult.Ok();
        }

        public virtual float GetCooldown()
        {
            return Ability != null ? Ability.Definition.Cooldown.GetValue(Ability.Level) : 0f;
        }

        public virtual float GetManaCost()
        {
            return Ability != null ? Ability.Definition.ManaCost.GetValue(Ability.Level) : 0f;
        }

        public virtual float GetCastRange()
        {
            return Ability != null ? Ability.Definition.CastRange.GetValue(Ability.Level) : 0f;
        }

        public virtual float GetAoeRadius()
        {
            return Ability != null ? Ability.Definition.AoeRadius.GetValue(Ability.Level) : 0f;
        }

        public virtual float GetCastPoint()
        {
            return Ability != null ? Ability.Definition.CastPoint.GetValue(Ability.Level) : 0f;
        }

        public virtual float GetChannelTime()
        {
            return Ability != null ? Ability.Definition.ChannelTime.GetValue(Ability.Level) : 0f;
        }

        public virtual void OnSpellStart(SkillCastContext context)
        {
        }

        public virtual void OnChannelThink(float deltaTime)
        {
        }

        public virtual void OnChannelFinish(bool interrupted)
        {
        }

        public virtual void OnToggle(bool enabled)
        {
        }

        public virtual bool OnProjectileHit(SkillProjectile projectile, ISkill2Unit target, Vector3 position)
        {
            return false;
        }

        protected float GetSpecialValue(string name)
        {
            return Ability != null ? Ability.GetSpecialValue(name) : 0f;
        }
    }

    public sealed class DefaultSkillAbilityScript : SkillAbilityScript
    {
    }
}
