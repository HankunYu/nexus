using System.Collections.Generic;
using Nexus.Networking.Core;
using Nexus.Networking.VR;
using UnityEngine;

namespace Nexus.Networking.Debugging
{
    /// <summary>
    /// Inspector-driven debug panel for Nexus networking.
    /// Provides buttons for room creation, discovery, joining, and leaving.
    /// Shows connected players and session state in real time.
    /// </summary>
    public class NexusDebugPanel : MonoBehaviour
    {
        [SerializeField] private string _roomName = "Nexus Room";

        // Private fields
        private readonly List<RoomInfo> _discoveredRooms = new List<RoomInfo>();
        private bool _isSubscribed;

        // Public properties
        public NexusSession Session => NexusSession.Instance;
        public SessionState CurrentState => Session != null ? Session.State : SessionState.Idle;
        public IReadOnlyList<RoomInfo> DiscoveredRooms => _discoveredRooms;
        public string RoomName => _roomName;

        public INexusRoomManager RoomManager => Session?.RoomManager;
        public IReadOnlyList<NexusPlayer> Players => RoomManager?.Players;
        public RoomInfo CurrentRoom => RoomManager?.CurrentRoom;

        public NexusVRPlayerManager VRPlayerManager =>
            Session != null ? Session.GetComponent<NexusVRPlayerManager>() : null;

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_isSubscribed || Session?.Discovery == null)
            {
                return;
            }

            Session.Discovery.OnRoomFound += HandleRoomFound;
            Session.Discovery.OnRoomLost += HandleRoomLost;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed || Session?.Discovery == null)
            {
                return;
            }

            Session.Discovery.OnRoomFound -= HandleRoomFound;
            Session.Discovery.OnRoomLost -= HandleRoomLost;
            _isSubscribed = false;
        }

        public void CreateRoom()
        {
            if (Session == null)
            {
                Debug.LogWarning("[NexusDebugPanel] NexusSession not found.");
                return;
            }

            Subscribe();
            Session.CreateRoom(_roomName);
            Debug.Log($"[NexusDebugPanel] CreateRoom: {_roomName}");
        }

        public void StartDiscovery()
        {
            if (Session == null)
            {
                Debug.LogWarning("[NexusDebugPanel] NexusSession not found.");
                return;
            }

            Subscribe();
            _discoveredRooms.Clear();
            Session.StartDiscovery();
            Debug.Log("[NexusDebugPanel] Discovery started.");
        }

        public void StopDiscovery()
        {
            if (Session == null)
            {
                return;
            }

            Session.StopDiscovery();
            Debug.Log("[NexusDebugPanel] Discovery stopped.");
        }

        public void JoinRoom(int index)
        {
            if (Session == null)
            {
                Debug.LogWarning("[NexusDebugPanel] NexusSession not found.");
                return;
            }

            if (index < 0 || index >= _discoveredRooms.Count)
            {
                Debug.LogWarning($"[NexusDebugPanel] Invalid room index: {index}");
                return;
            }

            RoomInfo room = _discoveredRooms[index];
            Session.JoinRoom(room);
            Debug.Log($"[NexusDebugPanel] Joining room: {room.RoomName} @ {room.HostAddress}:{room.Port}");
        }

        public void LeaveRoom()
        {
            if (Session == null)
            {
                return;
            }

            Session.LeaveRoom();
            _discoveredRooms.Clear();
            Debug.Log("[NexusDebugPanel] Left room.");
        }

        private void HandleRoomFound(RoomInfo room)
        {
            // Update existing or add new
            for (int i = 0; i < _discoveredRooms.Count; i++)
            {
                if (_discoveredRooms[i].RoomId == room.RoomId)
                {
                    _discoveredRooms[i] = room;
                    return;
                }
            }

            _discoveredRooms.Add(room);
            Debug.Log($"[NexusDebugPanel] Room found: {room.RoomName} ({room.CurrentPlayers}/{room.MaxPlayers})");
        }

        private void HandleRoomLost(RoomInfo room)
        {
            _discoveredRooms.RemoveAll(r => r.RoomId == room.RoomId);
            Debug.Log($"[NexusDebugPanel] Room lost: {room.RoomName}");
        }
    }
}
