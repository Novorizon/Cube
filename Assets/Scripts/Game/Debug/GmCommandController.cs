#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game
{
    public sealed class GmCommandController : MonoBehaviour
    {
        private const int DefaultAddCount = 1;

        private GameObject root;
        private TMP_InputField inputField;
        private TMP_Text hintText;

        private bool IsOpen => root != null && root.activeSelf;

        public static void EnsureExists()
        {
            if (FindObjectOfType<GmCommandController>() != null)
            {
                return;
            }

            GameObject go = new GameObject("GmCommandController");
            DontDestroyOnLoad(go);
            go.AddComponent<GmCommandController>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!IsOpen)
                {
                    Open();
                }
            }

            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void Open()
        {
            EnsureView();
            root.SetActive(true);
            inputField.text = string.Empty;
            SetHint("GM: add <itemId> [count] | camera follow/free | time scale/set/season");
            inputField.ActivateInputField();
            inputField.Select();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            }
        }

        private void Close()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void EnsureView()
        {
            if (root != null)
            {
                return;
            }

            Canvas canvas = CreateCanvas();
            root = CreatePanel(canvas.transform);
            inputField = CreateInputField(root.transform);
            hintText = CreateText(root.transform, "Hint", 13, TextAlignmentOptions.Left);
            RectTransform hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 8f);
            hintRect.sizeDelta = new Vector2(-24f, 20f);

            inputField.onSubmit.AddListener(ExecuteAndClose);
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("GmCommandCanvas");
            DontDestroyOnLoad(canvasObject);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static GameObject CreatePanel(Transform parent)
        {
            GameObject panel = new GameObject("GmCommandPanel");
            panel.transform.SetParent(parent, false);

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.05f, 0.06f, 0.08f, 0.94f);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -80f);
            rect.sizeDelta = new Vector2(620f, 92f);

            panel.SetActive(false);
            return panel;
        }

        private static TMP_InputField CreateInputField(Transform parent)
        {
            GameObject inputObject = new GameObject("Input");
            inputObject.transform.SetParent(parent, false);

            Image image = inputObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.12f);

            TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;

            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(-24f, 42f);

            TMP_Text text = CreateText(inputObject.transform, "Text", 20, TextAlignmentOptions.Left);
            text.color = Color.white;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            TMP_Text placeholder = CreateText(inputObject.transform, "Placeholder", 18, TextAlignmentOptions.Left);
            placeholder.color = new Color(1f, 1f, 1f, 0.45f);
            placeholder.text = "add 20700001 / camera follow";
            RectTransform placeholderRect = placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(12f, 4f);
            placeholderRect.offsetMax = new Vector2(-12f, -4f);

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static TMP_Text CreateText(Transform parent, string name, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private void ExecuteAndClose(string commandText)
        {
            if (TryExecute(commandText, out string message))
            {
                Debug.Log($"[GM] {message}");
                Close();
                return;
            }

            Debug.LogWarning($"[GM] {message}");
            SetHint(message);
            inputField.ActivateInputField();
            inputField.Select();
        }

        private bool TryExecute(string commandText, out string message)
        {
            if (string.IsNullOrWhiteSpace(commandText))
            {
                message = "Empty command.";
                return false;
            }

            string[] parts = commandText.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                message = "Empty command.";
                return false;
            }

            string command = parts[0].ToLowerInvariant();
            switch (command)
            {
                case "add":
                    return ExecuteAdd(parts, out message);
                case "camera":
                    return ExecuteCamera(parts, out message);
                case "time":
                    return ExecuteTime(parts, out message);
                default:
                    message = $"Unknown command: {parts[0]}";
                    return false;
            }
        }

        private static bool ExecuteTime(string[] parts, out string message)
        {
            if (parts.Length < 2)
            {
                message = "Usage: time scale <value> | time set <hour> [minute] | time date <year> <month> <day> <hour> [minute] | time season spring/summer/autumn/winter";
                return false;
            }

            string mode = parts[1].ToLowerInvariant();
            switch (mode)
            {
                case "scale":
                case "speed":
                    return ExecuteTimeScale(parts, out message);
                case "set":
                    return ExecuteTimeSet(parts, out message);
                case "date":
                    return ExecuteTimeDate(parts, out message);
                case "season":
                    return ExecuteTimeSeason(parts, out message);
                default:
                    message = "Usage: time scale/set/date/season";
                    return false;
            }
        }

        private static bool ExecuteTimeScale(string[] parts, out string message)
        {
            if (parts.Length < 3 ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float scale) ||
                scale < 0f)
            {
                message = "Usage: time scale <value>. Example: time scale 120";
                return false;
            }

            CalendarManager.Instance.SetGameTimeScale(scale);
            message = $"Game time scale: {scale.ToString("0.###", CultureInfo.InvariantCulture)}";
            return true;
        }

        private static bool ExecuteTimeSet(string[] parts, out string message)
        {
            if (parts.Length < 3 ||
                !int.TryParse(parts[2], out int hour))
            {
                message = "Usage: time set <hour> [minute]. Example: time set 12 0";
                return false;
            }

            int minute = 0;
            if (parts.Length >= 4 && !int.TryParse(parts[3], out minute))
            {
                message = "Minute must be an integer.";
                return false;
            }

            CalendarManager.Instance.SetTimeOfDay(hour, minute);
            WorldMainPanel.Instance?.RefreshNow();
            message = $"Game time: {CalendarManager.Instance.GetDateText()} {CalendarManager.Instance.GetTimeText()}";
            return true;
        }

        private static bool ExecuteTimeDate(string[] parts, out string message)
        {
            if (parts.Length < 6 ||
                !int.TryParse(parts[2], out int year) ||
                !int.TryParse(parts[3], out int month) ||
                !int.TryParse(parts[4], out int day) ||
                !int.TryParse(parts[5], out int hour))
            {
                message = "Usage: time date <year> <month> <day> <hour> [minute]. Example: time date 1 2 1 12 0";
                return false;
            }

            int minute = 0;
            if (parts.Length >= 7 && !int.TryParse(parts[6], out minute))
            {
                message = "Minute must be an integer.";
                return false;
            }

            CalendarManager.Instance.SetDateTime(year, month, day, hour, minute);
            WorldMainPanel.Instance?.RefreshNow();
            message = $"Game time: {CalendarManager.Instance.GetDateText()} {CalendarManager.Instance.GetTimeText()}";
            return true;
        }

        private static bool ExecuteTimeSeason(string[] parts, out string message)
        {
            if (parts.Length < 3 || !TryParseSeason(parts[2], out Season season))
            {
                message = "Usage: time season spring/summer/autumn/winter";
                return false;
            }

            CalendarManager calendar = CalendarManager.Instance;
            int month = ((int)season - 1) * CalendarManager.MonthsPerSeason + 1;
            calendar.SetDateTime(calendar.Year, month, 1, calendar.Hour, calendar.Minute);
            WorldMainPanel.Instance?.RefreshNow();
            message = $"Season: {CalendarManager.GetSeasonName(calendar.Season)}";
            return true;
        }

        private static bool TryParseSeason(string value, out Season season)
        {
            switch ((value ?? string.Empty).ToLowerInvariant())
            {
                case "spring":
                case "1":
                    season = Season.Spring;
                    return true;
                case "summer":
                case "2":
                    season = Season.Summer;
                    return true;
                case "autumn":
                case "fall":
                case "3":
                    season = Season.Autumn;
                    return true;
                case "winter":
                case "4":
                    season = Season.Winter;
                    return true;
                default:
                    season = default;
                    return false;
            }
        }

        private static bool ExecuteCamera(string[] parts, out string message)
        {
            if (parts.Length < 2)
            {
                message = "Usage: camera follow/free";
                return false;
            }

            if (GameplayController.Instance == null)
            {
                message = "GameplayController is not active.";
                return false;
            }

            string mode = parts[1].ToLowerInvariant();
            switch (mode)
            {
                case "follow":
                case "followplayer":
                    GameplayController.Instance.SetCameraFollowMode(CameraFollowMode.FollowPlayer);
                    message = "Camera mode: FollowPlayer";
                    return true;

                case "free":
                    GameplayController.Instance.SetCameraFollowMode(CameraFollowMode.Free);
                    message = "Camera mode: Free";
                    return true;

                default:
                    message = "Usage: camera follow/free";
                    return false;
            }
        }

        private static bool ExecuteAdd(string[] parts, out string message)
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out int itemId) || itemId <= 0)
            {
                message = "Usage: add <itemId> [count]";
                return false;
            }

            int count = DefaultAddCount;
            if (parts.Length >= 3 && (!int.TryParse(parts[2], out count) || count <= 0))
            {
                message = "Count must be a positive integer.";
                return false;
            }

            if (!BagManager.Instance.TryAddItem(itemId, count))
            {
                message = $"Add item failed. itemId: {itemId}, count: {count}";
                return false;
            }

            if (ToolKitDefinitions.TryGetTool(itemId, out _))
            {
                ToolKitManager.Instance.TrySelectToolItem(itemId);
            }

            WorldMainPanel.Instance?.RefreshNow();
            message = $"Added item. itemId: {itemId}, count: {count}";
            return true;
        }

        private void SetHint(string message)
        {
            if (hintText != null)
            {
                hintText.text = message ?? string.Empty;
            }
        }
    }
}

#endif
