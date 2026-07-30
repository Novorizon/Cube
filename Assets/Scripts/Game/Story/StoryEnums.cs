namespace Game
{
    public enum StoryStepType
    {
        Text = 0,
        Illustration = 1,
        Mixed = 2,
        Guide = 3,
    }

    public enum StoryAdvanceMode
    {
        Click = 0,
        MotionComplete = 1,
        AutoAfterDelay = 2,
        GuideTargetClicked = 3,
    }

    public enum StoryMotionPreset
    {
        None = 0,
        ZoomOut = 1,
        PanLeftToRight = 2,
        PanRightToLeft = 3,
        ZoomIn = 4,
    }

    public enum StoryProgressDisplayMode
    {
        Hidden = 0,
        DialogueOnly = 1,
        AllSteps = 2,
    }

    public enum StoryTriggerMode
    {
        Manual = 0,
        AutoOnNewGame = 1,
        QuestCompleted = 2,
        CustomFlag = 3,
        EnterArea = 4,
        TalkNpc = 5,
    }
}
