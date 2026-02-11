using System;
using System.Collections.Generic;
using Nexus.Networking.Core;
using UnityEngine;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Manages VR player lifecycle: spawn/despawn NexusVRPlayer instances
    /// in response to room events. Bridges NexusSession with VR sync.
    /// </summary>
    public class NexusVRPlayerManager : MonoBehaviour
    {
        // Private fields
        private INexusRoomManager _roomManager;
        private NexusConfig _config;
        private NexusVRPlayer _localPlayer;
        private readonly List<NexusVRPlayer> _remotePlayers = new List<NexusVRPlayer>();
        private readonly Dictionary<int, NexusVRPlayer> _playersByConnectionId = new Dictionary<int, NexusVRPlayer>();

        // Public properties
        public IVRTrackingProvider TrackingProvider { get; set; }
        public IVRCustomState CustomState { get; set; }
        public NexusVRPlayer LocalPlayer => _localPlayer;
        public IReadOnlyList<NexusVRPlayer> RemotePlayers => _remotePlayers;

        // Events
        public event Action<NexusVRPlayer> OnVRPlayerSpawned;
        public event Action<NexusVRPlayer> OnVRPlayerDespawned;

        // Unity callbacks
        private void OnDestroy()
        {
            if (_roomManager != null)
            {
                _roomManager.OnPlayerJoined -= HandlePlayerJoined;
                _roomManager.OnPlayerLeft -= HandlePlayerLeft;
                _roomManager.OnRoomLeft -= HandleRoomLeft;
            }
        }

        // Public methods
        public void Initialize(INexusRoomManager roomManager, NexusConfig config)
        {
            _roomManager = roomManager;
            _config = config;

            _roomManager.OnPlayerJoined += HandlePlayerJoined;
            _roomManager.OnPlayerLeft += HandlePlayerLeft;
            _roomManager.OnRoomLeft += HandleRoomLeft;
        }

        // Private methods
        private void HandlePlayerJoined(NexusPlayer player)
        {
            var playerObj = new GameObject($"VRPlayer_{player.DisplayName}");
            playerObj.transform.SetParent(transform, false);
            var vrPlayer = playerObj.AddComponent<NexusVRPlayer>();
            vrPlayer.ConfigureSync(_config.SyncRateHz);

            _playersByConnectionId[player.ConnectionId] = vrPlayer;

            if (player.IsLocal)
            {
                _localPlayer = vrPlayer;
                vrPlayer.TrackingProvider = TrackingProvider;
                vrPlayer.CustomState = CustomState;
            }
            else
            {
                _remotePlayers.Add(vrPlayer);
            }

            Debug.Log($"[NexusVRPlayerManager] VR player spawned: {player.DisplayName} (local={player.IsLocal})");
            OnVRPlayerSpawned?.Invoke(vrPlayer);
        }

        private void HandlePlayerLeft(NexusPlayer player)
        {
            if (!_playersByConnectionId.TryGetValue(player.ConnectionId, out NexusVRPlayer vrPlayer))
            {
                return;
            }

            DespawnPlayer(player.ConnectionId, vrPlayer);
        }

        private void HandleRoomLeft()
        {
            var connectionIds = new List<int>(_playersByConnectionId.Keys);
            foreach (int connId in connectionIds)
            {
                if (_playersByConnectionId.TryGetValue(connId, out NexusVRPlayer vrPlayer))
                {
                    DespawnPlayer(connId, vrPlayer);
                }
            }
        }

        private void DespawnPlayer(int connectionId, NexusVRPlayer vrPlayer)
        {
            OnVRPlayerDespawned?.Invoke(vrPlayer);

            if (_localPlayer == vrPlayer)
            {
                _localPlayer = null;
            }
            else
            {
                _remotePlayers.Remove(vrPlayer);
            }

            _playersByConnectionId.Remove(connectionId);

            if (vrPlayer != null && vrPlayer.gameObject != null)
            {
                Destroy(vrPlayer.gameObject);
            }

            Debug.Log($"[NexusVRPlayerManager] VR player despawned (conn={connectionId})");
        }
    }
}
