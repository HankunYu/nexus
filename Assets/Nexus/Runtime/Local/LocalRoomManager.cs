using System;
using System.Collections.Generic;
using Nexus.Networking.Core;
using UnityEngine;

namespace Nexus.Networking.Local
{
    /// <summary>
    /// LAN room manager. Coordinates LocalTransport for room lifecycle
    /// and tracks connected players via Mirror server events.
    /// </summary>
    public class LocalRoomManager : MonoBehaviour, INexusRoomManager
    {
        // Private fields
        private INexusTransport _transport;
        private NexusConfig _config;
        private RoomState _currentState = RoomState.Idle;
        private RoomInfo _currentRoom;
        private readonly List<NexusPlayer> _players = new List<NexusPlayer>();

        // Public properties
        public RoomState CurrentState => _currentState;
        public RoomInfo CurrentRoom => _currentRoom;
        public IReadOnlyList<NexusPlayer> Players => _players;

        // Events
        public event Action<NexusPlayer> OnPlayerJoined;
        public event Action<NexusPlayer> OnPlayerLeft;
        public event Action<RoomInfo> OnRoomCreated;
        public event Action OnRoomJoined;
        public event Action OnRoomLeft;

        // Unity callbacks
        private void OnDestroy()
        {
            if (_transport != null)
            {
                _transport.OnClientConnected -= HandleClientConnected;
                _transport.OnClientDisconnected -= HandleClientDisconnected;
            }
        }

        // Public methods
        public void Initialize(INexusTransport transport, NexusConfig config)
        {
            _transport = transport;
            _config = config;

            _transport.OnClientConnected += HandleClientConnected;
            _transport.OnClientDisconnected += HandleClientDisconnected;
        }

        public void CreateRoom(RoomConfig config)
        {
            if (_currentState != RoomState.Idle)
            {
                Debug.LogWarning($"[LocalRoomManager] Cannot create room in state {_currentState}.");
                return;
            }

            SetState(RoomState.Creating);

            _currentRoom = new RoomInfo
            {
                RoomId = Guid.NewGuid().ToString(),
                RoomName = config.RoomName,
                Port = config.Port,
                MaxPlayers = config.MaxPlayers,
                CurrentPlayers = 1,
                Metadata = config.Metadata ?? new Dictionary<string, string>()
            };

            // Add host player BEFORE StartHost to prevent duplicate from
            // HandleClientConnected(0) which fires inside StartHost.
            var hostPlayer = new NexusPlayer
            {
                ConnectionId = 0,
                PlayerId = Guid.NewGuid().ToString(),
                DisplayName = "Host",
                IsHost = true,
                IsLocal = true
            };
            _players.Add(hostPlayer);

            _transport.StartHost(config.Port);

            SetState(RoomState.InRoom);

            Debug.Log($"[LocalRoomManager] Room created: {_currentRoom.RoomName} (ID: {_currentRoom.RoomId})");
            OnRoomCreated?.Invoke(_currentRoom);
            OnPlayerJoined?.Invoke(hostPlayer);
        }

        public void JoinRoom(RoomInfo room)
        {
            if (_currentState != RoomState.Idle)
            {
                Debug.LogWarning($"[LocalRoomManager] Cannot join room in state {_currentState}.");
                return;
            }

            SetState(RoomState.Joining);

            _currentRoom = room;
            _transport.StartClient(room.HostAddress, room.Port);

            var localPlayer = new NexusPlayer
            {
                ConnectionId = -1,
                PlayerId = Guid.NewGuid().ToString(),
                DisplayName = "Player",
                IsHost = false,
                IsLocal = true
            };
            _players.Add(localPlayer);

            SetState(RoomState.InRoom);

            Debug.Log($"[LocalRoomManager] Joined room: {room.RoomName} at {room.HostAddress}:{room.Port}");
            OnRoomJoined?.Invoke();
            OnPlayerJoined?.Invoke(localPlayer);
        }

        public void LeaveRoom()
        {
            if (_currentState == RoomState.Idle)
            {
                return;
            }

            _transport.Stop();
            _players.Clear();
            _currentRoom = null;

            SetState(RoomState.Idle);

            Debug.Log("[LocalRoomManager] Left room.");
            OnRoomLeft?.Invoke();
        }

        // Private methods
        private void HandleClientConnected(int connectionId)
        {
            if (_currentRoom == null)
            {
                return;
            }

            // Skip if player already tracked (e.g., host connId=0 added in CreateRoom)
            if (_players.Exists(p => p.ConnectionId == connectionId))
            {
                return;
            }

            var player = new NexusPlayer
            {
                ConnectionId = connectionId,
                PlayerId = Guid.NewGuid().ToString(),
                DisplayName = $"Player {connectionId}",
                IsHost = false,
                IsLocal = false
            };

            _players.Add(player);
            _currentRoom.CurrentPlayers = _players.Count;

            Debug.Log($"[LocalRoomManager] Player connected: {player.DisplayName} (conn: {connectionId})");
            OnPlayerJoined?.Invoke(player);
        }

        private void HandleClientDisconnected(int connectionId)
        {
            NexusPlayer player = _players.Find(p => p.ConnectionId == connectionId);
            if (player == null)
            {
                return;
            }

            _players.Remove(player);

            if (_currentRoom != null)
            {
                _currentRoom.CurrentPlayers = _players.Count;
            }

            Debug.Log($"[LocalRoomManager] Player disconnected: {player.DisplayName} (conn: {connectionId})");
            OnPlayerLeft?.Invoke(player);
        }

        private void SetState(RoomState newState)
        {
            if (_currentState == newState)
            {
                return;
            }

            _currentState = newState;
        }
    }
}
