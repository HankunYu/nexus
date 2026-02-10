using System;
using System.Collections.Generic;
using System.Net;
using Mirror.Discovery;
using Nexus.Networking.Core;
using UnityEngine;

namespace Nexus.Networking.Local
{
    /// <summary>
    /// LAN room discovery using Mirror's UDP broadcast.
    /// Wraps NetworkDiscoveryBase to implement INexusDiscovery.
    /// </summary>
    public class LanDiscovery : NetworkDiscoveryBase<NexusDiscoveryRequest, NexusDiscoveryResponse>,
        INexusDiscovery
    {
        // Private fields
        private readonly Dictionary<string, RoomInfo> _discoveredRooms = new Dictionary<string, RoomInfo>();
        private RoomInfo _broadcastRoom;
        private float _roomTimeout = 5f;
        private bool _isListening;

        // Public properties
        public bool IsActive => serverUdpClient != null || _isListening;
        public IReadOnlyDictionary<string, RoomInfo> DiscoveredRooms => _discoveredRooms;

        // Events
        public event Action<RoomInfo> OnRoomFound;
        public event Action<RoomInfo> OnRoomLost;

        // Unity callbacks
        private void Update()
        {
            if (!_isListening)
            {
                return;
            }

            CleanupTimedOutRooms();
        }

        // Public methods
        /// <summary>
        /// Configure discovery parameters. Call before StartBroadcast/StartListening.
        /// Port is set via protected base field; interval is set via inspector.
        /// </summary>
        public void Configure(int discoveryPort, float roomTimeout)
        {
            serverBroadcastListenPort = discoveryPort;
            _roomTimeout = roomTimeout;
        }

        public void StartBroadcast(RoomInfo room)
        {
            _broadcastRoom = room;
            AdvertiseServer();
            Debug.Log($"[LanDiscovery] Broadcasting room: {room.RoomName}");
        }

        public void StartListening()
        {
            _discoveredRooms.Clear();
            _isListening = true;
            StartDiscovery();
            Debug.Log("[LanDiscovery] Listening for rooms...");
        }

        public void Stop()
        {
            _isListening = false;
            _broadcastRoom = null;
            StopDiscovery();
            _discoveredRooms.Clear();
            Debug.Log("[LanDiscovery] Stopped.");
        }

        // Protected overrides (Mirror)
        protected override NexusDiscoveryResponse ProcessRequest(
            NexusDiscoveryRequest request,
            IPEndPoint endpoint)
        {
            if (_broadcastRoom == null)
            {
                return default;
            }

            return new NexusDiscoveryResponse
            {
                ServerId = ServerId,
                RoomId = _broadcastRoom.RoomId,
                RoomName = _broadcastRoom.RoomName,
                Port = _broadcastRoom.Port,
                CurrentPlayers = _broadcastRoom.CurrentPlayers,
                MaxPlayers = _broadcastRoom.MaxPlayers
            };
        }

        protected override void ProcessResponse(
            NexusDiscoveryResponse response,
            IPEndPoint endpoint)
        {
            string roomId = response.RoomId;
            long now = DateTime.UtcNow.Ticks;

            if (_discoveredRooms.TryGetValue(roomId, out RoomInfo existing))
            {
                existing.RoomName = response.RoomName;
                existing.CurrentPlayers = response.CurrentPlayers;
                existing.MaxPlayers = response.MaxPlayers;
                existing.Port = response.Port;
                existing.HostAddress = endpoint.Address.ToString();
                existing.LastSeenTimestamp = now;
            }
            else
            {
                var room = new RoomInfo
                {
                    RoomId = roomId,
                    RoomName = response.RoomName,
                    HostAddress = endpoint.Address.ToString(),
                    Port = response.Port,
                    CurrentPlayers = response.CurrentPlayers,
                    MaxPlayers = response.MaxPlayers,
                    LastSeenTimestamp = now
                };

                _discoveredRooms[roomId] = room;
                Debug.Log($"[LanDiscovery] Room found: {room.RoomName} at {room.HostAddress}:{room.Port}");
                OnRoomFound?.Invoke(room);
            }
        }

        // Private methods
        private void CleanupTimedOutRooms()
        {
            long now = DateTime.UtcNow.Ticks;
            long timeoutTicks = (long)(_roomTimeout * TimeSpan.TicksPerSecond);
            List<string> timedOut = null;

            foreach (var kvp in _discoveredRooms)
            {
                if (now - kvp.Value.LastSeenTimestamp > timeoutTicks)
                {
                    timedOut ??= new List<string>();
                    timedOut.Add(kvp.Key);
                }
            }

            if (timedOut == null)
            {
                return;
            }

            foreach (string roomId in timedOut)
            {
                RoomInfo room = _discoveredRooms[roomId];
                _discoveredRooms.Remove(roomId);
                Debug.Log($"[LanDiscovery] Room lost: {room.RoomName}");
                OnRoomLost?.Invoke(room);
            }
        }
    }
}
