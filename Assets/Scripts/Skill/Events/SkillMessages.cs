namespace Game.Skill
{
    public enum SkillMessageTopic
    {
        CastSucceeded = 1,
        CastFailed = 2,
        CooldownStarted = 3,
        CooldownFinished = 4,
        ModifierAdded = 5,
        ModifierRemoved = 6,
        ActionExecuted = 7
    }
}
