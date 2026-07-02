using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public static class LocalizationManager
    {
        private const string LanguageKey = "World.Language";

        public const string English = "en";
        public const string Chinese = "zh-cn";

        private static readonly Dictionary<string, string> FallbackZhCn = new Dictionary<string, string>
        {
            { "ui.common.free", "免费" },
            { "ui.common.none", "无" },
            { "ui.gm.status.ready", "就绪" },
        };

        public static event Action LanguageChanged;

        public static string CurrentLanguage => NormalizeLanguage(PlayerPrefs.GetString(LanguageKey, Chinese));

        public static void SetLanguage(string language)
        {
            string normalized = NormalizeLanguage(language);
            if (CurrentLanguage == normalized)
            {
                return;
            }

            PlayerPrefs.SetString(LanguageKey, normalized);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }

        public static string GetLanguageDisplayName(string language)
        {
            return NormalizeLanguage(language) == English
                ? Get("ui.language.english")
                : Get("ui.language.chinese");
        }

        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            IReadOnlyDictionary<string, LocalizationConfig> localization = DataManager.Instance.Localization;
            if (localization != null &&
                localization.TryGetValue(key, out LocalizationConfig config) &&
                config != null)
            {
                string value = CurrentLanguage == English ? config.En : config.ZhCn;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                string fallbackValue = CurrentLanguage == English ? config.ZhCn : config.En;
                if (!string.IsNullOrEmpty(fallbackValue))
                {
                    return fallbackValue;
                }
            }

            return FallbackZhCn.TryGetValue(key, out string fallback) ? fallback : key;
        }

        public static string GetOrFallback(string key, string fallback)
        {
            string value = Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        public static string Format(string key, params object[] args)
        {
            string format = Get(key);
            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        public static string FormatOrFallback(string key, string fallback, params object[] args)
        {
            string format = GetOrFallback(key, fallback);
            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        private static string NormalizeLanguage(string language)
        {
            return string.Equals(language, English, StringComparison.OrdinalIgnoreCase) ? English : Chinese;
        }
    }
}
