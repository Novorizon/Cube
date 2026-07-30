using System;

namespace Game
{
    /// <summary>
    /// Business-neutral result returned by feature prerequisite checks.
    /// The business layer owns the rule; UI decides how to present a failure.
    /// </summary>
    public readonly struct RequirementResult
    {
        private static readonly object[] EmptyArgs = Array.Empty<object>();

        private RequirementResult(
            bool succeeded,
            string code,
            string localizationKey,
            string fallbackZhCn,
            string fallbackEn,
            object[] formatArgs)
        {
            Succeeded = succeeded;
            Code = code ?? string.Empty;
            LocalizationKey = localizationKey ?? string.Empty;
            FallbackZhCn = fallbackZhCn ?? string.Empty;
            FallbackEn = fallbackEn ?? string.Empty;
            FormatArgs = formatArgs ?? EmptyArgs;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string LocalizationKey { get; }
        public string FallbackZhCn { get; }
        public string FallbackEn { get; }
        public object[] FormatArgs { get; }

        public string Message
        {
            get
            {
                if (Succeeded)
                {
                    return string.Empty;
                }

                string fallback = LocalizationManager.CurrentLanguage == LocalizationManager.English
                    ? FallbackEn
                    : FallbackZhCn;
                if (string.IsNullOrWhiteSpace(LocalizationKey))
                {
                    return fallback;
                }

                return LocalizationManager.FormatOrFallback(
                    LocalizationKey,
                    fallback,
                    FormatArgs ?? EmptyArgs);
            }
        }

        public static RequirementResult Success()
        {
            return new RequirementResult(true, string.Empty, string.Empty, string.Empty, string.Empty, EmptyArgs);
        }

        public static RequirementResult Failure(
            string code,
            string localizationKey,
            string fallbackZhCn,
            string fallbackEn,
            params object[] formatArgs)
        {
            return new RequirementResult(
                false,
                code,
                localizationKey,
                fallbackZhCn,
                fallbackEn,
                formatArgs);
        }
    }
}
