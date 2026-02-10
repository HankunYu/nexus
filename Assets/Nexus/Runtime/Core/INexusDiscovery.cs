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
        void StartBroadcast(RoomInfo room);

        /// <summary>
        /// Begin listening for available rooms.
        /// </summary>
        void StartListening();

        /// <summary>
        /// Stop broadcasting or listening.
        /// </summary>
        void Stop();

        bool IsActive { get; }

        /// <summary>
        /// Fired when a new room is discovered or an existing room's info is updated.
        /// </summary>
        event Action<RoomInfo> OnRoomFound;

        /// <summary>
        /// Fired when a previously discovered room is no longer reachable.
        /// </summary>
        event Action<RoomInfo> OnRoomLost;
    }
}
