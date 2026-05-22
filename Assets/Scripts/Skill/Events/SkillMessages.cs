namespace Game.Skill
{
    public enum SkillMessageTopic
    {
        CastStarted = 1,
        CastSucceeded = 2,
        CastFailed = 3,
        CastInterrupted = 4,
        ChannelFinished = 5,
        CooldownStarted = 6,
        CooldownFinished = 7,
        ModifierAdded = 8,
        ModifierRemoved = 9,
        ActionExecuted = 10
    }
}
