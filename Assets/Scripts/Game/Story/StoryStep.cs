namespace Game
{
    public sealed class StoryStep
    {
        public int Id;
        public int StepIndex;
        public StoryStepType StepType = StoryStepType.Text;
        public string Text;
        public string IllustrationPath;
        public StoryMotionPreset MotionPreset = StoryMotionPreset.None;
        public float MotionDuration;
        public StoryAdvanceMode AdvanceMode = StoryAdvanceMode.Click;
        public float AutoAdvanceDelay;
        public string GuideTargetId;
        public string GuideText;
        public bool AllowTargetInteraction;

        public bool UsesText => StepType == StoryStepType.Text || StepType == StoryStepType.Mixed;
        public bool UsesIllustration => StepType == StoryStepType.Illustration || StepType == StoryStepType.Mixed;
        public bool UsesGuide => StepType == StoryStepType.Guide;
    }
}
