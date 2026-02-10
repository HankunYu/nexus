namespace Nexus.Networking.Core
{
    /// <summary>
    /// Current state of the room lifecycle.
    /// </summary>
    public enum RoomState
    {
        Idle,
        Creating,
        Joining,
        InLobby,
        InRoom,
        Disconnected,
        Reconnecting
    }
}
