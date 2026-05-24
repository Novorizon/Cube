using UnityEngine;

namespace Ability
{
    public abstract class AbilityScript
    {
        public Ability Ability { get; private set; }
        public AbilitySystem Engine => Ability != null ? Ability.Engine : null;
        public IUnit Caster => Ability != null ? Ability.Owner : null;

        internal void Bind(Ability ability)
        {
            Ability = ability;
        }

        public virtual void OnCreated() { }
        public virtual void OnRemoved() { }
        public virtual void OnUpgrade() { }

        public virtual string GetIntrinsicModifierName()
        {
            return Ability != null ? Ability.Definition.IntrinsicModifierName : null;
        }

        public virtual CastResult CastFilter(CastContext context)
        {
            return CastResult.Ok();
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

        public virtual void OnSpellStart(CastContext context) { }
        public virtual void OnChannelThink(float deltaTime) { }
        public virtual void OnChannelFinish(bool interrupted) { }
        public virtual void OnToggle(bool enabled) { }

        public virtual bool OnProjectileHit(Projectile projectile, IUnit target, Vector3 position)
        {
            return false;
        }

        protected float GetSpecialValue(string name)
        {
            return Ability != null ? Ability.GetSpecialValue(name) : 0f;
        }
    }

    public sealed class DefaultAbilityScript : AbilityScript
    {
    }
}
