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
        public void StartHost(int port);

        /// <summary>
        /// Connect to an existing host as a client.
        /// </summary>
        public void StartClient(string address, int port);

        /// <summary>
        /// Start as a dedicated server (no local client).
        /// </summary>
        public void StartServer(int port);

        /// <summary>
        /// Stop all networking activity and clean up.
        /// </summary>
        public void Stop();

        public bool IsActive { get; }
        public NetworkMode Mode { get; }

        public event Action OnStarted;
        public event Action OnStopped;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;
    }
}
