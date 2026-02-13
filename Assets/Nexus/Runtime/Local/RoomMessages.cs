using Mirror;

namespace Nexus.Networking.Local
{
    /// <summary>
    /// Wire-format for a single player entry in the player list.
    /// </summary>
    public struct PlayerInfoEntry
    {
        public int ConnectionId;
        public string PlayerId;
        public string DisplayName;
        public bool IsHost;
    }

    /// <summary>
    /// Broadcast from server to all clients whenever the player list changes.
    /// </summary>
    public struct PlayerListMessage : NetworkMessage
    {
        public PlayerInfoEntry[] Players;
    }
}
