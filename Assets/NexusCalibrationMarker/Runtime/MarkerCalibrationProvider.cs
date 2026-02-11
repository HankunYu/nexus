using System;
using Nexus.Networking.VR.Calibration;
using UnityEngine;

namespace Nexus.Calibration.Marker
{
    /// <summary>
    /// Calibration provider using physical markers (QR codes, ArUco, etc.)
    /// detected via AR image tracking.
    /// Requires AR Foundation (com.unity.xr.arfoundation).
    /// Each player views the same physical marker; the detected 6DOF pose
    /// differences provide semi-automatic calibration.
    /// </summary>
    public class MarkerCalibrationProvider : MonoBehaviour, ICalibrationProvider
    {
        // Public properties
        public CalibrationState State { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;
        public event Action<string> OnCalibrationFailed;

        // Public methods
        public void SetReferencePoints(Vector3 pointA, Vector3 pointB)
        {
            // Not used by Marker provider — calibration is based on marker detection
        }

        public void StartCalibration()
        {
#if AR_FOUNDATION
            State = CalibrationState.InProgress;
            // TODO: Implement AR marker tracking flow
            // 1. Enable ARTrackedImageManager
            // 2. Wait for marker detection → get marker's 6DOF world pose
            // 3. Compare marker pose across devices → compute CalibrationData
            // 4. Fire OnCalibrationComplete
            Debug.Log("[MarkerCalibrationProvider] Starting marker detection...");
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("Marker calibration not yet implemented. Install AR Foundation and complete TODO.");
#else
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("AR Foundation not available. Install com.unity.xr.arfoundation.");
#endif
        }

        public void CancelCalibration()
        {
            State = CalibrationState.None;
        }
    }
}
