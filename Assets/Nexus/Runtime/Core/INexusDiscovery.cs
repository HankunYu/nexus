using System;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Abstraction for network room discovery.
    /// Local implementation uses UDP broadcast; remote uses HTTP API polling.
    /// </summary>
    public interface INexusDiscovery
    {
        /// <summary>
        /// Begin advertising a room on the network.
        /// </summary>
        public void StartBroadcast(RoomInfo room);

        /// <summary>
        /// Begin listening for available rooms.
        /// </summary>
        public void StartListening();

        /// <summary>
        /// Stop broadcasting or listening.
        /// </summary>
        public void Stop();

        public bool IsActive { get; }

        /// <summary>
        /// Fired when a new room is discovered or an existing room's info is updated.
        /// </summary>
        public event Action<RoomInfo> OnRoomFound;

        /// <summary>
        /// Fired when a previously discovered room is no longer reachable.
        /// </summary>
        public event Action<RoomInfo> OnRoomLost;
    }
}
