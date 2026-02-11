using Nexus.Networking.VR;
using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public class SpatialCalibrationManagerTests
    {
        private GameObject _rootObject;
        private SpatialCalibrationManager _calibrationManager;
        private NexusVRPlayer _vrPlayer;

        [SetUp]
        public void SetUp()
        {
            _rootObject = new GameObject("CalibrationTestRoot");
            _calibrationManager = _rootObject.AddComponent<SpatialCalibrationManager>();

            var playerObj = new GameObject("TestVRPlayer");
            playerObj.transform.SetParent(_rootObject.transform, false);
            _vrPlayer = playerObj.AddComponent<NexusVRPlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
            {
                Object.DestroyImmediate(_rootObject);
            }
        }

        [Test]
        public void ApplyCalibration_ShouldSetPlayerRootTransform()
        {
            var calibration = new CalibrationData
            {
                Position = new Vector3(2f, 0, 3f),
                Rotation = Quaternion.Euler(0, 45f, 0)
            };

            _calibrationManager.ApplyCalibrationToPlayer(_vrPlayer, calibration);

            Assert.That(Vector3.Distance(_vrPlayer.transform.position, calibration.Position),
                Is.LessThan(0.001f), "Player position should match calibration offset");
            Assert.That(Quaternion.Angle(_vrPlayer.transform.rotation, calibration.Rotation),
                Is.LessThan(0.1f), "Player rotation should match calibration offset");
        }

        [Test]
        public void ApplyCalibration_ShouldCorrectChildWorldPosition()
        {
            var calibration = new CalibrationData
            {
                Position = new Vector3(2f, 0, 0),
                Rotation = Quaternion.Euler(0, 90f, 0)
            };

            _calibrationManager.ApplyCalibrationToPlayer(_vrPlayer, calibration);

            var pose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0), Quaternion.identity),
                LeftHand = new Pose(Vector3.zero, Quaternion.identity),
                RightHand = new Pose(Vector3.zero, Quaternion.identity)
            };
            _vrPlayer.ApplyPose(pose);

            Vector3 expected = calibration.Position + calibration.Rotation * new Vector3(0, 1.7f, 0);
            Assert.That(Vector3.Distance(_vrPlayer.Head.position, expected),
                Is.LessThan(0.001f), "Head world position should reflect calibration + local pose");
        }

        [Test]
        public void Initialize_ShouldSubscribeToProviderEvents()
        {
            var provider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(provider);

            CalibrationData? received = null;
            _calibrationManager.OnCalibrationComplete += data => received = data;

            provider.SetReferencePoints(Vector3.zero, new Vector3(0, 0, 2));
            provider.StartCalibration();
            provider.RecordPoint(Vector3.zero);
            provider.RecordPoint(new Vector3(0, 0, 2));

            Assert.IsNotNull(received, "Manager should relay OnCalibrationComplete from provider");
        }

        [Test]
        public void RecordReferencePoint_ShouldStoreAndSetOnProvider()
        {
            var provider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(provider);

            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 0));
            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 2));

            provider.StartCalibration();
            Assert.AreEqual(CalibrationState.InProgress, provider.State,
                "Provider should accept StartCalibration after reference points are set");
        }

        [Test]
        public void StoreCalibration_ShouldTrackPerConnectionId()
        {
            var provider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(provider);

            var calibration = new CalibrationData
            {
                Position = new Vector3(1, 0, 1),
                Rotation = Quaternion.Euler(0, 30f, 0)
            };

            _calibrationManager.StoreCalibration(42, calibration);

            Assert.IsTrue(_calibrationManager.TryGetCalibration(42, out CalibrationData stored));
            Assert.That(Vector3.Distance(stored.Position, calibration.Position), Is.LessThan(0.001f));
        }
    }
}
