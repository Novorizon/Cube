using System;
using Game.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldTopBarPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text weatherText;
        [SerializeField] private RectTransform seasonImage;
        [SerializeField] private RectTransform dayNightSky;
        [SerializeField] private Button menuButton;

        private Vector2 seasonImageBaseAnchoredPosition;
        private float dayNightSkyBaseRotationZ;

        private void Awake()
        {
            CacheBaseTransforms();
        }

        public void Initialize(Action menuClicked)
        {
            CacheBaseTransforms();
            BindMenu(menuClicked);
            RefreshCalendarWidget();
            RefreshWeather();
        }

        public void RefreshCalendarWidget()
        {
            CalendarManager calendar = CalendarManager.Instance;
            SetDate(FormatTopBarDate(calendar));
            SetTime(calendar != null ? calendar.GetTimeText() : string.Empty);
            SetSeasonProgress(GetSeasonProgress(calendar));
            SetDayNightRotation(GetSkyDiskRotationZ(calendar));
        }

        public void RefreshCalendarMotion()
        {
            CalendarManager calendar = CalendarManager.Instance;
            SetTime(calendar != null ? calendar.GetTimeText() : string.Empty);
            SetSeasonProgress(GetSeasonProgress(calendar));
            SetDayNightRotation(GetSkyDiskRotationZ(calendar));
        }

        public void RefreshWeather()
        {
            SetWeather(FormatTopBarWeather());
        }

        private void BindMenu(Action clicked)
        {
            if (menuButton == null)
            {
                return;
            }

            menuButton.onClick.RemoveAllListeners();
            menuButton.onClick.AddListener(() => clicked?.Invoke());
        }

        private void SetDate(string value)
        {
            if (dateText != null)
            {
                dateText.text = value;
            }
        }

        private void SetTime(string value)
        {
            if (timeText != null)
            {
                timeText.text = value;
            }
        }

        private void SetWeather(string value)
        {
            if (weatherText != null)
            {
                weatherText.text = value;
            }
        }

        private void SetSeasonProgress(float yearProgress)
        {
            if (seasonImage == null)
            {
                return;
            }

            float clampedProgress = Mathf.Clamp01(yearProgress);
            float seasonWidth = GetSeasonWidth();
            float maxOffset = seasonWidth * (CalendarManager.SeasonsPerYear - 1);
            seasonImage.anchoredPosition = seasonImageBaseAnchoredPosition + new Vector2(-clampedProgress * maxOffset, 0f);
        }

        private void SetDayNightRotation(float rotationZ)
        {
            if (dayNightSky == null)
            {
                return;
            }

            dayNightSky.localEulerAngles = new Vector3(0f, 0f, dayNightSkyBaseRotationZ + rotationZ);
        }

        private void CacheBaseTransforms()
        {
            seasonImageBaseAnchoredPosition = seasonImage != null ? seasonImage.anchoredPosition : Vector2.zero;
            dayNightSkyBaseRotationZ = dayNightSky != null ? dayNightSky.localEulerAngles.z : 0f;
        }

        private float GetSeasonWidth()
        {
            if (seasonImage == null)
            {
                return 0f;
            }

            if (seasonImage.parent is RectTransform viewport && viewport.rect.width > 0f)
            {
                return viewport.rect.width;
            }

            float imageWidth = seasonImage.rect.width > 0f ? seasonImage.rect.width : Mathf.Abs(seasonImage.sizeDelta.x);
            return imageWidth / CalendarManager.SeasonsPerYear;
        }

        private static float GetSkyDiskRotationZ(CalendarManager calendar)
        {
            return calendar != null ? calendar.SmoothDayNightDiskRotationZ : 0f;
        }

        private static float GetSeasonProgress(CalendarManager calendar)
        {
            if (calendar == null)
            {
                return 0f;
            }

            float totalDays = CalendarManager.DaysPerMonth * CalendarManager.MonthsPerYear;
            return totalDays > 1f
                ? ((calendar.DayOfYear - 1) + calendar.TimeOfDay) / (totalDays - 1f)
                : 0f;
        }

        private static string FormatTopBarDate(CalendarManager calendar)
        {
            if (calendar == null)
            {
                return string.Empty;
            }

            string seasonName = CalendarManager.GetSeasonName(calendar.Season);
            return LocalizationManager.CurrentLanguage == LocalizationManager.English
                ? $"Year {calendar.Year}\n{seasonName}\n{calendar.Month}/{calendar.Day}"
                : $"\u7B2C{calendar.Year}\u5E74\n{seasonName}\n{calendar.Month}\u6708{calendar.Day}\u65E5";
        }

        private static string FormatTopBarWeather()
        {
            return $"{LocalizationManager.Get("ui.topbar.weather")}\n{LocalizationManager.Format("ui.topbar.weather_value", LocalizationManager.Get("ui.weather.sunny"), 22)}";
        }
    }
}
