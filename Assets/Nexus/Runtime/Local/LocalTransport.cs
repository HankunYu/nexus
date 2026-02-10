using System;
using Mirror;
using Nexus.Networking.Core;
using UnityEngine;
using NetworkMode = Nexus.Networking.Core.NetworkMode;

namespace Nexus.Networking.Local
{
    /// <summary>
    /// Local (LAN) transport implementation wrapping Mirror NetworkManager + KCP Transport.
    /// Mirror internals are fully encapsulated — consumers only interact via INexusTransport.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class LocalTransport : MonoBehaviour, INexusTransport
    {
        // Private fields
        private NetworkManager _networkManager;
        private NetworkMode _mode = NetworkMode.None;

        // Public properties
        public bool IsActive => _networkManager != null && _networkManager.isNetworkActive;
        public NetworkMode Mode => _mode;

        // Events
        public event Action OnStarted;
        public event Action OnStopped;
        public event Action<int> OnClientConnected;
        public event Action<int> OnClientDisconnected;

        // Unity callbacks
        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()
        {
            NetworkServer.OnConnectedEvent += HandleServerConnected;
            NetworkServer.OnDisconnectedEvent += HandleServerDisconnected;
        }

        private void OnDisable()
        {
            NetworkServer.OnConnectedEvent -= HandleServerConnected;
            NetworkServer.OnDisconnectedEvent -= HandleServerDisconnected;
        }

        // Public methods
        public void StartHost(int port)
        {
            if (IsActive)
            {
                Debug.LogWarning("[LocalTransport] Already active. Stop first before restarting.");
                return;
            }

            ConfigureTransport(port);
            _networkManager.StartHost();
            _mode = NetworkMode.Host;

            Debug.Log($"[LocalTransport] Host started on port {port}.");
            OnStarted?.Invoke();
        }

        public void StartClient(string address, int port)
        {
            if (IsActive)
            {
                Debug.LogWarning("[LocalTransport] Already active. Stop first before restarting.");
                return;
            }

            ConfigureTransport(port);
            _networkManager.networkAddress = address;
            _networkManager.StartClient();
            _mode = NetworkMode.Client;

            Debug.Log($"[LocalTransport] Client connecting to {address}:{port}.");
            OnStarted?.Invoke();
        }

        public void StartServer(int port)
        {
            if (IsActive)
            {
                Debug.LogWarning("[LocalTransport] Already active. Stop first before restarting.");
                return;
            }

            ConfigureTransport(port);
            _networkManager.StartServer();
            _mode = NetworkMode.Server;

            Debug.Log($"[LocalTransport] Server started on port {port}.");
            OnStarted?.Invoke();
        }

        public void Stop()
        {
            if (!IsActive)
            {
                return;
            }

            switch (_mode)
            {
                case NetworkMode.Host:
                    _networkManager.StopHost();
                    break;
                case NetworkMode.Client:
                    _networkManager.StopClient();
                    break;
                case NetworkMode.Server:
                    _networkManager.StopServer();
                    break;
            }

            _mode = NetworkMode.None;

            Debug.Log("[LocalTransport] Stopped.");
            OnStopped?.Invoke();
        }

        // Private methods
        private void ConfigureTransport(int port)
        {
            if (Transport.active is kcp2k.KcpTransport kcpTransport)
            {
                kcpTransport.port = (ushort)port;
            }
            else
            {
                Debug.LogWarning("[LocalTransport] Active transport is not KcpTransport. Port configuration skipped.");
            }
        }

        private void HandleServerConnected(NetworkConnectionToClient conn)
        {
            Debug.Log($"[LocalTransport] Client connected: {conn.connectionId}");
            OnClientConnected?.Invoke(conn.connectionId);
        }

        private void HandleServerDisconnected(NetworkConnectionToClient conn)
        {
            Debug.Log($"[LocalTransport] Client disconnected: {conn.connectionId}");
            OnClientDisconnected?.Invoke(conn.connectionId);
        }
    }
}
