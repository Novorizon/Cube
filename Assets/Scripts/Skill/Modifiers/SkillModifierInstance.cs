namespace Game.Skill
{
    public sealed class SkillModifierInstance
    {
        public int ModifierId;
        public int SourceSkillId;
        public SkillModifierData Data;
        public ISkillUnit Caster;
        public ISkillUnit Parent;
        public float Duration;
        public float TimeLeft;
        public float Interval;
        public float IntervalTimer;
        public int StackCount = 1;
    }
}
