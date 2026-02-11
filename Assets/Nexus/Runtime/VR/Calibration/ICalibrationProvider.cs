using System;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Interface for spatial calibration methods.
    /// Implement to provide platform-specific calibration (Meta Spatial Anchor, AR Marker, etc.)
    /// or use ManualCalibrationProvider for universal two-point alignment.
    /// </summary>
    public interface ICalibrationProvider
    {
        CalibrationState State { get; }
        event Action<CalibrationData> OnCalibrationComplete;
        event Action<string> OnCalibrationFailed;

        /// <summary>
        /// Set the host's reference points for alignment.
        /// Manual providers use these for two-point math.
        /// Platform-specific providers may ignore this.
        /// </summary>
        void SetReferencePoints(Vector3 pointA, Vector3 pointB);

        void StartCalibration();
        void CancelCalibration();
    }
}
