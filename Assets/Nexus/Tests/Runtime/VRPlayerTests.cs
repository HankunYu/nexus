using System.Collections;
using Nexus.Networking.VR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    public class VRPlayerTests
    {
        private GameObject _playerObject;
        private NexusVRPlayer _vrPlayer;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("TestVRPlayer");
            _vrPlayer = _playerObject.AddComponent<NexusVRPlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }
        }

        [Test]
        public void Awake_ShouldCreateChildTransforms()
        {
            Assert.IsNotNull(_vrPlayer.Head, "Head transform should be created");
            Assert.IsNotNull(_vrPlayer.LeftHand, "LeftHand transform should be created");
            Assert.IsNotNull(_vrPlayer.RightHand, "RightHand transform should be created");
        }

        [Test]
        public void ChildTransforms_ShouldBeChildrenOfPlayer()
        {
            Assert.AreEqual(_playerObject.transform, _vrPlayer.Head.parent);
            Assert.AreEqual(_playerObject.transform, _vrPlayer.LeftHand.parent);
            Assert.AreEqual(_playerObject.transform, _vrPlayer.RightHand.parent);
        }

        [Test]
        public void ApplyPose_ShouldSetTransformValues()
        {
            var pose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0), Quaternion.Euler(10, 0, 0)),
                LeftHand = new Pose(new Vector3(-0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, 45)),
                RightHand = new Pose(new Vector3(0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, -45))
            };

            _vrPlayer.ApplyPose(pose);

            AssertVector3Approx(pose.Head.position, _vrPlayer.Head.localPosition, "Head position");
            AssertQuaternionApprox(pose.Head.rotation, _vrPlayer.Head.localRotation, "Head rotation");
            AssertVector3Approx(pose.LeftHand.position, _vrPlayer.LeftHand.localPosition, "LeftHand position");
            AssertQuaternionApprox(pose.LeftHand.rotation, _vrPlayer.LeftHand.localRotation, "LeftHand rotation");
            AssertVector3Approx(pose.RightHand.position, _vrPlayer.RightHand.localPosition, "RightHand position");
            AssertQuaternionApprox(pose.RightHand.rotation, _vrPlayer.RightHand.localRotation, "RightHand rotation");
        }

        [Test]
        public void TrackingProvider_WhenSet_ShouldSamplePose()
        {
            var mockProvider = new MockTrackingProvider();
            var expectedPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.8f, 0), Quaternion.identity),
                LeftHand = new Pose(new Vector3(-0.5f, 1f, 0.3f), Quaternion.identity),
                RightHand = new Pose(new Vector3(0.5f, 1f, 0.3f), Quaternion.identity)
            };
            mockProvider.NextPose = expectedPose;

            _vrPlayer.TrackingProvider = mockProvider;
            _vrPlayer.SampleLocalPose();

            AssertVector3Approx(expectedPose.Head.position, _vrPlayer.Head.localPosition, "Head");
            AssertVector3Approx(expectedPose.LeftHand.position, _vrPlayer.LeftHand.localPosition, "LeftHand");
            AssertVector3Approx(expectedPose.RightHand.position, _vrPlayer.RightHand.localPosition, "RightHand");
        }

        // === Task 4: Interpolation tests ===

        [UnityTest]
        public IEnumerator SetTargetPose_ShouldInterpolateOverTime()
        {
            var targetPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 2f, 0), Quaternion.identity),
                LeftHand = new Pose(new Vector3(-1f, 1f, 0), Quaternion.identity),
                RightHand = new Pose(new Vector3(1f, 1f, 0), Quaternion.identity)
            };

            _vrPlayer.SetTargetPose(targetPose, interpolationSpeed: 10f);

            for (int i = 0; i < 10; i++)
            {
                _vrPlayer.InterpolateToTarget();
                yield return null;
            }

            float headDist = Vector3.Distance(_vrPlayer.Head.localPosition, targetPose.Head.position);
            Assert.Less(headDist, 1.9f, "Head should have moved toward target");
        }

        [Test]
        public void SetTargetPose_WithHighSpeed_ShouldReachTargetQuickly()
        {
            var targetPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.5f, 0), Quaternion.identity),
                LeftHand = new Pose(Vector3.zero, Quaternion.identity),
                RightHand = new Pose(Vector3.zero, Quaternion.identity)
            };

            _vrPlayer.SetTargetPose(targetPose, interpolationSpeed: 1000f);
            _vrPlayer.InterpolateToTarget(deltaTime: 1f);

            float headDist = Vector3.Distance(_vrPlayer.Head.localPosition, targetPose.Head.position);
            Assert.Less(headDist, 0.01f, "Head should be at target with high speed");
        }

        private static void AssertVector3Approx(Vector3 expected, Vector3 actual, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f), $"{label}.x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f), $"{label}.y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f), $"{label}.z");
        }

        private static void AssertQuaternionApprox(Quaternion expected, Quaternion actual, string label)
        {
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThan(0.1f), $"{label} angle diff");
        }
    }

    /// <summary>
    /// Mock IVRTrackingProvider for testing.
    /// </summary>
    public class MockTrackingProvider : IVRTrackingProvider
    {
        public VRPose NextPose { get; set; }

        public VRPose GetCurrentPose()
        {
            return NextPose;
        }
    }
}
