using System;
using System.Collections.Generic;
using Nexus.Networking.VR;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Manages spatial calibration lifecycle. Coordinates with an ICalibrationProvider
    /// to compute per-player offsets and applies them to NexusVRPlayer root Transforms.
    /// </summary>
    public class SpatialCalibrationManager : MonoBehaviour
    {
        // Private fields
        private ICalibrationProvider _provider;
        private NexusVRPlayerManager _vrPlayerManager;
        private Vector3 _firstReferencePoint;
        private bool _hasFirstPoint;
        private readonly Dictionary<int, CalibrationData> _calibrationsByConnectionId =
            new Dictionary<int, CalibrationData>();

        // Public properties
        public CalibrationState State => _provider?.State ?? CalibrationState.None;
        public CalibrationData LocalCalibration { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;

        // Unity callbacks
        private void OnDestroy()
        {
            if (_provider != null)
            {
                _provider.OnCalibrationComplete -= HandleCalibrationComplete;
                _provider.OnCalibrationFailed -= HandleCalibrationFailed;
            }

            if (_vrPlayerManager != null)
            {
                _vrPlayerManager.OnVRPlayerSpawned -= HandleVRPlayerSpawned;
            }
        }

        // Public methods
        public void Initialize(ICalibrationProvider provider)
        {
            Initialize(provider, null);
        }

        public void Initialize(ICalibrationProvider provider, NexusVRPlayerManager vrPlayerManager)
        {
            _provider = provider;
            _vrPlayerManager = vrPlayerManager;

            _provider.OnCalibrationComplete += HandleCalibrationComplete;
            _provider.OnCalibrationFailed += HandleCalibrationFailed;

            if (_vrPlayerManager != null)
            {
                _vrPlayerManager.OnVRPlayerSpawned += HandleVRPlayerSpawned;
            }
        }

        /// <summary>
        /// Host records a reference point. Call twice to define the calibration axis.
        /// After the second call, reference points are set on the provider.
        /// </summary>
        public void RecordReferencePoint(Vector3 controllerPosition)
        {
            if (!_hasFirstPoint)
            {
                _firstReferencePoint = controllerPosition;
                _hasFirstPoint = true;
                Debug.Log("[SpatialCalibrationManager] First reference point recorded.");
                return;
            }

            _provider.SetReferencePoints(_firstReferencePoint, controllerPosition);
            _hasFirstPoint = false;
            Debug.Log("[SpatialCalibrationManager] Reference points set on provider.");
        }

        /// <summary>
        /// Apply calibration offset to a VR player's root Transform.
        /// </summary>
        public void ApplyCalibrationToPlayer(NexusVRPlayer player, CalibrationData data)
        {
            player.transform.position = data.Position;
            player.transform.rotation = data.Rotation;
        }

        /// <summary>
        /// Store calibration data for a specific connection.
        /// </summary>
        public void StoreCalibration(int connectionId, CalibrationData data)
        {
            _calibrationsByConnectionId[connectionId] = data;
        }

        /// <summary>
        /// Try to retrieve stored calibration for a connection.
        /// </summary>
        public bool TryGetCalibration(int connectionId, out CalibrationData data)
        {
            return _calibrationsByConnectionId.TryGetValue(connectionId, out data);
        }

        // Private methods
        private void HandleCalibrationComplete(CalibrationData data)
        {
            LocalCalibration = data;
            Debug.Log($"[SpatialCalibrationManager] Calibration complete: pos={data.Position}, rot={data.Rotation.eulerAngles}");
            OnCalibrationComplete?.Invoke(data);
        }

        private void HandleCalibrationFailed(string message)
        {
            Debug.LogWarning($"[SpatialCalibrationManager] Calibration failed: {message}");
        }

        private void HandleVRPlayerSpawned(NexusVRPlayer player)
        {
            if (_vrPlayerManager != null && player == _vrPlayerManager.LocalPlayer)
            {
                if (State == CalibrationState.Calibrated)
                {
                    ApplyCalibrationToPlayer(player, LocalCalibration);
                }
            }
        }
    }
}
