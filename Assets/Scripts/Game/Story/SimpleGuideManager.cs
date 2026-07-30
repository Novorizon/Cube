using System;

namespace Game
{
    public sealed class SimpleGuideManager : IDisposable
    {
        private GuideOverlay overlay;
        private StoryStep step;
        private GuideTarget target;
        private Action completed;

        public void Show(StoryStep storyStep, GuideOverlay guideOverlay, Action targetClicked)
        {
            Hide();
            step = storyStep;
            overlay = guideOverlay;
            completed = targetClicked;
            GuideTargetRegistry.TargetChanged += OnTargetChanged;
            RefreshTarget();
        }

        public void Hide()
        {
            GuideTargetRegistry.TargetChanged -= OnTargetChanged;

            if (target != null)
            {
                target.Clicked -= OnTargetClicked;
                target = null;
            }

            overlay?.Hide();
            overlay = null;
            step = null;
            completed = null;
        }

        public void Dispose()
        {
            Hide();
        }

        private void OnTargetChanged(string targetId, GuideTarget changedTarget)
        {
            if (step != null && string.Equals(step.GuideTargetId, targetId, StringComparison.Ordinal))
            {
                RefreshTarget();
            }
        }

        private void RefreshTarget()
        {
            if (target != null)
            {
                target.Clicked -= OnTargetClicked;
                target = null;
            }

            if (step != null)
            {
                GuideTargetRegistry.TryGet(step.GuideTargetId, out target);
            }

            if (target != null && step.AdvanceMode == StoryAdvanceMode.GuideTargetClicked)
            {
                target.Clicked += OnTargetClicked;
            }

            bool allowInteraction = step != null &&
                (step.AllowTargetInteraction || step.AdvanceMode == StoryAdvanceMode.GuideTargetClicked);
            overlay?.Show(target, allowInteraction, step?.GuideText);
        }

        private void OnTargetClicked()
        {
            completed?.Invoke();
        }
    }
}
