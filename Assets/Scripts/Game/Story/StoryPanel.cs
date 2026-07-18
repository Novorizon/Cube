using System;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class StoryPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Story/StoryPanel.prefab";

        private TMP_Text titleText;
        private TMP_Text bodyText;
        private TMP_Text progressText;
        private Button continueButton;

        private StoryConfig config;
        private Action completed;
        private int lineIndex;
        private bool completing;

        public override bool HideOnBack => false;

        public sealed class Args
        {
            public StoryConfig Config { get; }
            public Action Completed { get; }

            public Args(StoryConfig config, Action completed)
            {
                Config = config;
                Completed = completed;
            }
        }

        protected override void OnCreate()
        {
            BuildIfNeeded();
        }

        protected override void OnOpen(object args)
        {
            BuildIfNeeded();

            Args storyArgs = args as Args;
            config = storyArgs?.Config;
            completed = storyArgs?.Completed;
            lineIndex = 0;
            completing = false;

            if (config == null)
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
                return;
            }

            Refresh();
        }

        protected override void OnClose()
        {
            config = null;
            completed = null;
            completing = false;
        }

        private void OnContinueClicked()
        {
            if (config == null || completing)
            {
                return;
            }

            int lineCount = GetLineCount(config);
            if (lineIndex < lineCount - 1)
            {
                lineIndex++;
                Refresh();
                return;
            }

            completing = true;
            Action callback = completed;
            completed = null;

            UIManager.Instance.Panels.Hide(PrefabPath);
            callback?.Invoke();
        }

        private void Refresh()
        {
            if (config == null)
            {
                return;
            }

            if (titleText != null)
            {
                titleText.text = GetTitle(config);
            }

            if (bodyText != null)
            {
                bodyText.text = GetLine(config, lineIndex);
            }

            if (progressText != null)
            {
                progressText.text = $"{lineIndex + 1}/{GetLineCount(config)}";
            }
        }

        private void BuildIfNeeded()
        {
            if (titleText != null && bodyText != null && continueButton != null)
            {
                return;
            }

            RectTransform rootRect = EnsureRectTransform(gameObject);
            Stretch(rootRect, Vector2.zero, Vector2.zero);

            Image blocker = gameObject.GetComponent<Image>();
            if (blocker == null)
            {
                blocker = gameObject.AddComponent<Image>();
            }

            blocker.color = new Color(0f, 0f, 0f, 0.62f);
            blocker.raycastTarget = true;

            GameObject card = CreateChild("StoryCard", transform);
            RectTransform cardRect = EnsureRectTransform(card);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(760f, 420f);

            Image cardImage = card.AddComponent<Image>();
            cardImage.color = new Color(0.98f, 0.92f, 0.78f, 0.97f);
            cardImage.raycastTarget = true;

            titleText = CreateText("Title", card.transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -36f);
            titleRect.sizeDelta = new Vector2(-72f, 56f);

            bodyText = CreateText("Body", card.transform, 24, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            RectTransform bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(64f, 118f);
            bodyRect.offsetMax = new Vector2(-64f, -112f);

            progressText = CreateText("Progress", card.transform, 18, FontStyles.Normal, TextAlignmentOptions.Right);
            RectTransform progressRect = progressText.rectTransform;
            progressRect.anchorMin = new Vector2(1f, 0f);
            progressRect.anchorMax = new Vector2(1f, 0f);
            progressRect.pivot = new Vector2(1f, 0f);
            progressRect.anchoredPosition = new Vector2(-64f, 64f);
            progressRect.sizeDelta = new Vector2(120f, 32f);

            continueButton = CreateButton("ContinueButton", card.transform);
            RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 36f);
            buttonRect.sizeDelta = new Vector2(220f, 56f);
            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        private static TMP_Text CreateText(string name, Transform parent, int fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = CreateChild(name, parent);
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.18f, 0.14f, 0.1f, 1f);
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            GameObject go = CreateChild(name, parent);
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.74f, 0.56f, 0.28f, 0.95f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = CreateText("Text", go.transform, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            label.text = "Continue";
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            EnsureRectTransform(go);
            return go;
        }

        private static RectTransform EnsureRectTransform(GameObject go)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = go.AddComponent<RectTransform>();
            }

            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static string GetTitle(StoryConfig config)
        {
            return !string.IsNullOrWhiteSpace(config.Title) ? config.Title : config.Id.ToString();
        }

        private static string GetLine(StoryConfig config, int index)
        {
            if (config?.Lines == null || config.Lines.Length == 0)
            {
                return string.Empty;
            }

            int safeIndex = Mathf.Clamp(index, 0, config.Lines.Length - 1);
            return config.Lines[safeIndex] ?? string.Empty;
        }

        private static int GetLineCount(StoryConfig config)
        {
            return Mathf.Max(1, config?.Lines != null ? config.Lines.Length : 0);
        }
    }
}
