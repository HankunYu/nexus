using System;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Two-point alignment calibration. Host defines two physical reference points.
    /// Each client records the same two points from their tracking space.
    /// System computes the position + Y-axis rotation offset.
    /// </summary>
    public class ManualCalibrationProvider : ICalibrationProvider
    {
        // Private fields
        private Vector3 _referencePointA;
        private Vector3 _referencePointB;
        private bool _hasReferencePoints;
        private Vector3 _localPointA;
        private bool _hasLocalPointA;

        // Public properties
        public CalibrationState State { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;
        public event Action<string> OnCalibrationFailed;

        // Public methods
        public void SetReferencePoints(Vector3 pointA, Vector3 pointB)
        {
            _referencePointA = pointA;
            _referencePointB = pointB;
            _hasReferencePoints = true;
        }

        public void StartCalibration()
        {
            if (!_hasReferencePoints)
            {
                State = CalibrationState.Failed;
                OnCalibrationFailed?.Invoke("Reference points not set. Host must record reference points first.");
                return;
            }

            State = CalibrationState.InProgress;
            _hasLocalPointA = false;
        }

        public void CancelCalibration()
        {
            State = CalibrationState.None;
            _hasLocalPointA = false;
        }

        /// <summary>
        /// Record a local point from the client's controller.
        /// Call twice: first for point A, then for point B.
        /// After the second call, calibration is computed automatically.
        /// </summary>
        public void RecordPoint(Vector3 controllerPosition)
        {
            if (State != CalibrationState.InProgress)
            {
                return;
            }

            if (!_hasLocalPointA)
            {
                _localPointA = controllerPosition;
                _hasLocalPointA = true;
                return;
            }

            // Second point — compute calibration
            CalibrationData data = ComputeCalibration(
                _referencePointA, _referencePointB,
                _localPointA, controllerPosition);

            State = CalibrationState.Calibrated;
            OnCalibrationComplete?.Invoke(data);
        }

        /// <summary>
        /// Compute calibration offset from two pairs of corresponding points.
        /// Projects directions to XZ plane for Y-axis-only rotation.
        /// </summary>
        public static CalibrationData ComputeCalibration(
            Vector3 refA, Vector3 refB, Vector3 localA, Vector3 localB)
        {
            // Project direction vectors to XZ plane (Y-axis rotation only)
            Vector3 refDir = refB - refA;
            refDir.y = 0f;

            Vector3 localDir = localB - localA;
            localDir.y = 0f;

            // Guard against degenerate input (points too close in XZ plane)
            if (refDir.sqrMagnitude < 0.0001f || localDir.sqrMagnitude < 0.0001f)
            {
                Vector3 position = refA - localA;
                return new CalibrationData
                {
                    Position = position,
                    Rotation = Quaternion.identity
                };
            }

            refDir.Normalize();
            localDir.Normalize();

            // Compute Y-axis rotation offset
            Quaternion rotation = Quaternion.FromToRotation(localDir, refDir);

            // Compute position offset: refA = rotation * localA + position
            Vector3 position = refA - rotation * localA;

            return new CalibrationData
            {
                Position = position,
                Rotation = rotation
            };
        }
    }
}
