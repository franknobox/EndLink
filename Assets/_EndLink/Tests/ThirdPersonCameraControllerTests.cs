using EndLink.Core;
using NUnit.Framework;

namespace EndLink.Tests
{
    public class ThirdPersonCameraControllerTests
    {
        [Test]
        public void ResolveDistanceMode_WaitsForActiveDelayBeforeZoomingOut()
        {
            CameraDistanceMode beforeDelay = ThirdPersonCameraController.ResolveDistanceMode(
                CameraDistanceMode.Idle,
                true,
                ThirdPersonCameraController.ActiveDistanceDelay - 0.01f);

            CameraDistanceMode afterDelay = ThirdPersonCameraController.ResolveDistanceMode(
                CameraDistanceMode.Idle,
                true,
                ThirdPersonCameraController.ActiveDistanceDelay);

            Assert.AreEqual(CameraDistanceMode.Idle, beforeDelay);
            Assert.AreEqual(CameraDistanceMode.Active, afterDelay);
        }

        [Test]
        public void ResolveDistanceMode_WaitsForIdleDelayBeforeZoomingIn()
        {
            CameraDistanceMode beforeDelay = ThirdPersonCameraController.ResolveDistanceMode(
                CameraDistanceMode.Active,
                false,
                ThirdPersonCameraController.IdleDistanceDelay - 0.01f);

            CameraDistanceMode afterDelay = ThirdPersonCameraController.ResolveDistanceMode(
                CameraDistanceMode.Active,
                false,
                ThirdPersonCameraController.IdleDistanceDelay);

            Assert.AreEqual(CameraDistanceMode.Active, beforeDelay);
            Assert.AreEqual(CameraDistanceMode.Idle, afterDelay);
        }

        [TestCase(PlayerStateId.Idle, false)]
        [TestCase(PlayerStateId.Move, true)]
        [TestCase(PlayerStateId.Attack, true)]
        [TestCase(PlayerStateId.Skill, true)]
        [TestCase(PlayerStateId.Dodge, true)]
        [TestCase(PlayerStateId.Hit, true)]
        [TestCase(PlayerStateId.Dead, true)]
        public void IsActiveCameraState_MapsPlayerStates(PlayerStateId stateId, bool expected)
        {
            Assert.AreEqual(expected, ThirdPersonCameraController.IsActiveCameraState(stateId));
        }
    }
}
