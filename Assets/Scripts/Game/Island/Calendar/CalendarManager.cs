using System;
using UnityEngine;

namespace Game
{
    public sealed class CalendarManager
    {
        public const int HoursPerDay = 24;
        public const int MinutesPerHour = 60;
        public const int DaysPerMonth = 28;
        public const int MonthsPerSeason = 1;
        public const int SeasonsPerYear = 4;
        public const int MonthsPerYear = MonthsPerSeason * SeasonsPerYear;
        public const int DayStartHour = 6;
        public const int NightStartHour = 18;
        public const int DayNightDiskZeroHour = 12;
        public const float RealSecondsPerDay = 600f;

        public static CalendarManager Instance { get; } = new CalendarManager();

        private const int MinutesPerDay = HoursPerDay * MinutesPerHour;
        private const int MinutesPerMonth = DaysPerMonth * MinutesPerDay;
        private const int MinutesPerYear = MonthsPerYear * MinutesPerMonth;

        private float accumulatedRealSeconds;
        private int lastUpdateFrame = -1;
        private bool initialized;
        private bool loading;

        private CalendarManager()
        {
        }

        public event Action MinuteChanged;
        public event Action HourChanged;
        public event Action DayChanged;
        public event Action MonthChanged;
        public event Action SeasonChanged;
        public event Action YearChanged;
        public event Action DayNightChanged;

        public int Year { get; private set; }
        public int Month { get; private set; }
        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Minute { get; private set; }
        public bool IsPaused { get; private set; }
        public float GameTimeScale { get; private set; } = 1f;
        public bool IsInitialized => initialized;

        public Season Season => GetSeason(Month);
        public int DayOfYear => (Month - 1) * DaysPerMonth + Day;
        public float TimeOfDay => (Hour * MinutesPerHour + Minute) / (float)MinutesPerDay;
        public bool IsDay => Hour >= DayStartHour && Hour < NightStartHour;
        public bool IsNight => !IsDay;
        public float DayNightProgress => TimeOfDay;
        public float DayNightDiskRotationZ => GetDayNightDiskRotationZ(Hour, Minute);
        public float SmoothDayNightDiskRotationZ => GetDayNightDiskRotationZ(GetSmoothMinuteOfDay());
        public long AbsoluteMinutes => ToAbsoluteMinutes(Year, Month, Day, Hour, Minute);
        public float RealSecondsPerGameMinute => RealSecondsPerDay / MinutesPerDay;

        public void Initialize()
        {
            initialized = true;
            loading = true;
            IsPaused = false;
            GameTimeScale = 1f;
            accumulatedRealSeconds = 0f;
            SetDateTimeRaw(1, 1, 1, DayStartHour, 0);
            loading = false;
        }

        public void Update(float deltaTime)
        {
            if (!initialized || IsPaused || deltaTime <= 0f || GameTimeScale <= 0f)
            {
                return;
            }

            int frame = Time.frameCount;
            if (lastUpdateFrame == frame)
            {
                return;
            }

            lastUpdateFrame = frame;
            accumulatedRealSeconds += deltaTime * GameTimeScale;
            if (accumulatedRealSeconds < RealSecondsPerGameMinute)
            {
                return;
            }

            int passedMinutes = Mathf.FloorToInt(accumulatedRealSeconds / RealSecondsPerGameMinute);
            accumulatedRealSeconds -= passedMinutes * RealSecondsPerGameMinute;
            AdvanceMinutesInternal(passedMinutes, false);
        }

