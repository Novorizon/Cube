namespace Game
{
    public sealed class StoryConfig
    {
        public int Id;
        public string Title;
        public StoryStep[] Steps = System.Array.Empty<StoryStep>();
        public StoryTriggerMode TriggerMode = StoryTriggerMode.Manual;
        public int TriggerTargetId;
        public QuestEventType CompleteQuestEventType = QuestEventType.None;
        public int CompleteQuestTargetId;
        public int NextStoryId;
        public bool Repeatable;
        public bool Enable = true;
    }
}
