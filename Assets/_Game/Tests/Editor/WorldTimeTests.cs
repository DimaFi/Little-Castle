using LittleCastle.World;
using NUnit.Framework;

namespace LittleCastle.Tests
{
    public sealed class WorldTimeTests
    {
        [Test]
        public void DayWindow_TakesSixRealMinutes()
        {
            var clock = new WorldClock(6.0, 6.0, 20.0, 360.0, 90.0);
            WorldTimeAdvance advance = clock.Advance(360.0);

            Assert.That(clock.CurrentHour, Is.EqualTo(20.0).Within(0.000001));
            Assert.That(advance.GameHoursAdvanced, Is.EqualTo(14.0).Within(0.000001));
            Assert.That(clock.IsNight, Is.True);
        }

        [Test]
        public void NightWindow_TakesNinetyRealSeconds()
        {
            var clock = new WorldClock(20.0, 6.0, 20.0, 360.0, 90.0);
            WorldTimeAdvance advance = clock.Advance(90.0);

            Assert.That(clock.DayIndex, Is.EqualTo(2));
            Assert.That(clock.CurrentHour, Is.EqualTo(6.0).Within(0.000001));
            Assert.That(advance.GameHoursAdvanced, Is.EqualTo(10.0).Within(0.000001));
            Assert.That(clock.IsDay, Is.True);
        }

        [Test]
        public void NightRate_StaysContinuousAcrossMidnight()
        {
            var clock = new WorldClock(20.0, 6.0, 20.0, 360.0, 90.0);
            clock.Advance(45.0);

            Assert.That(clock.DayIndex, Is.EqualTo(2));
            Assert.That(clock.CurrentHour, Is.EqualTo(1.0).Within(0.000001));
            Assert.That(clock.IsNight, Is.True);
        }

        [Test]
        public void State_CanBeRestored()
        {
            var original = new WorldClock(8.0, 6.0, 20.0, 360.0, 90.0);
            original.Advance(137.5);

            var restored = new WorldClock(6.0, 6.0, 20.0, 360.0, 90.0);
            restored.Restore(original.State);

            Assert.That(restored.DayIndex, Is.EqualTo(original.DayIndex));
            Assert.That(restored.MinuteOfDay, Is.EqualTo(original.MinuteOfDay).Within(0.000001));
        }
    }
}
