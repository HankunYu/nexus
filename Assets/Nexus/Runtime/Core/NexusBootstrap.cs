using Mirror;
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
        [Header("Calibration")]
        [Tooltip("Optional: assign a MonoBehaviour implementing ICalibrationProvider (e.g. MetaSpatialAnchorProvider). If empty, defaults to ManualCalibrationProvider.")]
        [SerializeField] private MonoBehaviour _calibrationProviderOverride;

        [Header("VR Tracking")]
        [Tooltip("Optional: assign a MonoBehaviour implementing IVRTrackingProvider (e.g. MetaVRTrackingProvider). If empty, no local tracking.")]
        [SerializeField] private MonoBehaviour _trackingProviderOverride;

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

            // Ensure KcpTransport exists BEFORE NetworkManager (Awake checks transport)
            if (!TryGetComponent<kcp2k.KcpTransport>(out var kcpTransport))
            {
                kcpTransport = gameObject.AddComponent<kcp2k.KcpTransport>();
            }

            Mirror.Transport.active = kcpTransport;

            // Ensure Mirror NetworkManager exists
            if (!TryGetComponent<Mirror.NetworkManager>(out var networkManager))
            {
                networkManager = gameObject.AddComponent<Mirror.NetworkManager>();
            }

            // Assign transport and disable auto player spawn
            networkManager.transport = kcpTransport;
            networkManager.autoCreatePlayer = false;

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
            vrPlayerManager.TrackingProvider = ResolveTrackingProvider();
            var playerTemplate = CreateVRPlayerTemplate();
            vrPlayerManager.Initialize(roomManager, config, playerTemplate);

            // Wire spatial calibration manager
            var calibrationManager = GetOrAddComponent<SpatialCalibrationManager>();
            ICalibrationProvider calibrationProvider = ResolveCalibrationProvider();
            calibrationManager.Initialize(calibrationProvider, vrPlayerManager);

            Debug.Log("[NexusBootstrap] Local mode initialized.");
        }

        private static GameObject CreateVRPlayerTemplate()
        {
            var template = new GameObject("NexusVRPlayerTemplate");
            template.SetActive(false);
            template.AddComponent<NetworkIdentity>();
            template.AddComponent<NexusVRPlayer>();
            template.hideFlags = HideFlags.HideAndDontSave;
            return template;
        }

        private IVRTrackingProvider ResolveTrackingProvider()
        {
            if (_trackingProviderOverride != null)
            {
                if (_trackingProviderOverride is IVRTrackingProvider provider)
                {
                    Debug.Log($"[NexusBootstrap] Using tracking provider: {_trackingProviderOverride.GetType().Name}");
                    return provider;
                }

                Debug.LogWarning($"[NexusBootstrap] {_trackingProviderOverride.GetType().Name} does not implement IVRTrackingProvider. No local tracking.");
            }

            return null;
        }

        private ICalibrationProvider ResolveCalibrationProvider()
        {
            if (_calibrationProviderOverride != null)
            {
                if (_calibrationProviderOverride is ICalibrationProvider provider)
                {
                    Debug.Log($"[NexusBootstrap] Using calibration provider: {_calibrationProviderOverride.GetType().Name}");
                    return provider;
                }

                Debug.LogWarning($"[NexusBootstrap] {_calibrationProviderOverride.GetType().Name} does not implement ICalibrationProvider. Falling back to ManualCalibrationProvider.");
            }

            return new ManualCalibrationProvider();
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
