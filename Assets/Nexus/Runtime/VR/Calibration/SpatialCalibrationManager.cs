using System;
using System.Collections.Generic;
using Mirror;
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
        private bool _networkHandlersRegistered;
        private readonly Dictionary<int, CalibrationData> _calibrationsByConnectionId =
            new Dictionary<int, CalibrationData>();

        // Public properties
        public CalibrationState State => _provider?.State ?? CalibrationState.None;
        public CalibrationData LocalCalibration { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;

        // Unity callbacks
        private void Update()
        {
            // Auto-register network handlers when Mirror becomes active
            if (!_networkHandlersRegistered && _provider != null &&
                (NetworkServer.active || NetworkClient.active))
            {
                RegisterNetworkHandlers();
            }
        }

        private void OnDestroy()
        {
            if (_provider != null)
            {
                _provider.OnCalibrationComplete -= HandleCalibrationComplete;
                _provider.OnCalibrationFailed -= HandleCalibrationFailed;

                if (_provider is IAnchorCalibrationProvider anchorProvider)
                {
                    anchorProvider.OnAnchorShared -= HandleAnchorShared;
                }
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
            // Unsubscribe from previous provider/manager if re-initialized
            if (_provider != null)
            {
                _provider.OnCalibrationComplete -= HandleCalibrationComplete;
                _provider.OnCalibrationFailed -= HandleCalibrationFailed;

                if (_provider is IAnchorCalibrationProvider oldAnchorProvider)
                {
                    oldAnchorProvider.OnAnchorShared -= HandleAnchorShared;
                }
            }

            if (_vrPlayerManager != null)
            {
                _vrPlayerManager.OnVRPlayerSpawned -= HandleVRPlayerSpawned;
            }

            _provider = provider;
            _vrPlayerManager = vrPlayerManager;

            _provider.OnCalibrationComplete += HandleCalibrationComplete;
            _provider.OnCalibrationFailed += HandleCalibrationFailed;

            if (_provider is IAnchorCalibrationProvider anchorProvider)
            {
                anchorProvider.OnAnchorShared += HandleAnchorShared;
            }

            if (_vrPlayerManager != null)
            {
                _vrPlayerManager.OnVRPlayerSpawned += HandleVRPlayerSpawned;
            }
        }

        /// <summary>
        /// Host records a reference point. Call twice to define the calibration axis.
        /// After the second call, reference points are set on the provider and broadcast to clients.
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
            BroadcastReferencePoints(_firstReferencePoint, controllerPosition);
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

        /// <summary>
        /// Register Mirror network message handlers. Call after Mirror server/client is active.
        /// </summary>
        public void RegisterNetworkHandlers()
        {
            if (_networkHandlersRegistered)
            {
                return;
            }

            if (NetworkServer.active)
            {
                NetworkServer.RegisterHandler<CalibrationResultMessage>(OnServerReceivedCalibration);
            }

            if (NetworkClient.active)
            {
                NetworkClient.RegisterHandler<ReferencePointsMessage>(OnClientReceivedReferencePoints);
                NetworkClient.RegisterHandler<CalibrationResultMessage>(OnClientReceivedCalibration);
                NetworkClient.RegisterHandler<AnchorShareMessage>(OnClientReceivedAnchorShare);
            }

            _networkHandlersRegistered = true;
            Debug.Log("[SpatialCalibrationManager] Network handlers registered.");
        }

        /// <summary>
        /// Unregister network handlers. Call before leaving room.
        /// </summary>
        public void UnregisterNetworkHandlers()
        {
            if (!_networkHandlersRegistered)
            {
                return;
            }

            if (NetworkServer.active)
            {
                NetworkServer.UnregisterHandler<CalibrationResultMessage>();
            }

            if (NetworkClient.active)
            {
                NetworkClient.UnregisterHandler<ReferencePointsMessage>();
                NetworkClient.UnregisterHandler<CalibrationResultMessage>();
                NetworkClient.UnregisterHandler<AnchorShareMessage>();
            }

            _networkHandlersRegistered = false;
        }

        /// <summary>
        /// Broadcast reference points to all clients. Called after host records both points.
        /// </summary>
        public void BroadcastReferencePoints(Vector3 pointA, Vector3 pointB)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[SpatialCalibrationManager] Cannot broadcast: server not active.");
                return;
            }

            NetworkServer.SendToAll(new ReferencePointsMessage
            {
                PointA = pointA,
                PointB = pointB
            });
        }

        /// <summary>
        /// Send local calibration result to server. Called by client after calibration completes.
        /// </summary>
        public void SendCalibrationToServer(CalibrationData data)
        {
            if (!NetworkClient.active)
            {
                Debug.LogWarning("[SpatialCalibrationManager] Cannot send: client not active.");
                return;
            }

            // ConnectionId is filled in by the server (server-authoritative)
            NetworkClient.Send(new CalibrationResultMessage
            {
                Position = data.Position,
                Rotation = data.Rotation
            });
        }

        // Private methods
        private void HandleCalibrationComplete(CalibrationData data)
        {
            LocalCalibration = data;
            Debug.Log($"[SpatialCalibrationManager] Calibration complete: pos={data.Position}, rot={data.Rotation.eulerAngles}");
            OnCalibrationComplete?.Invoke(data);
            SendCalibrationToServer(data);
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

        private void OnClientReceivedReferencePoints(ReferencePointsMessage msg)
        {
            Debug.Log("[SpatialCalibrationManager] Received reference points from host.");
            _provider?.SetReferencePoints(msg.PointA, msg.PointB);
        }

        private void OnServerReceivedCalibration(
            NetworkConnectionToClient conn, CalibrationResultMessage msg)
        {
            var data = new CalibrationData
            {
                Position = msg.Position,
                Rotation = msg.Rotation
            };
            // Use server-authoritative connection ID
            msg.ConnectionId = conn.connectionId;
            StoreCalibration(msg.ConnectionId, data);
            NetworkServer.SendToAll(msg);
        }

        private void OnClientReceivedCalibration(CalibrationResultMessage msg)
        {
            var data = new CalibrationData
            {
                Position = msg.Position,
                Rotation = msg.Rotation
            };
            StoreCalibration(msg.ConnectionId, data);
            Debug.Log($"[SpatialCalibrationManager] Applied calibration for connection {msg.ConnectionId}.");
        }

        private void HandleAnchorShared(AnchorShareData data)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[SpatialCalibrationManager] Cannot broadcast anchor: server not active.");
                return;
            }

            NetworkServer.SendToAll(new AnchorShareMessage
            {
                AnchorUuid = data.AnchorUuid,
                GroupUuid = data.GroupUuid,
                HostPosition = data.HostPosition,
                HostRotation = data.HostRotation
            });
            Debug.Log($"[SpatialCalibrationManager] Broadcast anchor share: {data.AnchorUuid}");
        }

        private void OnClientReceivedAnchorShare(AnchorShareMessage msg)
        {
            // Host already calibrated as Identity; skip anchor load on host
            if (NetworkServer.active)
            {
                return;
            }

            if (_provider is IAnchorCalibrationProvider anchorProvider)
            {
                var data = new AnchorShareData
                {
                    AnchorUuid = msg.AnchorUuid,
                    GroupUuid = msg.GroupUuid,
                    HostPosition = msg.HostPosition,
                    HostRotation = msg.HostRotation
                };
                anchorProvider.LoadSharedAnchor(data);
                Debug.Log($"[SpatialCalibrationManager] Received anchor share, loading: {msg.AnchorUuid}");
            }
        }
    }
}
