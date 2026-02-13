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
    /// Sent from server to each client whenever the player list changes.
    /// Each client receives its own connectionId in LocalConnectionId.
    /// </summary>
    public struct PlayerListMessage : NetworkMessage
    {
        public int LocalConnectionId;
        public PlayerInfoEntry[] Players;
    }
}
