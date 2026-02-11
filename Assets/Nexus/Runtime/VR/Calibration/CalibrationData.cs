using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    public struct CalibrationData
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public static readonly CalibrationData Identity = new CalibrationData
        {
            Position = Vector3.zero,
            Rotation = Quaternion.identity
        };

        public static void WriteCalibrationData(NetworkWriter writer, CalibrationData data)
        {
            writer.WriteVector3(data.Position);
            writer.WriteQuaternion(data.Rotation);
        }

        public static CalibrationData ReadCalibrationData(NetworkReader reader)
        {
            return new CalibrationData
            {
                Position = reader.ReadVector3(),
                Rotation = reader.ReadQuaternion()
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSerializers()
        {
            Writer<CalibrationData>.write = WriteCalibrationData;
            Reader<CalibrationData>.read = ReadCalibrationData;
        }
    }
}
