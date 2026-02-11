using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public class ManualCalibrationTests
    {
        [Test]
        public void SamePoints_ShouldReturnIdentity()
        {
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, refA, refB);

            Assert.That(Vector3.Distance(result.Position, Vector3.zero), Is.LessThan(0.001f),
                "Position offset should be zero when points match");
            Assert.That(Quaternion.Angle(result.Rotation, Quaternion.identity), Is.LessThan(0.1f),
                "Rotation offset should be identity when points match");
        }

        [Test]
        public void TranslatedPoints_ShouldComputePositionOffset()
        {
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);
            var localA = new Vector3(3, 0, 0);
            var localB = new Vector3(3, 0, 2);

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, localA, localB);

            Assert.That(Vector3.Distance(result.Position, new Vector3(-3, 0, 0)), Is.LessThan(0.001f),
                "Position offset should compensate for translation");
            Assert.That(Quaternion.Angle(result.Rotation, Quaternion.identity), Is.LessThan(0.1f),
                "Rotation should be identity (no rotation difference)");
        }

        [Test]
        public void Rotated90Degrees_ShouldComputeYawOffset()
        {
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);
            var localA = new Vector3(0, 0, 0);
            var localB = new Vector3(2, 0, 0);

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, localA, localB);

            float angle = Quaternion.Angle(result.Rotation, Quaternion.Euler(0, -90, 0));
            Assert.That(angle, Is.LessThan(0.5f),
                $"Rotation should be -90° around Y, got angle diff: {angle}");
        }

        [Test]
        public void RotatedPoints_ShouldOnlyCalibrateYaw()
        {
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);
            var localA = new Vector3(0, 0.5f, 0);
            var localB = new Vector3(0, 0.5f, 2);

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, localA, localB);

            Vector3 euler = result.Rotation.eulerAngles;
            float pitch = euler.x > 180 ? euler.x - 360 : euler.x;
            float roll = euler.z > 180 ? euler.z - 360 : euler.z;
            Assert.That(Mathf.Abs(pitch), Is.LessThan(0.1f), "Pitch should be zero");
            Assert.That(Mathf.Abs(roll), Is.LessThan(0.1f), "Roll should be zero");
        }

        [Test]
        public void RecordPoint_TwoPoints_ShouldFireOnCalibrationComplete()
        {
            var provider = new ManualCalibrationProvider();
            provider.SetReferencePoints(new Vector3(0, 0, 0), new Vector3(0, 0, 2));

            CalibrationData? receivedData = null;
            provider.OnCalibrationComplete += data => receivedData = data;

            provider.StartCalibration();
            provider.RecordPoint(new Vector3(0, 0, 0));
            Assert.IsNull(receivedData, "Should not fire after first point");

            provider.RecordPoint(new Vector3(0, 0, 2));
            Assert.IsNotNull(receivedData, "Should fire after second point");
            Assert.AreEqual(CalibrationState.Calibrated, provider.State);
        }

        [Test]
        public void StartCalibration_WithoutReferencePoints_ShouldFail()
        {
            var provider = new ManualCalibrationProvider();

            string failMessage = null;
            provider.OnCalibrationFailed += msg => failMessage = msg;

            provider.StartCalibration();

            Assert.AreEqual(CalibrationState.Failed, provider.State);
            Assert.IsNotNull(failMessage);
        }

        [Test]
        public void CancelCalibration_ShouldResetState()
        {
            var provider = new ManualCalibrationProvider();
            provider.SetReferencePoints(Vector3.zero, Vector3.forward);
            provider.StartCalibration();
            Assert.AreEqual(CalibrationState.InProgress, provider.State);

            provider.CancelCalibration();
            Assert.AreEqual(CalibrationState.None, provider.State);
        }
    }
}
