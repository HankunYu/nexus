using Mirror;
using Nexus.Networking.VR;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public class VRPoseTests
    {
        [Test]
        public void VRPose_DefaultValues_ShouldBeZero()
        {
            var pose = new VRPose();

            Assert.AreEqual(Vector3.zero, pose.Head.position);
            Assert.AreEqual(Vector3.zero, pose.LeftHand.position);
            Assert.AreEqual(Vector3.zero, pose.RightHand.position);
        }

        [Test]
        public void VRPose_MirrorSerialization_ShouldRoundTrip()
        {
            var original = new VRPose
            {
                Head = new Pose(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30)),
                LeftHand = new Pose(new Vector3(4, 5, 6), Quaternion.Euler(40, 50, 60)),
                RightHand = new Pose(new Vector3(7, 8, 9), Quaternion.Euler(70, 80, 90))
            };

            var writer = new NetworkWriter();
            VRPose.WritePose(writer, original);

            var reader = new NetworkReader(writer.ToArraySegment());
            VRPose deserialized = VRPose.ReadPose(reader);

            AssertPoseApprox(original.Head, deserialized.Head, "Head");
            AssertPoseApprox(original.LeftHand, deserialized.LeftHand, "LeftHand");
            AssertPoseApprox(original.RightHand, deserialized.RightHand, "RightHand");
        }

        private static void AssertPoseApprox(Pose expected, Pose actual, string label)
        {
            Assert.That(actual.position.x, Is.EqualTo(expected.position.x).Within(0.001f), $"{label} position.x");
            Assert.That(actual.position.y, Is.EqualTo(expected.position.y).Within(0.001f), $"{label} position.y");
            Assert.That(actual.position.z, Is.EqualTo(expected.position.z).Within(0.001f), $"{label} position.z");
            Assert.That(actual.rotation.x, Is.EqualTo(expected.rotation.x).Within(0.001f), $"{label} rotation.x");
            Assert.That(actual.rotation.y, Is.EqualTo(expected.rotation.y).Within(0.001f), $"{label} rotation.y");
            Assert.That(actual.rotation.z, Is.EqualTo(expected.rotation.z).Within(0.001f), $"{label} rotation.z");
            Assert.That(actual.rotation.w, Is.EqualTo(expected.rotation.w).Within(0.001f), $"{label} rotation.w");
        }
    }
}
