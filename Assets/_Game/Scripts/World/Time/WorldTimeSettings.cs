using UnityEngine;

namespace LittleCastle.World
{
    [CreateAssetMenu(fileName = "WorldTimeSettings", menuName = "Little Castle/World/World Time Settings")]
    public sealed class WorldTimeSettings : ScriptableObject
    {
        [SerializeField, Range(0f, 24f)] private float startHour = 8f;
        [SerializeField, Range(0f, 24f)] private float dayStartHour = 6f;
        [SerializeField, Range(0f, 24f)] private float nightStartHour = 20f;
        [SerializeField, Min(1f)] private float dayRealSeconds = 360f;
        [SerializeField, Min(1f)] private float nightRealSeconds = 90f;

        public float StartHour => startHour;
        public float DayStartHour => dayStartHour;
        public float NightStartHour => nightStartHour;
        public float DayRealSeconds => dayRealSeconds;
        public float NightRealSeconds => nightRealSeconds;

        public bool TryValidate(out string error)
        {
            if (!IsFinite(startHour) || startHour < 0f || startHour > 24f)
            {
                error = "Start hour must be between 0 and 24.";
                return false;
            }

            if (!IsFinite(dayStartHour) || !IsFinite(nightStartHour) ||
                dayStartHour < 0f || dayStartHour >= 24f ||
                nightStartHour <= dayStartHour || nightStartHour >= 24f)
            {
                error = "Expected 0 <= dayStartHour < nightStartHour < 24.";
                return false;
            }

            if (!IsFinite(dayRealSeconds) || !IsFinite(nightRealSeconds) ||
                dayRealSeconds <= 0f || nightRealSeconds <= 0f)
            {
                error = "Real-time durations must be positive.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
