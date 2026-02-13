using System.Collections;
using Nexus.Networking.Core;
using Nexus.Networking.VR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// End-to-end integration tests for VR player sync.
    /// Verifies the full pipeline: room → spawn → tracking → transform.
    /// </summary>
    public class VRSyncIntegrationTests : NexusTestBase
    {
        private NexusVRPlayerManager _vrManager;
        private MockTrackingProvider _mockProvider;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BaseSetUp();

            _vrManager = _rootObject.AddComponent<NexusVRPlayerManager>();
            var template = new GameObject("TestVRPlayerTemplate");
            template.SetActive(false);
            template.AddComponent<NexusVRPlayer>();
            _vrManager.Initialize(_roomManager, _config, template);

            _mockProvider = new MockTrackingProvider();
            _vrManager.TrackingProvider = _mockProvider;
        }

        [UnityTest]
        public IEnumerator FullPipeline_ShouldSyncLocalPoseToTransforms()
        {
            var expectedPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0.1f), Quaternion.Euler(5, 0, 0)),
                LeftHand = new Pose(new Vector3(-0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, 30)),
                RightHand = new Pose(new Vector3(0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, -30))
            };
            _mockProvider.NextPose = expectedPose;

            _session.CreateRoom("Integration Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            // Wait a frame for Update to sample pose
            yield return null;
            yield return null;

            NexusVRPlayer player = _vrManager.LocalPlayer;

            Assert.That(
                Vector3.Distance(player.Head.localPosition, expectedPose.Head.position),
                Is.LessThan(0.01f),
                "Head position should match tracking data");
            Assert.That(
                Vector3.Distance(player.LeftHand.localPosition, expectedPose.LeftHand.position),
                Is.LessThan(0.01f),
                "LeftHand position should match tracking data");
            Assert.That(
                Vector3.Distance(player.RightHand.localPosition, expectedPose.RightHand.position),
                Is.LessThan(0.01f),
                "RightHand position should match tracking data");
        }

        [UnityTest]
        public IEnumerator PoseUpdate_ShouldReflectNewTrackingData()
        {
            _mockProvider.NextPose = new VRPose
            {
                Head = new Pose(Vector3.zero, Quaternion.identity),
                LeftHand = new Pose(Vector3.zero, Quaternion.identity),
                RightHand = new Pose(Vector3.zero, Quaternion.identity)
            };

            _session.CreateRoom("Pose Update Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            yield return null;

            // Change tracking data
            var newPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 2f, 0), Quaternion.identity),
                LeftHand = new Pose(new Vector3(-1f, 1.5f, 0), Quaternion.identity),
                RightHand = new Pose(new Vector3(1f, 1.5f, 0), Quaternion.identity)
            };
            _mockProvider.NextPose = newPose;

            // Wait for Update to pick up new pose
            yield return null;
            yield return null;

            Assert.That(
                Vector3.Distance(_vrManager.LocalPlayer.Head.localPosition, newPose.Head.position),
                Is.LessThan(0.01f),
                "Head should reflect updated tracking data");
        }
    }
}
