using UnityEngine;

namespace Nexus.Networking.Core
{
    /// <summary>
    /// ScriptableObject holding Nexus networking configuration.
    /// Create via Assets > Create > Nexus > Network Config.
    /// </summary>
    [CreateAssetMenu(fileName = "NexusConfig", menuName = "Nexus/Network Config")]
    public class NexusConfig : ScriptableObject
    {
        [Header("General")]
        [SerializeField] private NexusMode _mode = NexusMode.Local;
        [SerializeField] private int _port = 7777;
        [SerializeField] private int _maxPlayers = 8;

        [Header("Discovery")]
        [SerializeField] private int _discoveryPort = 47777;
        [SerializeField] private float _broadcastInterval = 1f;
        [SerializeField] private float _roomTimeoutSeconds = 5f;

        [Header("Reconnection")]
        [SerializeField] private int _maxReconnectAttempts = 3;
        [SerializeField] private float _reconnectBaseDelay = 2f;

        [Header("VR Sync")]
        [SerializeField] private int _syncRateHz = 30;

        [Header("Remote")]
        [SerializeField] private string _relayServerUrl = "";

        public NexusMode Mode => _mode;
        public int Port => _port;
        public int MaxPlayers => _maxPlayers;
        public int DiscoveryPort => _discoveryPort;
        public float BroadcastInterval => _broadcastInterval;
        public float RoomTimeoutSeconds => _roomTimeoutSeconds;
        public int MaxReconnectAttempts => _maxReconnectAttempts;
        public float ReconnectBaseDelay => _reconnectBaseDelay;
        public int SyncRateHz => _syncRateHz;
        public string RelayServerUrl => _relayServerUrl;
    }
}
