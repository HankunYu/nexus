using System;
using UnityEngine;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Central hub for Nexus networking. Manages session state and coordinates
    /// Transport, Discovery, and RoomManager based on the configured NexusMode.
    /// </summary>
    public class NexusSession : MonoBehaviour
    {
        // Serialized fields
        [SerializeField] private NexusConfig _config;

        // Private fields
        private INexusTransport _transport;
        private INexusDiscovery _discovery;
        private INexusRoomManager _roomManager;
        private SessionState _state = SessionState.Idle;

        // Public properties
        public static NexusSession Instance { get; private set; }
        public NexusConfig Config => _config;
        public INexusTransport Transport => _transport;
        public INexusDiscovery Discovery => _discovery;
        public INexusRoomManager RoomManager => _roomManager;
        public SessionState State => _state;

        // Events
        public event Action<SessionState> OnStateChanged;

        // Unity callbacks
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

        // Public methods
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

        public void StartDiscovery()
        {
            _discovery.StartListening();
        }

        public void StopDiscovery()
        {
            _discovery.Stop();
        }

        public void Shutdown()
        {
            _discovery?.Stop();
            _transport?.Stop();
            UnsubscribeFromEvents();
            SetState(SessionState.Idle);
        }

        public void SetReconnecting()
        {
            if (_state == SessionState.Disconnected)
            {
                SetState(SessionState.Reconnecting);
            }
        }

        public void SetReconnected()
        {
            if (_state == SessionState.Reconnecting)
            {
                SetState(SessionState.InRoom);
            }
        }

        public void SetReconnectFailed()
        {
            if (_state == SessionState.Reconnecting)
            {
                LeaveRoom();
            }
        }

        // Private methods
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
            if (_state == SessionState.InRoom && connectionId == 0)
            {
                SetState(SessionState.Disconnected);
            }
        }
    }
}
