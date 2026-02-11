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
        public void CreateRoom(RoomConfig config);

        /// <summary>
        /// Join an existing room.
        /// </summary>
        public void JoinRoom(RoomInfo room);

        /// <summary>
        /// Leave the current room.
        /// </summary>
        public void LeaveRoom();

        public RoomState CurrentState { get; }
        public RoomInfo CurrentRoom { get; }
        public IReadOnlyList<NexusPlayer> Players { get; }

        public event Action<NexusPlayer> OnPlayerJoined;
        public event Action<NexusPlayer> OnPlayerLeft;
        public event Action<RoomInfo> OnRoomCreated;
        public event Action OnRoomJoined;
        public event Action OnRoomLeft;
    }
}
