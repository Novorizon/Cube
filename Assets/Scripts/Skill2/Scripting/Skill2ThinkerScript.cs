namespace Skill2
{
    public abstract class SkillThinkerScript
    {
        public SkillThinker Thinker { get; private set; }
        public Skill2Engine Engine => Thinker != null ? Thinker.Engine : null;
        public SkillAbility Ability => Thinker != null ? Thinker.Ability : null;
        public ISkill2Unit Caster => Thinker != null ? Thinker.Caster : null;

        internal void Bind(SkillThinker thinker)
        {
            Thinker = thinker;
        }

        public virtual void OnCreated()
        {
        }

        public virtual void OnIntervalThink()
        {
        }

        public virtual void OnDestroy()
        {
        }
    }

    public sealed class DefaultSkillThinkerScript : SkillThinkerScript
    {
    }
}
