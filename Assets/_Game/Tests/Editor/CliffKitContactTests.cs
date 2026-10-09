using NUnit.Framework;
using UnityEngine;
using LittleCastle.World;

namespace LittleCastle.Tests
{
    public sealed class CliffKitContactTests
    {
        private static bool Ramp(Vector2 p, out float h) { h = 6.5f * p.y / 24; return true; }
        private static bool Clear(Vector2 p) => false;
        [Test] public void ExactRampAllowsInfantryContactButRefusesCartGrade()
        {
            var infantry = CliffTerrainContactValidator.ValidateRamp(Vector3.zero, 0, 7, 24, 6.5f, 3.5f, .1f, 30, Ramp, Clear);
            var cart = CliffTerrainContactValidator.ValidateRamp(Vector3.zero, 0, 7, 24, 6.5f, 3.5f, .1f, 12, Ramp, Clear);
            Assert.That(infantry.Accepted, Is.True);
            Assert.That(infantry.MaximumGradeDegrees, Is.EqualTo(15.154f).Within(.02f));
            Assert.That(cart.Accepted, Is.False); Assert.That(cart.Reason, Is.EqualTo("agent-grade-exceeded"));
        }
        [Test] public void FloatingRampIsRefused()
        {
            var r = CliffTerrainContactValidator.ValidateRamp(Vector3.up, 0, 7, 24, 6.5f, 3.5f, .1f, 30, Ramp, Clear);
            Assert.That(r.Reason, Is.EqualTo("ramp-does-not-match-ground"));
        }
        [Test] public void ShoulderReservationRefusesEntireFootprint()
        {
            var r = CliffTerrainContactValidator.ValidateRamp(Vector3.zero, 0, 7, 24, 6.5f, 3.5f, .1f, 30, Ramp, p => p.x > 3);
            Assert.That(r.Reason, Is.EqualTo("reserved-footprint"));
        }
        [Test] public void MissingAdjacentChunkFailsClosed()
        {
            bool Ground(Vector2 p, out float h) { h = 0; return p.y < 0; }
            var r = CliffTerrainContactValidator.ValidateRamp(new Vector3(-32,0,-12), 0, 7, 24, 6.5f, 3.5f, 1, 30, Ground, Clear);
            Assert.That(r.Accepted, Is.False);
        }
        [Test] public void NegativeCoordinatesAndYawPreserveRampGrade()
        {
            bool Ground(Vector2 p, out float h) { h = (p.x + 32) * 6.5f / 24; return true; }
            var r = CliffTerrainContactValidator.ValidateRamp(new Vector3(-32,0,-32), 90, 7, 24, 6.5f, 3.5f, .01f, 30, Ground, Clear);
            Assert.That(r.Accepted, Is.True);
        }
        [Test] public void InternalSpikeIsRefusedDespiteMatchingEndpoints()
        {
            bool Ground(Vector2 p, out float h) { h = 6.5f * p.y / 24 + (p.y > 11 && p.y < 13 ? 2 : 0); return true; }
            var r = CliffTerrainContactValidator.ValidateRamp(Vector3.zero, 0, 7, 24, 6.5f, 3.5f, .1f, 30, Ground, Clear);
            Assert.That(r.Accepted, Is.False);
        }
        [Test] public void CliffRefusesFlatLowGround()
        {
            bool Ground(Vector2 p, out float h) { h = 0; return true; }
            var r = CliffTerrainContactValidator.ValidateStraightCliff(Vector3.zero, 0, 16, 5, 6, .4f, Ground, Clear);
            Assert.That(r.Reason, Is.EqualTo("cliff-does-not-match-ground"));
        }
        [Test] public void CliffChecksWholeUpperShelf()
        {
            bool Ground(Vector2 p, out float h) { h = p.y < 0 ? 0 : 6; return true; }
            Assert.That(CliffTerrainContactValidator.ValidateStraightCliff(Vector3.zero, 0, 16, 5, 6, .4f, Ground, Clear).Accepted, Is.True);
            Assert.That(CliffTerrainContactValidator.ValidateStraightCliff(Vector3.zero, 0, 16, 5, 6, .4f, Ground, p => p.y > 4 && p.x < -7).Accepted, Is.False);
        }
        [Test] public void NonfiniteInputAndMissingExclusionsAreRefused()
        {
            Assert.That(CliffTerrainContactValidator.ValidateRamp(Vector3.zero,float.NaN,7,24,6.5f,3.5f,.1f,30,Ramp,Clear).Accepted,Is.False);
            Assert.That(CliffTerrainContactValidator.ValidateRamp(Vector3.zero,0,7,24,6.5f,3.5f,.1f,30,Ramp,null).Accepted,Is.False);
        }
        [Test] public void ActualOverhangingShoulderAndLastCellGradeAreChecked()
        {
            var shoulder=CliffTerrainContactValidator.ValidateRamp(Vector3.zero,0,7,24,6.5f,3.5f,.1f,30,Ramp,p=>p.x>3.7f);
            Assert.That(shoulder.Reason,Is.EqualTo("reserved-footprint"));
            bool EndSpike(Vector2 p,out float h){h=6.5f*p.y/24+(p.y>23.9f&&p.x>1.7f?.8f:0);return true;}
            var grade=CliffTerrainContactValidator.ValidateRamp(Vector3.zero,0,7,24,6.5f,3.5f,.9f,30,EndSpike,Clear);
            Assert.That(grade.Reason,Is.EqualTo("agent-grade-exceeded"));
        }
    }
}
