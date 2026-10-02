using System;

namespace LittleCastle.World
{
    [Serializable]
    public struct WorldTimeState
    {
        public int dayIndex;
        public double minuteOfDay;

        public WorldTimeState(int dayIndex, double minuteOfDay)
        {
            this.dayIndex = dayIndex;
            this.minuteOfDay = minuteOfDay;
        }
    }

    public readonly struct WorldTimeAdvance
    {
        public double RealSecondsConsumed { get; }
        public double GameMinutesAdvanced { get; }
        public double GameHoursAdvanced => GameMinutesAdvanced / 60.0;

        public WorldTimeAdvance(double realSecondsConsumed, double gameMinutesAdvanced)
        {
            RealSecondsConsumed = realSecondsConsumed;
            GameMinutesAdvanced = gameMinutesAdvanced;
        }
    }

    public sealed class WorldClock
    {
        public const double MinutesPerDay = 1440.0;
        private const double Epsilon = 0.000000001;

        private readonly double dayStartMinute;
        private readonly double nightStartMinute;
        private readonly double dayRate;
        private readonly double nightRate;

        private int dayIndex = 1;
        private double minuteOfDay;

        public int DayIndex => dayIndex;
        public double MinuteOfDay => minuteOfDay;
        public double CurrentHour => minuteOfDay / 60.0;
        public int CurrentHourInt => (int)Math.Floor(CurrentHour);
        public int CurrentMinute => (int)Math.Floor(minuteOfDay) % 60;
        public bool IsDay => minuteOfDay >= dayStartMinute && minuteOfDay < nightStartMinute;
        public bool IsNight => !IsDay;
        public double NormalizedTime => minuteOfDay / MinutesPerDay;
        public double AbsoluteGameMinutes => (dayIndex - 1) * MinutesPerDay + minuteOfDay;
        public WorldTimeState State => new WorldTimeState(dayIndex, minuteOfDay);

        public WorldClock(WorldTimeSettings settings)
            : this(
                settings != null ? settings.StartHour : throw new ArgumentNullException(nameof(settings)),
                settings.DayStartHour,
                settings.NightStartHour,
                settings.DayRealSeconds,
                settings.NightRealSeconds)
        {
            if (!settings.TryValidate(out string error))
                throw new ArgumentException(error, nameof(settings));
        }

        public WorldClock(
            double startHour,
            double dayStartHour,
            double nightStartHour,
            double dayRealSeconds,
            double nightRealSeconds)
        {
            if (!IsFinite(startHour) || startHour < 0.0 || startHour > 24.0)
                throw new ArgumentOutOfRangeException(nameof(startHour));

            if (!IsFinite(dayStartHour) || !IsFinite(nightStartHour) ||
                dayStartHour < 0.0 || dayStartHour >= 24.0 ||
                nightStartHour <= dayStartHour || nightStartHour >= 24.0)
                throw new ArgumentException("Expected 0 <= dayStartHour < nightStartHour < 24.");

            if (!IsFinite(dayRealSeconds) || !IsFinite(nightRealSeconds) ||
                dayRealSeconds <= 0.0 || nightRealSeconds <= 0.0)
                throw new ArgumentException("Real-time durations must be positive.");

            dayStartMinute = dayStartHour * 60.0;
            nightStartMinute = nightStartHour * 60.0;

            dayRate = (nightStartMinute - dayStartMinute) / dayRealSeconds;
            nightRate = (MinutesPerDay - nightStartMinute + dayStartMinute) / nightRealSeconds;

            minuteOfDay = NormalizeMinute(startHour * 60.0);
        }

        public WorldTimeAdvance Advance(double realSeconds)
        {
            if (!IsFinite(realSeconds) || realSeconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));

            double remaining = realSeconds;
            double advanced = 0.0;

            while (remaining > Epsilon)
            {
                bool day = IsDay;
                double rate = day ? dayRate : nightRate;
                double untilBoundary;

                if (day)
                    untilBoundary = nightStartMinute - minuteOfDay;
                else if (minuteOfDay >= nightStartMinute)
                    untilBoundary = MinutesPerDay - minuteOfDay;
                else
                    untilBoundary = dayStartMinute - minuteOfDay;

                double secondsToBoundary = untilBoundary / rate;

                if (remaining < secondsToBoundary - Epsilon)
                {
                    double step = remaining * rate;
                    minuteOfDay += step;
                    advanced += step;
                    remaining = 0.0;
                    break;
                }

                advanced += untilBoundary;
                remaining -= secondsToBoundary;

                if (day)
                    minuteOfDay = nightStartMinute;
                else if (minuteOfDay >= nightStartMinute)
                {
                    minuteOfDay = 0.0;
                    dayIndex++;
                }
                else
                    minuteOfDay = dayStartMinute;
            }

            return new WorldTimeAdvance(realSeconds, advanced);
        }

        public void Restore(WorldTimeState state)
        {
            if (!IsFinite(state.minuteOfDay))
                throw new ArgumentException("minuteOfDay must be finite.", nameof(state));

            dayIndex = Math.Max(1, state.dayIndex);
            minuteOfDay = NormalizeMinute(state.minuteOfDay);
        }

        private static double NormalizeMinute(double value)
        {
            double result = value % MinutesPerDay;
            return result < 0.0 ? result + MinutesPerDay : result;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
