namespace Nexus.Networking.Core
{
    /// <summary>
    /// Represents a player in the network session.
    /// </summary>
    public class NexusPlayer
    {
        public int ConnectionId { get; set; }
        public string PlayerId { get; set; }
        public string DisplayName { get; set; }
        public bool IsHost { get; set; }
        public bool IsLocal { get; set; }
    }
}
