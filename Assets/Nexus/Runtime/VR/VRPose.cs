using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR
{
    public struct VRPose
    {
        public Pose Head;
        public Pose LeftHand;
        public Pose RightHand;

        public static void WritePose(NetworkWriter writer, VRPose pose)
        {
            writer.WriteVector3(pose.Head.position);
            writer.WriteQuaternion(pose.Head.rotation);
            writer.WriteVector3(pose.LeftHand.position);
            writer.WriteQuaternion(pose.LeftHand.rotation);
            writer.WriteVector3(pose.RightHand.position);
            writer.WriteQuaternion(pose.RightHand.rotation);
        }

        public static VRPose ReadPose(NetworkReader reader)
        {
            return new VRPose
            {
                Head = new Pose(reader.ReadVector3(), reader.ReadQuaternion()),
                LeftHand = new Pose(reader.ReadVector3(), reader.ReadQuaternion()),
                RightHand = new Pose(reader.ReadVector3(), reader.ReadQuaternion())
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSerializers()
        {
            Writer<VRPose>.write = WritePose;
            Reader<VRPose>.read = ReadPose;
        }
    }
}
