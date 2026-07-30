using System;
using Game.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public enum WeatherType
    {
        Sunny = 0,
        Cloudy = 1,
        Rain = 2,
        Storm = 3,
        Snow = 4,
        Fog = 5,
    }

    public sealed class WorldTopBarPanel : MonoBehaviour
    {
        [Serializable]
        private struct SeasonIconBinding
        {
            [SerializeField] private Season season;
            [SerializeField] private Sprite icon;

            public Season Season => season;
            public Sprite Icon => icon;
        }

        [Serializable]
        private struct WeatherIconBinding
        {
            [SerializeField] private WeatherType weather;
            [SerializeField] private Sprite icon;

            public WeatherType Weather => weather;
            public Sprite Icon => icon;
        }

        [Header("Calendar")]
        [SerializeField] private TMP_Text yearText;
        [SerializeField] private TMP_Text monthDayText;
        [SerializeField] private TMP_Text seasonText;
        [SerializeField] private TMP_Text timeText;

        [Header("Weather")]
        [SerializeField] private TMP_Text weatherText;
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private WeatherType initialWeather = WeatherType.Sunny;
        [SerializeField] private int initialTemperatureCelsius = 22;

        [Header("Icons")]
        [SerializeField] private Image seasonIcon;
        [SerializeField] private SeasonIconBinding[] seasonIconBindings;
        [SerializeField] private Image weatherIcon;
        [SerializeField] private WeatherIconBinding[] weatherIconBindings;

        [Header("Season Background")]
        [SerializeField] private Image seasonBackground;
        [SerializeField] private Sprite defaultSeasonBackground;
        [SerializeField] private SeasonIconBinding[] seasonBackgroundBindings;

        [Header("Day/Night")]
        [SerializeField] private RectTransform dayNightSky;
        [SerializeField] private float dayNightSkyNoonRotationZ;

        private WeatherType currentWeather;
        private int currentTemperatureCelsius;
        private bool weatherInitialized;

        public void Initialize()
        {
            EnsureWeatherInitialized();
            RefreshCalendarWidget();
            RefreshWeather();
        }

        public void RefreshCalendarWidget()
        {
            CalendarManager calendar = CalendarManager.Instance;
            SetCalendar(calendar);
            SetDayNightRotation(GetSkyDiskRotationZ(calendar));
        }

        public void RefreshCalendarMotion()
        {
            CalendarManager calendar = CalendarManager.Instance;
            SetTime(calendar != null ? calendar.GetTimeText() : string.Empty);
            SetDayNightRotation(GetSkyDiskRotationZ(calendar));
        }

        public void RefreshWeather()
        {
            EnsureWeatherInitialized();
            ApplyWeather();
        }

        public void SetWeather(WeatherType weather, int temperatureCelsius)
        {
            currentWeather = weather;
            currentTemperatureCelsius = temperatureCelsius;
            weatherInitialized = true;
            ApplyWeather();
        }

        private void SetCalendar(CalendarManager calendar)
        {
            if (calendar == null)
            {
                SetText(yearText, string.Empty);
                SetText(monthDayText, string.Empty);
                SetText(seasonText, string.Empty);
                SetText(timeText, string.Empty);
                SetIcon(seasonIcon, null);
                SetSeasonBackground(defaultSeasonBackground);
                return;
            }

            bool english = LocalizationManager.CurrentLanguage == LocalizationManager.English;
            SetText(yearText, english ? $"Year {calendar.Year}" : $"第{calendar.Year}年");
            SetText(monthDayText, english ? $"{calendar.Month}/{calendar.Day}" : $"{calendar.Month}月{calendar.Day}日");
            SetText(seasonText, CalendarManager.GetSeasonName(calendar.Season));
            SetTextColor(seasonText, GetSeasonTextColor(calendar.Season));
            SetTime(calendar.GetTimeText());
            SetIcon(seasonIcon, GetSeasonIcon(calendar.Season));
            SetSeasonBackground(GetSeasonBackground(calendar.Season));
        }

        private void SetTime(string value)
        {
            if (timeText != null)
            {
                timeText.text = value;
            }
        }

        private void ApplyWeather()
        {
            SetText(weatherText, GetWeatherName(currentWeather));
            SetText(temperatureText, currentTemperatureCelsius.ToString());
            SetIcon(weatherIcon, GetWeatherIcon(currentWeather));
        }

        private void EnsureWeatherInitialized()
        {
            if (weatherInitialized)
            {
                return;
            }

            currentWeather = initialWeather;
            currentTemperatureCelsius = initialTemperatureCelsius;
            weatherInitialized = true;
        }

        private void SetDayNightRotation(float rotationZ)
        {
            if (dayNightSky == null)
            {
                return;
            }

            dayNightSky.localRotation = Quaternion.Euler(
                0f,
                0f,
                dayNightSkyNoonRotationZ + rotationZ);
        }

        private Sprite GetSeasonIcon(Season season)
        {
            return GetSeasonSprite(seasonIconBindings, season);
        }

        private Sprite GetSeasonBackground(Season season)
        {
            Sprite background = GetSeasonSprite(seasonBackgroundBindings, season);
            return background != null ? background : defaultSeasonBackground;
        }

        private static Sprite GetSeasonSprite(SeasonIconBinding[] bindings, Season season)
        {
            if (bindings == null)
            {
                return null;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                SeasonIconBinding binding = bindings[i];
                if (binding.Season == season)
                {
                    return binding.Icon;
                }
            }

            return null;
        }

        private void SetSeasonBackground(Sprite background)
        {
            if (seasonBackground == null)
            {
                return;
            }

            seasonBackground.sprite = background;
            seasonBackground.enabled = background != null;
        }

        private Sprite GetWeatherIcon(WeatherType weather)
        {
            if (weatherIconBindings == null)
            {
                return null;
            }

            for (int i = 0; i < weatherIconBindings.Length; i++)
            {
                WeatherIconBinding binding = weatherIconBindings[i];
                if (binding.Weather == weather)
                {
                    return binding.Icon;
                }
            }

            return null;
        }

        private static string GetWeatherName(WeatherType weather)
        {
            bool english = LocalizationManager.CurrentLanguage == LocalizationManager.English;
            switch (weather)
            {
                case WeatherType.Sunny:
                    return LocalizationManager.Get("ui.weather.sunny");
                case WeatherType.Cloudy:
                    return LocalizationManager.GetOrFallback("ui.weather.cloudy", english ? "Cloudy" : "多云");
                case WeatherType.Rain:
                    return LocalizationManager.GetOrFallback("ui.weather.rain", english ? "Rain" : "下雨");
                case WeatherType.Storm:
                    return LocalizationManager.GetOrFallback("ui.weather.storm", english ? "Storm" : "雷暴");
                case WeatherType.Snow:
                    return LocalizationManager.GetOrFallback("ui.weather.snow", english ? "Snow" : "下雪");
                case WeatherType.Fog:
                    return LocalizationManager.GetOrFallback("ui.weather.fog", english ? "Fog" : "有雾");
                default:
                    return string.Empty;
            }
        }

        private static Color32 GetSeasonTextColor(Season season)
        {
            switch (season)
            {
                case Season.Spring:
                    return new Color32(0x4A, 0x8F, 0x3C, 0xFF);
                case Season.Summer:
                    return new Color32(0xE2, 0xA7, 0x2B, 0xFF);
                case Season.Autumn:
                    return new Color32(0xC8, 0x6A, 0x2A, 0xFF);
                case Season.Winter:
                    return new Color32(0x4F, 0x86, 0xB8, 0xFF);
                default:
                    return new Color32(0x2B, 0x26, 0x1D, 0xFF);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetTextColor(TMP_Text target, Color color)
        {
            if (target != null)
            {
                target.color = color;
            }
        }

        private static void SetIcon(Image target, Sprite icon)
        {
            if (target != null)
            {
                target.sprite = icon;
                target.enabled = icon != null;
            }
        }

        private static float GetSkyDiskRotationZ(CalendarManager calendar)
        {
            return calendar != null ? calendar.SmoothDayNightDiskRotationZ : 0f;
        }
    }
}
