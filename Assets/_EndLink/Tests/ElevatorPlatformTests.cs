using EndLink.World;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Tests
{
    public class ElevatorPlatformTests
    {
        [TestCase(ElevatorPlatformState.Lower, ElevatorStop.Upper)]
        [TestCase(ElevatorPlatformState.Upper, ElevatorStop.Lower)]
        public void ResolveDestination_ReturnsOppositeStop(
            ElevatorPlatformState state,
            ElevatorStop expected)
        {
            Assert.AreEqual(expected, ElevatorPlatform.ResolveDestination(state));
        }

        [TestCase(ElevatorPlatformState.Lower, true)]
        [TestCase(ElevatorPlatformState.Upper, true)]
        [TestCase(ElevatorPlatformState.MovingUp, false)]
        [TestCase(ElevatorPlatformState.MovingDown, false)]
        public void CanStartTravel_OnlyAllowsStoppedStates(
            ElevatorPlatformState state,
            bool expected)
        {
            Assert.AreEqual(expected, ElevatorPlatform.CanStartTravel(state));
        }

        [Test]
        public void EvaluateTravelPosition_UsesSmoothStepProgress()
        {
            Vector3 start = Vector3.zero;
            Vector3 destination = new Vector3(0f, 10f, 0f);

            Vector3 result = ElevatorPlatform.EvaluateTravelPosition(start, destination, 0.25f);

            Assert.AreEqual(1.5625f, result.y, 0.0001f);
        }

        [Test]
        public void ResolveStopPosition_UsesOnlyStopHeight()
        {
            Vector3 initialPlatformPosition = new Vector3(3f, 2f, 4f);
            Vector3 configuredStopPosition = new Vector3(100f, 10f, -100f);

            Vector3 result = ElevatorPlatform.ResolveStopPosition(
                initialPlatformPosition,
                configuredStopPosition);

            Assert.AreEqual(new Vector3(3f, 10f, 4f), result);
        }
    }
}
