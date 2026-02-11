using System.Collections;
using Nexus.Networking.Core;
using Nexus.Networking.VR;
using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// End-to-end integration tests for spatial calibration.
    /// Verifies: calibration → player spawn → offset applied → transforms correct.
    /// </summary>
    public class CalibrationIntegrationTests : NexusTestBase
    {
        private NexusVRPlayerManager _vrManager;
        private SpatialCalibrationManager _calibrationManager;
        private ManualCalibrationProvider _manualProvider;
        private MockTrackingProvider _mockTracking;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BaseSetUp();

            _vrManager = _rootObject.AddComponent<NexusVRPlayerManager>();
            _vrManager.Initialize(_roomManager, _config);

            _mockTracking = new MockTrackingProvider();
            _mockTracking.NextPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0), Quaternion.identity),
                LeftHand = new Pose(new Vector3(-0.3f, 1f, 0.3f), Quaternion.identity),
                RightHand = new Pose(new Vector3(0.3f, 1f, 0.3f), Quaternion.identity)
            };
            _vrManager.TrackingProvider = _mockTracking;

            _calibrationManager = _rootObject.AddComponent<SpatialCalibrationManager>();
            _manualProvider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(_manualProvider, _vrManager);
        }

        [UnityTest]
        public IEnumerator CalibrateBeforeRoom_ShouldApplyToSpawnedPlayer()
        {
            // Host records reference points
            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 0));
            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 2));

            // Simulate client calibration with a 2m X offset, no rotation
            _manualProvider.StartCalibration();
            _manualProvider.RecordPoint(new Vector3(2, 1, 0));
            _manualProvider.RecordPoint(new Vector3(2, 1, 2));

            Assert.AreEqual(CalibrationState.Calibrated, _manualProvider.State);

            // Create room — should spawn player with calibration applied
            _session.CreateRoom("Calibration Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            yield return null;
            yield return null;

            // The calibration offset should shift the player -2m on X
            NexusVRPlayer player = _vrManager.LocalPlayer;
            Assert.That(Vector3.Distance(player.transform.position, new Vector3(-2, 0, 0)),
                Is.LessThan(0.01f),
                "Player root should have calibration position offset");
        }

        [UnityTest]
        public IEnumerator CalibratedPlayer_PoseShouldBeInWorldSpace()
        {
            // Simple calibration: 1m offset on Z
            _calibrationManager.RecordReferencePoint(new Vector3(0, 0, 0));
            _calibrationManager.RecordReferencePoint(new Vector3(1, 0, 0));
            _manualProvider.StartCalibration();
            _manualProvider.RecordPoint(new Vector3(0, 0, 1));
            _manualProvider.RecordPoint(new Vector3(1, 0, 1));

            _session.CreateRoom("World Space Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            yield return null;
            yield return null;

            NexusVRPlayer player = _vrManager.LocalPlayer;

            // Head local position is (0, 1.7, 0) from MockTrackingProvider
            // Calibration offset is (0, 0, -1) on Z
            // World position should be (0, 1.7, -1)
            Vector3 expectedHead = player.transform.position +
                player.transform.rotation * new Vector3(0, 1.7f, 0);
            Assert.That(Vector3.Distance(player.Head.position, expectedHead),
                Is.LessThan(0.01f),
                "Head world position should be calibration offset + local pose");
        }
    }
}
