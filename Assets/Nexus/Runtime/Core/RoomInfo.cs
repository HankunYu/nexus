using System.Collections.Generic;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Describes a discoverable room on the network.
    /// </summary>
    public class RoomInfo
    {
        public string RoomId { get; set; }
        public string RoomName { get; set; }
        public string HostAddress { get; set; }
        public int Port { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public long LastSeenTimestamp { get; set; }
    }
}
