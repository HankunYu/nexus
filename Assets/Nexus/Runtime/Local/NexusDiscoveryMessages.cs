using Mirror;

namespace Nexus.Networking.Local
{
    // Empty request — client just pings for servers
    public struct NexusDiscoveryRequest : NetworkMessage { }

    // Response carrying room information
    public struct NexusDiscoveryResponse : NetworkMessage
    {
        public long ServerId;
        public string RoomId;
        public string RoomName;
        public int Port;
        public int CurrentPlayers;
        public int MaxPlayers;
    }
}
