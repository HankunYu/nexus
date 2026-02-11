using Nexus.Networking.Local;
using Nexus.Networking.VR;
using Nexus.Networking.VR.Calibration;
using UnityEngine;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// Auto-wiring component. Creates Local/Remote implementations based on
    /// NexusConfig.Mode and initializes NexusSession.
    /// Attach to the same GameObject as NexusSession.
    /// </summary>
    [RequireComponent(typeof(NexusSession))]
    public class NexusBootstrap : MonoBehaviour
    {
        private void Start()
        {
            var session = GetComponent<NexusSession>();

            if (session.Config == null)
            {
                Debug.LogError("[NexusBootstrap] NexusConfig is not assigned on NexusSession.");
                return;
            }

            switch (session.Config.Mode)
            {
                case NexusMode.Local:
                    InitializeLocal(session);
                    break;
                case NexusMode.Remote:
                    Debug.LogWarning("[NexusBootstrap] Remote mode not yet implemented. Falling back to Local.");
                    InitializeLocal(session);
                    break;
            }
        }

        private void InitializeLocal(NexusSession session)
        {
            NexusConfig config = session.Config;

            // Ensure Mirror NetworkManager exists
            if (!TryGetComponent<Mirror.NetworkManager>(out _))
            {
                gameObject.AddComponent<Mirror.NetworkManager>();
            }

            // Ensure KcpTransport exists
            if (!TryGetComponent<kcp2k.KcpTransport>(out var kcpTransport))
            {
                kcpTransport = gameObject.AddComponent<kcp2k.KcpTransport>();
            }

            // Set KCP as active transport
            Mirror.Transport.active = kcpTransport;

            // Create Local implementations
            var transport = GetOrAddComponent<LocalTransport>();
            var discovery = GetOrAddComponent<LanDiscovery>();
            var roomManager = GetOrAddComponent<LocalRoomManager>();
            var reconnectHandler = GetOrAddComponent<ReconnectHandler>();

            // Configure
            discovery.Configure(config.DiscoveryPort, config.RoomTimeoutSeconds);
            roomManager.Initialize(transport, config);
            reconnectHandler.Initialize(transport, roomManager, config);

            // Wire reconnect events to session state
            reconnectHandler.OnReconnectStarted += session.SetReconnecting;
            reconnectHandler.OnReconnectSucceeded += session.SetReconnected;
            reconnectHandler.OnReconnectFailed += session.SetReconnectFailed;

            // Wire into session
            session.Initialize(transport, discovery, roomManager);

            // Wire VR player manager
            var vrPlayerManager = GetOrAddComponent<NexusVRPlayerManager>();
            vrPlayerManager.Initialize(roomManager, config);

            // Wire spatial calibration manager
            var calibrationManager = GetOrAddComponent<SpatialCalibrationManager>();
            var manualCalibration = new ManualCalibrationProvider();
            calibrationManager.Initialize(manualCalibration, vrPlayerManager);

            Debug.Log("[NexusBootstrap] Local mode initialized.");
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            if (!TryGetComponent<T>(out var component))
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }
    }
}
