using Mirror;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Optional interface for syncing custom per-player state (grab state, tool type, etc.).
    /// Uses Mirror NetworkWriter/NetworkReader for optimal serialization.
    /// </summary>
    public interface IVRCustomState
    {
        public void Serialize(NetworkWriter writer);
        public void Deserialize(NetworkReader reader);
    }
}
