using System;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Abstraction over the underlying network transport (Mirror/KCP, SimpleWebTransport, etc.).
    /// Implementations handle starting/stopping host, client, or server roles.
    /// </summary>
    public interface INexusTransport
    {
        /// <summary>
        /// Start as host (server + local client).
        /// </summary>
        void StartHost(int port);

        /// <summary>
        /// Connect to an existing host as a client.
        /// </summary>
        void StartClient(string address, int port);

        /// <summary>
        /// Start as a dedicated server (no local client).
        /// </summary>
        void StartServer(int port);

        /// <summary>
        /// Stop all networking activity and clean up.
        /// </summary>
        void Stop();

        bool IsActive { get; }
        NetworkMode Mode { get; }

        event Action OnStarted;
        event Action OnStopped;
        event Action<int> OnClientConnected;
        event Action<int> OnClientDisconnected;
    }
}
