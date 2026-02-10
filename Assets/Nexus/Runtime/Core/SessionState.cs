namespace Nexus.Networking.Core
{
    /// <summary>
    /// State machine states for NexusSession.
    /// </summary>
    public enum SessionState
    {
        Idle,
        Creating,
        Hosting,
        Joining,
        Connected,
        InRoom,
        Disconnected,
        Reconnecting
    }
}
