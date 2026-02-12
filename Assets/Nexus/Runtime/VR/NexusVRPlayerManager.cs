using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using Nexus.Networking.Core;
using UnityEngine;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Manages VR player lifecycle: spawn/despawn NexusVRPlayer instances
    /// in response to room events. Bridges NexusSession with VR sync.
    /// Uses Mirror's RegisterSpawnHandler for runtime network object spawning.
    /// </summary>
    public class NexusVRPlayerManager : MonoBehaviour
    {
        // Constants
        private const uint VRPlayerAssetId = 0x4E580001;

        // Private fields
        private INexusRoomManager _roomManager;
        private NexusConfig _config;
        private NexusVRPlayer _localPlayer;
        private readonly List<NexusVRPlayer> _remotePlayers = new List<NexusVRPlayer>();
        private readonly Dictionary<int, NexusVRPlayer> _playersByConnectionId = new Dictionary<int, NexusVRPlayer>();
        private bool _spawnHandlersRegistered;

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

            if (_spawnHandlersRegistered)
            {
                NetworkClient.UnregisterSpawnHandler(VRPlayerAssetId);
                _spawnHandlersRegistered = false;
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

            SetupMirrorSpawning();
        }

        /// <summary>
        /// Called by NexusVRPlayer.OnStartAuthority() on the owning client.
        /// </summary>
        public void RegisterLocalPlayer(NexusVRPlayer player)
        {
            _localPlayer = player;
            player.TrackingProvider = TrackingProvider;
            player.CustomState = CustomState;
            player.ConfigureSync(_config.SyncRateHz);

            int connId = (int)player.netId;
            _playersByConnectionId[connId] = player;

            Debug.Log($"[NexusVRPlayerManager] Local VR player registered (netId={player.netId})");
            OnVRPlayerSpawned?.Invoke(player);
        }

        /// <summary>
        /// Called by NexusVRPlayer.OnStartClient() on non-owner clients.
        /// </summary>
        public void RegisterRemotePlayer(NexusVRPlayer player)
        {
            player.ConfigureSync(_config.SyncRateHz);
            _remotePlayers.Add(player);

            int connId = (int)player.netId;
            _playersByConnectionId[connId] = player;

            Debug.Log($"[NexusVRPlayerManager] Remote VR player registered (netId={player.netId})");
            OnVRPlayerSpawned?.Invoke(player);
        }

        /// <summary>
        /// Reverse-lookup connection ID for a given VRPlayer.
        /// Returns -1 if not found.
        /// </summary>
        public int GetConnectionId(NexusVRPlayer player)
        {
            foreach (var kvp in _playersByConnectionId)
            {
                if (kvp.Value == player)
                {
                    return kvp.Key;
                }
            }

            return -1;
        }

        // Private methods
        private void SetupMirrorSpawning()
        {
            if (_spawnHandlersRegistered)
            {
                return;
            }

            NetworkClient.RegisterSpawnHandler(
                VRPlayerAssetId,
                OnClientSpawnHandler,
                OnClientUnspawnHandler);

            _spawnHandlersRegistered = true;
            Debug.Log("[NexusVRPlayerManager] Mirror spawn handlers registered.");
        }

        private GameObject OnClientSpawnHandler(SpawnMessage msg)
        {
            var playerObj = new GameObject($"VRPlayer_Remote_{msg.netId}");
            playerObj.transform.SetParent(transform, false);
            playerObj.AddComponent<NexusVRPlayer>();
            return playerObj;
        }

        private void OnClientUnspawnHandler(GameObject spawned)
        {
            if (spawned != null)
            {
                Destroy(spawned);
            }
        }

        private void HandlePlayerJoined(NexusPlayer player)
        {
            // In networked mode, only the server spawns VR player objects
            if (NetworkServer.active)
            {
                SpawnNetworkedPlayer(player);
                return;
            }

            // Non-networked fallback (testing / offline)
            if (!NetworkClient.active)
            {
                SpawnLocalOnly(player);
            }

            // Client side: Mirror will auto-spawn via RegisterSpawnHandler
        }

        private void SpawnNetworkedPlayer(NexusPlayer player)
        {
            // Find the owner connection
            NetworkConnectionToClient ownerConn = null;
            if (NetworkServer.connections.TryGetValue(player.ConnectionId, out var conn))
            {
                ownerConn = conn;
            }
            else if (player.ConnectionId == 0)
            {
                ownerConn = NetworkServer.localConnection;
            }

            // Mirror requires conn.isReady before ShowForConnection sends SpawnMessage.
            // In host mode, localConnection becomes ready next frame (QueueConnectedEvent).
            // Defer spawn until connection is ready to avoid silent drop.
            if (ownerConn != null && !ownerConn.isReady)
            {
                StartCoroutine(SpawnWhenReady(player, ownerConn));
                return;
            }

            DoSpawn(player, ownerConn);
        }

        private IEnumerator SpawnWhenReady(NexusPlayer player, NetworkConnectionToClient ownerConn)
        {
            Debug.Log($"[NexusVRPlayerManager] Waiting for connection ready: {player.DisplayName} (conn={player.ConnectionId})");

            while (!ownerConn.isReady)
            {
                yield return null;
            }

            DoSpawn(player, ownerConn);
        }

        private void DoSpawn(NexusPlayer player, NetworkConnectionToClient ownerConn)
        {
            var playerObj = new GameObject($"VRPlayer_{player.DisplayName}");
            playerObj.transform.SetParent(transform, false);

            playerObj.AddComponent<NetworkIdentity>();
            playerObj.AddComponent<NexusVRPlayer>();

            // Use the 3-arg overload: internally sets identity.assetId (internal setter)
            NetworkServer.Spawn(playerObj, VRPlayerAssetId, ownerConn);

            Debug.Log($"[NexusVRPlayerManager] Server spawned VR player: {player.DisplayName} (conn={player.ConnectionId})");
        }

        private void SpawnLocalOnly(NexusPlayer player)
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

            Debug.Log($"[NexusVRPlayerManager] VR player spawned (local-only): {player.DisplayName}");
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
                if (NetworkServer.active)
                {
                    NetworkServer.Destroy(vrPlayer.gameObject);
                }
                else
                {
                    Destroy(vrPlayer.gameObject);
                }
            }

            Debug.Log($"[NexusVRPlayerManager] VR player despawned (conn={connectionId})");
        }
    }
}
