using System;
using System.Collections;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class StoryPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Story/StoryPanel.prefab";

        [Header("Background")]
        [SerializeField] private Image backgroundBlocker;

        [Header("Illustration")]
        [SerializeField] private GameObject illustrationRoot;
        [SerializeField] private RawImage illustrationView;
        [SerializeField] private AspectRatioFitter illustrationAspectFitter;
        [SerializeField] private StoryMotionPlayer motionPlayer;

        [Header("Text")]
        [SerializeField] private GameObject storyCard;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button continueButton;

        [Header("Guide")]
        [SerializeField] private GuideOverlay guideOverlay;

        private readonly SimpleGuideManager guideManager = new SimpleGuideManager();
        private StoryConfig config;
        private Action<int> stepChanged;
        private Action completed;
        private int stepIndex;
        private int stepVersion;
        private bool completing;

        public override bool HideOnBack => false;

        public sealed class Args
        {
            public StoryConfig Config { get; }
            public int InitialStepIndex { get; }
            public Action<int> StepChanged { get; }
            public Action Completed { get; }

            public Args(StoryConfig config, int initialStepIndex, Action<int> stepChanged, Action completed)
            {
                Config = config;
                InitialStepIndex = initialStepIndex;
                StepChanged = stepChanged;
                Completed = completed;
            }
        }

        protected override void OnCreate()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            ValidateBindings();
        }

        protected override void OnOpen(object args)
        {
            Args storyArgs = args as Args;
            config = storyArgs?.Config;
            stepChanged = storyArgs?.StepChanged;
            completed = storyArgs?.Completed;
            completing = false;
            stepVersion = 0;

            if (config?.Steps == null || config.Steps.Length == 0)
            {
                CompleteAndClose();
                return;
            }

            stepIndex = Mathf.Clamp(storyArgs != null ? storyArgs.InitialStepIndex : 0, 0, config.Steps.Length - 1);
            RefreshStep();
        }

        protected override void OnClose()
        {
            StopAllCoroutines();
            motionPlayer?.Stop();
            guideManager.Hide();
            config = null;
            stepChanged = null;
            completed = null;
            completing = false;
        }

        protected override void OnDestroyed()
        {
            guideManager.Dispose();
        }

        private void OnContinueClicked()
        {
            StoryStep step = GetCurrentStep();
            if (step == null || step.AdvanceMode != StoryAdvanceMode.Click || completing)
            {
                return;
            }

            AdvanceStep();
        }

        private void RefreshStep()
        {
            StopAllCoroutines();
            motionPlayer?.Stop();
            guideManager.Hide();
            stepVersion++;

            StoryStep step = GetCurrentStep();
            if (step == null)
            {
                CompleteAndClose();
                return;
            }

            bool isGuide = step.UsesGuide;
            bool showText = step.UsesText || (isGuide && step.AdvanceMode == StoryAdvanceMode.Click);
            bool showIllustration = step.UsesIllustration;

            if (backgroundBlocker != null)
            {
                backgroundBlocker.color = isGuide ? Color.clear : new Color(0f, 0f, 0f, 0.72f);
                backgroundBlocker.raycastTarget = !isGuide;
            }

            SetActive(storyCard, showText);
            SetActive(illustrationRoot, showIllustration);

            if (titleText != null)
            {
                titleText.text = !string.IsNullOrWhiteSpace(config.Title) ? config.Title : config.Id.ToString();
            }

            if (bodyText != null)
            {
                bodyText.text = isGuide && !string.IsNullOrWhiteSpace(step.GuideText)
                    ? step.GuideText
                    : step.Text ?? string.Empty;
            }

            RefreshProgress(step);

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(step.AdvanceMode == StoryAdvanceMode.Click);
            }

            if (isGuide && step.AdvanceMode == StoryAdvanceMode.Click && storyCard != null)
            {
                storyCard.transform.SetAsLastSibling();
            }
            else if (guideOverlay != null)
            {
                guideOverlay.transform.SetAsLastSibling();
            }

            if (showIllustration)
            {
                LoadIllustration(step.IllustrationPath);
            }
            else if (illustrationView != null)
            {
                illustrationView.texture = null;
            }

            int version = stepVersion;
            if (isGuide)
            {
                guideManager.Show(step, guideOverlay, () => AdvanceIfCurrent(version));
            }

            if (showIllustration)
            {
                motionPlayer?.Play(
                    step.MotionPreset,
                    step.MotionDuration,
                    step.AdvanceMode == StoryAdvanceMode.MotionComplete
                        ? () => AdvanceIfCurrent(version)
                        : null);
            }
            else if (step.AdvanceMode == StoryAdvanceMode.MotionComplete)
            {
                StartCoroutine(AdvanceAfterDelay(0f, version));
            }

            if (step.AdvanceMode == StoryAdvanceMode.AutoAfterDelay)
            {
                StartCoroutine(AdvanceAfterDelay(step.AutoAdvanceDelay, version));
            }
        }

        private IEnumerator AdvanceAfterDelay(float delay, int version)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return null;
            }

            AdvanceIfCurrent(version);
        }

        private void AdvanceIfCurrent(int version)
        {
            if (version == stepVersion && !completing)
            {
                AdvanceStep();
            }
        }

        private void AdvanceStep()
        {
            if (config?.Steps == null || completing)
            {
                return;
            }

            if (stepIndex >= config.Steps.Length - 1)
            {
                CompleteAndClose();
                return;
            }

            stepIndex++;
            stepChanged?.Invoke(stepIndex);
            RefreshStep();
        }

        private void CompleteAndClose()
        {
            if (completing)
            {
                return;
            }

            completing = true;
            Action callback = completed;
            completed = null;
            UIManager.Instance.Panels.Hide(PrefabPath);
            callback?.Invoke();
        }

        private StoryStep GetCurrentStep()
        {
            if (config?.Steps == null || stepIndex < 0 || stepIndex >= config.Steps.Length)
            {
                return null;
            }

            return config.Steps[stepIndex];
        }

        private void RefreshProgress(StoryStep step)
        {
            if (progressText == null)
            {
                return;
            }

            StoryProgressDisplayMode mode = GameConfig.Story.ProgressDisplayMode;
            bool visible = mode != StoryProgressDisplayMode.Hidden &&
                           step != null &&
                           step.UsesText;
            progressText.gameObject.SetActive(visible);
            if (!visible)
            {
                progressText.text = string.Empty;
                return;
            }

            if (mode == StoryProgressDisplayMode.AllSteps)
            {
                progressText.text = $"{stepIndex + 1}/{config.Steps.Length}";
                return;
            }

            int currentDialogue = 0;
            int totalDialogues = 0;
            for (int i = 0; i < config.Steps.Length; i++)
            {
                StoryStep candidate = config.Steps[i];
                if (candidate == null || !candidate.UsesText)
                {
                    continue;
                }

                totalDialogues++;
                if (i <= stepIndex)
                {
                    currentDialogue++;
                }
            }

            progressText.text = totalDialogues > 0
                ? $"{currentDialogue}/{totalDialogues}"
                : string.Empty;
        }

        private void LoadIllustration(string assetPath)
        {
            if (illustrationView == null)
            {
                return;
            }

            illustrationView.texture = null;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            Texture2D texture = ResourceManager.Instance.LoadAsset<Texture2D>(assetPath);
            if (texture == null)
            {
                Debug.LogWarning($"[{nameof(StoryPanel)}] Illustration texture could not be loaded: {assetPath}", this);
                return;
            }

            illustrationView.texture = texture;
            if (illustrationAspectFitter != null && texture.height > 0)
            {
                illustrationAspectFitter.aspectRatio = (float)texture.width / texture.height;
            }
        }

        private void ValidateBindings()
        {
            if (backgroundBlocker == null ||
                illustrationRoot == null ||
                illustrationView == null ||
                illustrationAspectFitter == null ||
                motionPlayer == null ||
                storyCard == null ||
                titleText == null ||
                bodyText == null ||
                progressText == null ||
                continueButton == null ||
                guideOverlay == null)
            {
                Debug.LogError($"[{nameof(StoryPanel)}] Prefab bindings are incomplete: {PrefabPath}", this);
            }
        }

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }
    }
}
