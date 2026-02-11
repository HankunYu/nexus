using System;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Data shared from host to clients for anchor-based calibration.
    /// Contains the anchor UUID, group UUID, and the host's anchor world pose.
    /// </summary>
    public struct AnchorShareData
    {
        public Guid AnchorUuid;
        public Guid GroupUuid;
        public Vector3 HostPosition;
        public Quaternion HostRotation;
    }

    /// <summary>
    /// Extended calibration provider for anchor-based methods (Meta Spatial Anchor, etc.).
    /// The manager detects this interface and handles network transport of anchor data
    /// instead of using the manual reference-point flow.
    /// </summary>
    public interface IAnchorCalibrationProvider : ICalibrationProvider
    {
        /// <summary>
        /// Fired when host creates and shares an anchor successfully.
        /// The manager should broadcast this data to all clients via network.
        /// </summary>
        event Action<AnchorShareData> OnAnchorShared;

        /// <summary>
        /// Called on the client side when anchor share data is received from the network.
        /// The provider should load, localize, and compute calibration from this data.
        /// </summary>
        void LoadSharedAnchor(AnchorShareData data);
    }
}
