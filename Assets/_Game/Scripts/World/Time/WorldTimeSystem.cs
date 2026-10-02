using System;
using UnityEngine;

namespace LittleCastle.World
{
    [DefaultExecutionOrder(-100)]
    public sealed class WorldTimeSystem : MonoBehaviour
    {
        [SerializeField] private WorldDefinition worldDefinition;
        [SerializeField] private WorldTimeSettings timeSettingsOverride;
        [SerializeField] private bool runAutomatically = true;
        [SerializeField, Min(0f)] private float simulationSpeedMultiplier = 1f;

        private WorldClock clock;
        private WorldTimeSettings resolvedSettings;

        public event Action<int> DayChanged;
        public event Action<int, int> HourChanged;
        public event Action<WorldTimeState, double> TimeAdvanced;

        public WorldTimeSettings Settings => resolvedSettings;
        public WorldClock Clock => clock;
        public WorldTimeState State => clock != null ? clock.State : default;
        public int CurrentDay => clock != null ? clock.DayIndex : 1;
        public float CurrentHour => clock != null ? (float)clock.CurrentHour : 0f;
        public int CurrentHourInt => clock != null ? clock.CurrentHourInt : 0;
        public int CurrentMinute => clock != null ? clock.CurrentMinute : 0;
        public float NormalizedTime => clock != null ? (float)clock.NormalizedTime : 0f;
        public bool IsDay => clock != null && clock.IsDay;
        public bool IsNight => clock != null && clock.IsNight;
        public double GameHoursAdvancedLastTick { get; private set; }

        public bool RunAutomatically
        {
            get => runAutomatically;
            set => runAutomatically = value;
        }

        public float SimulationSpeedMultiplier
        {
            get => simulationSpeedMultiplier;
            set => simulationSpeedMultiplier = Mathf.Max(0f, value);
        }

        private void Awake() => TryInitialize();

        private void Update()
        {
            if (!runAutomatically || clock == null)
            {
                GameHoursAdvancedLastTick = 0.0;
                return;
            }

            AdvanceSimulation(Time.deltaTime * simulationSpeedMultiplier);
        }

        public bool TryInitialize()
        {
            if (clock != null)
                return true;

            resolvedSettings = timeSettingsOverride != null
                ? timeSettingsOverride
                : worldDefinition != null
                    ? worldDefinition.TimeSettings
                    : null;

            if (resolvedSettings == null)
            {
                Debug.LogError(
                    "WorldTimeSystem requires WorldTimeSettings directly or through WorldDefinition.",
                    this);
                enabled = false;
                return false;
            }

            if (!resolvedSettings.TryValidate(out string error))
            {
                Debug.LogError("Invalid WorldTimeSettings: " + error, resolvedSettings);
                enabled = false;
                return false;
            }

            clock = new WorldClock(resolvedSettings);
            enabled = true;
            return true;
        }

        public WorldTimeAdvance AdvanceSimulation(double realSeconds)
        {
            if (clock == null && !TryInitialize())
                return new WorldTimeAdvance(0.0, 0.0);

            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0.0)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));

            double before = clock.AbsoluteGameMinutes;
            WorldTimeAdvance advance = clock.Advance(realSeconds);
            double after = clock.AbsoluteGameMinutes;

            GameHoursAdvancedLastTick = advance.GameHoursAdvanced;
            DispatchBoundaryEvents(before, after);

            if (advance.GameMinutesAdvanced > 0.0)
                TimeAdvanced?.Invoke(clock.State, advance.GameHoursAdvanced);

            return advance;
        }

        public WorldTimeState CaptureState()
        {
            if (clock == null && !TryInitialize())
                return default;

            return clock.State;
        }

        public void RestoreState(WorldTimeState state)
        {
            if (clock == null && !TryInitialize())
                return;

            clock.Restore(state);
            GameHoursAdvancedLastTick = 0.0;
        }

        private void DispatchBoundaryEvents(double before, double after)
        {
            int firstHour = (int)Math.Floor(before / 60.0) + 1;
            int lastHour = (int)Math.Floor(after / 60.0);

            for (int absoluteHour = firstHour; absoluteHour <= lastHour; absoluteHour++)
            {
                int zeroBasedDay = absoluteHour / 24;
                int hour = absoluteHour % 24;
                int eventDay = zeroBasedDay + 1;

                if (hour == 0)
                    DayChanged?.Invoke(eventDay);

                HourChanged?.Invoke(eventDay, hour);
            }
        }

        private void OnValidate() =>
            simulationSpeedMultiplier = Mathf.Max(0f, simulationSpeedMultiplier);
    }
}
