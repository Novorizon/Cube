using UnityEngine;

namespace Game.Skill
{
    public sealed class SkillEventData
    {
        public SkillMessageTopic Topic;
        public int SkillId;
        public int ActionId;
        public int ModifierId;
        public ISkillUnit Caster;
        public ISkillUnit Target;
        public Vector3 Position;
        public float Value;
        public string Reason;
    }
}
