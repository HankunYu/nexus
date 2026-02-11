using System.Collections;
using System.Reflection;
using Mirror;
using Nexus.Networking.Core;
using Nexus.Networking.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// Base class for Nexus Play Mode tests.
    /// Sets up a complete local networking stack per test.
    /// </summary>
    public abstract class NexusTestBase
    {
        protected GameObject _rootObject;
        protected NexusSession _session;
        protected NexusConfig _config;
        protected LocalTransport _localTransport;
        protected LanDiscovery _discovery;
        protected LocalRoomManager _roomManager;
        protected ReconnectHandler _reconnect;
        protected NetworkManager _mirrorManager;

        [UnitySetUp]
        public IEnumerator BaseSetUp()
        {
            // Clean any leftover singleton state
            if (NexusSession.Instance != null)
            {
                Object.DestroyImmediate(NexusSession.Instance.gameObject);
                SetStaticProperty<NexusSession>("Instance", null);
            }

            if (NetworkManager.singleton != null)
            {
                Object.DestroyImmediate(NetworkManager.singleton.gameObject);
            }

            Transport.active = null;

            // Wait a frame for cleanup
            yield return null;

            // Create config
            _config = CreateTestConfig();

            // Create root GameObject with all components
            _rootObject = new GameObject("NexusTestRoot");

            // Add Mirror components first
            _mirrorManager = _rootObject.AddComponent<NetworkManager>();
            var kcpTransport = _rootObject.AddComponent<kcp2k.KcpTransport>();
            Transport.active = kcpTransport;

            // Add Nexus components
            _session = _rootObject.AddComponent<NexusSession>();

            // Set _config via reflection (private serialized field)
            TestHelpers.SetPrivateField(_session, "_config", _config);

            _localTransport = _rootObject.AddComponent<LocalTransport>();
            _discovery = _rootObject.AddComponent<LanDiscovery>();
            _roomManager = _rootObject.AddComponent<LocalRoomManager>();
            _reconnect = _rootObject.AddComponent<ReconnectHandler>();

            // Wait for Awake to run
            yield return null;

            // Wire up (same as NexusBootstrap.InitializeLocal)
            _discovery.Configure(_config.DiscoveryPort, _config.RoomTimeoutSeconds);
            _roomManager.Initialize(_localTransport, _config);
            _reconnect.Initialize(_localTransport, _roomManager, _config);

            _reconnect.OnReconnectStarted += _session.SetReconnecting;
            _reconnect.OnReconnectSucceeded += _session.SetReconnected;
            _reconnect.OnReconnectFailed += _session.SetReconnectFailed;

            _session.Initialize(_localTransport, _discovery, _roomManager);
        }

        [UnityTearDown]
        public IEnumerator BaseTearDown()
        {
            // Stop networking
            if (_session != null)
            {
                _session.Shutdown();
            }

            // Mirror cleanup
            if (NetworkServer.active)
            {
                NetworkServer.Shutdown();
            }

            if (NetworkClient.active)
            {
                NetworkClient.Shutdown();
            }

            // Destroy test objects
            if (_rootObject != null)
            {
                Object.DestroyImmediate(_rootObject);
            }

            Transport.active = null;

            // Wait for cleanup
            yield return null;
        }

        /// <summary>
        /// Override to customize config for specific test fixtures.
        /// </summary>
        protected virtual NexusConfig CreateTestConfig()
        {
            return TestHelpers.CreateConfig();
        }

        /// <summary>
        /// Sets a static property via reflection.
        /// </summary>
        private static void SetStaticProperty<T>(string propertyName, object value)
        {
            var property = typeof(T).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);

            if (property != null && property.CanWrite)
            {
                property.SetValue(null, value);
            }
            else
            {
                // Fall back to backing field
                var field = typeof(T).GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Static | BindingFlags.NonPublic);

                field?.SetValue(null, value);
            }
        }
    }
}