        public void Pause()
        {
            SetPaused(true);
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        public void SetGameTimeScale(float scale)
        {
            GameTimeScale = Mathf.Max(0f, scale);
        }

        public bool AdvanceMinutes(int minutes)
        {
            return AdvanceMinutesInternal(minutes, true);
        }

        public bool AdvanceHours(int hours)
        {
            return AdvanceMinutes(hours * MinutesPerHour);
        }

        public bool AdvanceDays(int days)
        {
            return AdvanceMinutes(days * MinutesPerDay);
        }

        public bool SetTimeOfDay(int hour, int minute)
        {
            return SetDateTime(Year, Month, Day, hour, minute);
        }

        public bool SetToDayStart()
        {
            return SetTimeOfDay(DayStartHour, 0);
        }

        public bool SetToNightStart()
        {
            return SetTimeOfDay(NightStartHour, 0);
        }

        public bool SetDateTime(int year, int month, int day, int hour, int minute)
        {
            return ApplyDateTime(year, month, day, hour, minute, true);
        }

        public bool NextDayAtTime(int hour = DayStartHour, int minute = 0)
        {
            long currentDayStart = AbsoluteMinutes - GetMinuteOfDay();
            return ApplyAbsoluteMinutes(currentDayStart + MinutesPerDay + GetMinuteOfDay(hour, minute), true);
        }

        public bool SetToNextSeason(int hour = DayStartHour, int minute = 0)
        {
            int currentSeasonIndex = Mathf.Clamp((Month - 1) / MonthsPerSeason, 0, SeasonsPerYear - 1);
            int nextSeasonIndex = currentSeasonIndex + 1;
            int year = Year;
            if (nextSeasonIndex >= SeasonsPerYear)
            {
                nextSeasonIndex = 0;
                year++;
            }

            int month = nextSeasonIndex * MonthsPerSeason + 1;
            return SetDateTime(year, month, 1, hour, minute);
        }

        public string GetDateText()
        {
            return LocalizationManager.Format("ui.calendar.date", Year, GetSeasonName(Season), Month, Day);
        }

        public string GetTimeText()
        {
            return $"{Hour:00}:{Minute:00}";
        }

        public string GetShortDateText()
        {
            return LocalizationManager.Format("ui.calendar.short_date", GetSeasonName(Season), Month, Day);
        }

        public SaveCalendarData CreateSaveData()
        {
            return new SaveCalendarData
            {
                Year = Year,
                Month = Month,
                Day = Day,
                Hour = Hour,
                Minute = Minute,
                AccumulatedRealSeconds = accumulatedRealSeconds,
            };
        }

        public void LoadSaveData(SaveCalendarData data)
        {
            loading = true;
            if (data == null)
            {
                SetDateTimeRaw(1, 1, 1, DayStartHour, 0);
                accumulatedRealSeconds = 0f;
                loading = false;
                return;
            }

            SetDateTimeRaw(data.Year, data.Month, data.Day, data.Hour, data.Minute);
            accumulatedRealSeconds = Mathf.Max(0f, data.AccumulatedRealSeconds);
            loading = false;
        }

        public static string GetSeasonName(Season season)
        {
            switch (season)
            {
                case Season.Spring:
                    return LocalizationManager.Get("ui.calendar.season.spring");
                case Season.Summer:
                    return LocalizationManager.Get("ui.calendar.season.summer");
                case Season.Autumn:
                    return LocalizationManager.Get("ui.calendar.season.autumn");
                case Season.Winter:
                    return LocalizationManager.Get("ui.calendar.season.winter");
                default:
                    return LocalizationManager.Get("ui.calendar.season.unknown");
            }
        }

        public static float GetDayNightDiskRotationZ(int hour, int minute)
        {
            float minuteOfDay = GetMinuteOfDay(hour, minute);
            return GetDayNightDiskRotationZ(minuteOfDay);
        }

        private static float GetDayNightDiskRotationZ(float minuteOfDay)
        {
            int zeroMinuteOfDay = DayNightDiskZeroHour * MinutesPerHour;
            return ((minuteOfDay - zeroMinuteOfDay) / MinutesPerDay) * 360f;
        }

        private bool AdvanceMinutesInternal(int minutes, bool forceDirty)
        {
            if (minutes == 0)
            {
                return false;
            }

            long absoluteMinutes = AbsoluteMinutes + minutes;
            return ApplyAbsoluteMinutes(absoluteMinutes, forceDirty);
        }

        private bool ApplyAbsoluteMinutes(long absoluteMinutes, bool forceDirty)
        {
            FromAbsoluteMinutes(absoluteMinutes, out int year, out int month, out int day, out int hour, out int minute);
            return ApplyDateTime(year, month, day, hour, minute, forceDirty);
        }

        private bool ApplyDateTime(int year, int month, int day, int hour, int minute, bool forceDirty)
        {
            CalendarState oldState = CaptureState();
            SetDateTimeRaw(year, month, day, hour, minute);
            CalendarState newState = CaptureState();
            if (oldState.AbsoluteMinutes == newState.AbsoluteMinutes)
            {
                return false;
            }

            NotifyChanged(oldState, newState);
            if (forceDirty || ShouldAutoDirty(oldState, newState))
            {
                MarkDirtyIfReady();
            }

            return true;
        }

        private void SetDateTimeRaw(int year, int month, int day, int hour, int minute)
        {
            Year = Mathf.Max(1, year);
            Month = Mathf.Clamp(month, 1, MonthsPerYear);
            Day = Mathf.Clamp(day, 1, DaysPerMonth);
            Hour = Mathf.Clamp(hour, 0, HoursPerDay - 1);
            Minute = Mathf.Clamp(minute, 0, MinutesPerHour - 1);
        }

        private CalendarState CaptureState()
        {
            return new CalendarState
            {
                Year = Year,
                Month = Month,
                Day = Day,
                Hour = Hour,
                Minute = Minute,
                Season = Season,
                IsDay = IsDay,
                AbsoluteMinutes = AbsoluteMinutes,
            };
        }

        private void NotifyChanged(CalendarState oldState, CalendarState newState)
        {
            MinuteChanged?.Invoke();

            if (oldState.Hour != newState.Hour)
            {
                HourChanged?.Invoke();
            }

            if (oldState.Day != newState.Day)
            {
                DayChanged?.Invoke();
            }

            if (oldState.Month != newState.Month)
            {
                MonthChanged?.Invoke();
            }

            if (oldState.Season != newState.Season)
            {
                SeasonChanged?.Invoke();
            }

            if (oldState.Year != newState.Year)
            {
                YearChanged?.Invoke();
            }

            if (oldState.IsDay != newState.IsDay)
            {
                DayNightChanged?.Invoke();
            }
        }

        private static bool ShouldAutoDirty(CalendarState oldState, CalendarState newState)
        {
            return oldState.Hour != newState.Hour ||
                   oldState.Day != newState.Day ||
                   oldState.Month != newState.Month ||
                   oldState.Year != newState.Year;
        }

        private int GetMinuteOfDay()
        {
            return GetMinuteOfDay(Hour, Minute);
        }

        private float GetSmoothMinuteOfDay()
        {
            float minuteFraction = RealSecondsPerGameMinute > 0f
                ? Mathf.Clamp01(accumulatedRealSeconds / RealSecondsPerGameMinute)
                : 0f;
            return (GetMinuteOfDay() + minuteFraction) % MinutesPerDay;
        }

        private static int GetMinuteOfDay(int hour, int minute)
        {
            int safeHour = Mathf.Clamp(hour, 0, HoursPerDay - 1);
            int safeMinute = Mathf.Clamp(minute, 0, MinutesPerHour - 1);
            return safeHour * MinutesPerHour + safeMinute;
        }

        private static Season GetSeason(int month)
        {
            int seasonIndex = Mathf.Clamp((month - 1) / MonthsPerSeason, 0, SeasonsPerYear - 1);
            return (Season)(seasonIndex + 1);
        }

        private static long ToAbsoluteMinutes(int year, int month, int day, int hour, int minute)
        {
            long years = Math.Max(0, year - 1);
            long months = Mathf.Clamp(month, 1, MonthsPerYear) - 1;
            long days = Mathf.Clamp(day, 1, DaysPerMonth) - 1;
            long hours = Mathf.Clamp(hour, 0, HoursPerDay - 1);
            long minutes = Mathf.Clamp(minute, 0, MinutesPerHour - 1);

            return years * MinutesPerYear +
                   months * MinutesPerMonth +
                   days * MinutesPerDay +
                   hours * MinutesPerHour +
                   minutes;
        }

        private static void FromAbsoluteMinutes(long absoluteMinutes, out int year, out int month, out int day, out int hour, out int minute)
        {
            absoluteMinutes = Math.Max(0, absoluteMinutes);

            year = (int)(absoluteMinutes / MinutesPerYear) + 1;
            absoluteMinutes %= MinutesPerYear;

            month = (int)(absoluteMinutes / MinutesPerMonth) + 1;
            absoluteMinutes %= MinutesPerMonth;

            day = (int)(absoluteMinutes / MinutesPerDay) + 1;
            absoluteMinutes %= MinutesPerDay;

            hour = (int)(absoluteMinutes / MinutesPerHour);
            minute = (int)(absoluteMinutes % MinutesPerHour);
        }

        private void MarkDirtyIfReady()
        {
            if (!loading)
            {
                StorageManager.Instance.MarkDirty();
            }
        }

        private struct CalendarState
        {
            public int Year;
            public int Month;
            public int Day;
            public int Hour;
            public int Minute;
            public Season Season;
            public bool IsDay;
            public long AbsoluteMinutes;
        }
    }
}
