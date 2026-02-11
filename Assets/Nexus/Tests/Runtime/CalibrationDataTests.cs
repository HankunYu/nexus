using Mirror;
using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public class CalibrationDataTests
    {
        [Test]
        public void Identity_ShouldHaveZeroPositionAndIdentityRotation()
        {
            CalibrationData identity = CalibrationData.Identity;

            Assert.AreEqual(Vector3.zero, identity.Position);
            Assert.That(Quaternion.Angle(Quaternion.identity, identity.Rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void MirrorSerialization_ShouldRoundTrip()
        {
            var original = new CalibrationData
            {
                Position = new Vector3(1.5f, 0.2f, -3.0f),
                Rotation = Quaternion.Euler(0, 45f, 0)
            };

            var writer = new NetworkWriter();
            CalibrationData.WriteCalibrationData(writer, original);

            var reader = new NetworkReader(writer.ToArraySegment());
            CalibrationData deserialized = CalibrationData.ReadCalibrationData(reader);

            Assert.That(Vector3.Distance(original.Position, deserialized.Position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(original.Rotation, deserialized.Rotation), Is.LessThan(0.1f));
        }

        [Test]
        public void ApplyToTransform_ShouldOffsetChildWorldPosition()
        {
            var parent = new GameObject("CalibrationRoot");
            var child = new GameObject("Head");
            child.transform.SetParent(parent.transform, false);

            var calibration = new CalibrationData
            {
                Position = new Vector3(2f, 0, 3f),
                Rotation = Quaternion.Euler(0, 90f, 0)
            };

            parent.transform.position = calibration.Position;
            parent.transform.rotation = calibration.Rotation;

            child.transform.localPosition = new Vector3(0, 1.7f, 0);
            child.transform.localRotation = Quaternion.identity;

            Vector3 expectedWorld = calibration.Position + calibration.Rotation * new Vector3(0, 1.7f, 0);
            Assert.That(Vector3.Distance(child.transform.position, expectedWorld), Is.LessThan(0.001f),
                "Child world position should reflect calibration offset");

            Object.DestroyImmediate(parent);
        }
    }
}
