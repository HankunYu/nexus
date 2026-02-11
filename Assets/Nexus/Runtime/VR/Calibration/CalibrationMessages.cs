using System;
using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Sent from Host to all clients with the two reference points for calibration.
    /// </summary>
    public struct ReferencePointsMessage : NetworkMessage
    {
        public Vector3 PointA;
        public Vector3 PointB;
    }

    /// <summary>
    /// Sent from client to server with calibration result, then relayed to all clients.
    /// </summary>
    public struct CalibrationResultMessage : NetworkMessage
    {
        public int ConnectionId;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>
    /// Sent from Host to all clients with shared spatial anchor data for anchor-based calibration.
    /// </summary>
    public struct AnchorShareMessage : NetworkMessage
    {
        public Guid AnchorUuid;
        public Guid GroupUuid;
        public Vector3 HostPosition;
        public Quaternion HostRotation;
    }
}
