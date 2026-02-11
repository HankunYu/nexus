using System;
using Nexus.Networking.VR.Calibration;
using UnityEngine;

namespace Nexus.Calibration.Meta
{
    /// <summary>
    /// Calibration provider using Meta Quest Shared Spatial Anchors.
    /// Requires Meta XR SDK (com.meta.xr.sdk.core).
    /// Host creates a spatial anchor, shares it, and clients localize to it.
    /// The pose difference provides automatic calibration with no user interaction.
    /// </summary>
    public class MetaSpatialAnchorProvider : MonoBehaviour, ICalibrationProvider
    {
        // Public properties
        public CalibrationState State { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;
        public event Action<string> OnCalibrationFailed;

        // Public methods
        public void SetReferencePoints(Vector3 pointA, Vector3 pointB)
        {
            // Not used by Meta Spatial Anchor — calibration is automatic
        }

        public void StartCalibration()
        {
#if META_XR_SDK
            State = CalibrationState.InProgress;
            // TODO: Implement Meta Shared Spatial Anchor flow
            // 1. If host: OVRSpatialAnchor.Create() -> Share() -> send UUID to clients
            // 2. If client: receive UUID -> OVRSpatialAnchor.Localize()
            // 3. Compare anchor world pose with tracking origin -> compute CalibrationData
            Debug.Log("[MetaSpatialAnchorProvider] Starting Shared Spatial Anchor calibration...");
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("Meta Spatial Anchor not yet implemented. Install Meta XR SDK and complete TODO.");
#else
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("Meta XR SDK not available. Install com.meta.xr.sdk.core.");
#endif
        }

        public void CancelCalibration()
        {
            State = CalibrationState.None;
        }
    }
}
