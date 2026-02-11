using Mirror;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Optional interface for syncing custom per-player state (grab state, tool type, etc.).
    /// Uses Mirror NetworkWriter/NetworkReader for optimal serialization.
    /// </summary>
    public interface IVRCustomState
    {
        void Serialize(NetworkWriter writer);
        void Deserialize(NetworkReader reader);
    }
}
