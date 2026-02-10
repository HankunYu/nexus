using System;
using System.Collections.Generic;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Manages room lifecycle: creation, joining, leaving, and player tracking.
    /// </summary>
    public interface INexusRoomManager
    {
        /// <summary>
        /// Create and host a new room.
        /// </summary>
        void CreateRoom(RoomConfig config);

        /// <summary>
        /// Join an existing room.
        /// </summary>
        void JoinRoom(RoomInfo room);

        /// <summary>
        /// Leave the current room.
        /// </summary>
        void LeaveRoom();

        RoomState CurrentState { get; }
        RoomInfo CurrentRoom { get; }
        IReadOnlyList<NexusPlayer> Players { get; }

        event Action<NexusPlayer> OnPlayerJoined;
        event Action<NexusPlayer> OnPlayerLeft;
        event Action<RoomInfo> OnRoomCreated;
        event Action OnRoomJoined;
        event Action OnRoomLeft;
    }
}
