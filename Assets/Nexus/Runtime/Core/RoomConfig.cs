using System.Collections.Generic;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Configuration for creating a new room.
    /// </summary>
    public class RoomConfig
    {
        public string RoomName { get; set; } = "Nexus Room";
        public int MaxPlayers { get; set; } = 8;
        public int Port { get; set; } = 7777;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
