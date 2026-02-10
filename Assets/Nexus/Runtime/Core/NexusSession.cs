using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Central hub for Nexus networking. Manages session state and coordinates
    /// Transport, Discovery, and RoomManager based on the configured NexusMode.
    /// </summary>
    public class NexusSession : MonoBehaviour
    {
        [SerializeField] private NexusConfig _config;

        private INexusTransport _transport;
        private INexusDiscovery _discovery;
        private INexusRoomManager _roomManager;
        private SessionState _state = SessionState.Idle;

        public static NexusSession Instance { get; private set; }

        public NexusConfig Config => _config;
        public INexusTransport Transport => _transport;
        public INexusDiscovery Discovery => _discovery;
        public INexusRoomManager RoomManager => _roomManager;
        public SessionState State => _state;

        public event Action<SessionState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Shutdown();
                Instance = null;
            }
        }

        /// <summary>
        /// Initialize the session with the specified implementations.
        /// Call this before any networking operations.
        /// </summary>
        public void Initialize(
            INexusTransport transport,
            INexusDiscovery discovery,
            INexusRoomManager roomManager)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            _roomManager = roomManager ?? throw new ArgumentNullException(nameof(roomManager));

            SubscribeToEvents();

            Debug.Log($"[NexusSession] Initialized in {_config.Mode} mode.");
        }

        /// <summary>
        /// Create and host a new room.
        /// </summary>
        public void CreateRoom(string roomName = null)
        {
            if (_state != SessionState.Idle)
            {
                Debug.LogWarning($"[NexusSession] Cannot create room in state {_state}.");
                return;
            }

            SetState(SessionState.Creating);

            var config = new RoomConfig
            {
                RoomName = roomName ?? "Nexus Room",
                MaxPlayers = _config.MaxPlayers,
                Port = _config.Port
            };

            _roomManager.CreateRoom(config);
        }

        /// <summary>
        /// Join an existing room.
        /// </summary>
        public void JoinRoom(RoomInfo room)
        {
            if (_state != SessionState.Idle)
            {
                Debug.LogWarning($"[NexusSession] Cannot join room in state {_state}.");
                return;
            }

            SetState(SessionState.Joining);
            _roomManager.JoinRoom(room);
        }

        /// <summary>
        /// Leave the current room and return to idle state.
        /// </summary>
        public void LeaveRoom()
        {
            if (_state == SessionState.Idle)
            {
                return;
            }

            _roomManager.LeaveRoom();
            _discovery.Stop();
            _transport.Stop();

            SetState(SessionState.Idle);
        }

        /// <summary>
        /// Start listening for available rooms.
        /// </summary>
        public void StartDiscovery()
        {
            _discovery.StartListening();
        }

        /// <summary>
        /// Stop listening for available rooms.
        /// </summary>
        public void StopDiscovery()
        {
            _discovery.Stop();
        }

        /// <summary>
        /// Shut down all networking activity.
        /// </summary>
        public void Shutdown()
        {
            _discovery?.Stop();
            _transport?.Stop();
            UnsubscribeFromEvents();
            SetState(SessionState.Idle);
        }

        private void SetState(SessionState newState)
        {
            if (_state == newState)
            {
                return;
            }

            SessionState previousState = _state;
            _state = newState;

            Debug.Log($"[NexusSession] {previousState} -> {newState}");
            OnStateChanged?.Invoke(newState);
        }

        private void SubscribeToEvents()
        {
            _roomManager.OnRoomCreated += HandleRoomCreated;
            _roomManager.OnRoomJoined += HandleRoomJoined;
            _roomManager.OnRoomLeft += HandleRoomLeft;
            _transport.OnClientDisconnected += HandleClientDisconnected;
        }

        private void UnsubscribeFromEvents()
        {
            if (_roomManager != null)
            {
                _roomManager.OnRoomCreated -= HandleRoomCreated;
                _roomManager.OnRoomJoined -= HandleRoomJoined;
                _roomManager.OnRoomLeft -= HandleRoomLeft;
            }

            if (_transport != null)
            {
                _transport.OnClientDisconnected -= HandleClientDisconnected;
            }
        }

        private void HandleRoomCreated(RoomInfo room)
        {
            SetState(SessionState.Hosting);

            // Start broadcasting the room for discovery
            _discovery.StartBroadcast(room);

            SetState(SessionState.InRoom);
        }

        private void HandleRoomJoined()
        {
            SetState(SessionState.Connected);
            SetState(SessionState.InRoom);
        }

        private void HandleRoomLeft()
        {
            SetState(SessionState.Idle);
        }

        private void HandleClientDisconnected(int connectionId)
        {
            // Only handle our own disconnection (connectionId 0 is local client)
            if (_state == SessionState.InRoom && connectionId == 0)
            {
                SetState(SessionState.Disconnected);
            }
        }

        /// <summary>
        /// Called by ReconnectHandler to update session state during reconnection.
        /// </summary>
        public void SetReconnecting()
        {
            if (_state == SessionState.Disconnected)
            {
                SetState(SessionState.Reconnecting);
            }
        }

        /// <summary>
        /// Called by ReconnectHandler when reconnection succeeds.
        /// </summary>
        public void SetReconnected()
        {
            if (_state == SessionState.Reconnecting)
            {
                SetState(SessionState.InRoom);
            }
        }

        /// <summary>
        /// Called by ReconnectHandler when all reconnection attempts fail.
        /// </summary>
        public void SetReconnectFailed()
        {
            if (_state == SessionState.Reconnecting)
            {
                LeaveRoom();
            }
        }
    }
}
