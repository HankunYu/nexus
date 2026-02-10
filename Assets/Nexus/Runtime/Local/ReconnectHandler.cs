using System;
using System.Collections;
using Nexus.Networking.Core;
using UnityEngine;

namespace Nexus.Networking.Local
{
    /// <summary>
    /// Monitors transport connection and attempts reconnection
    /// with exponential backoff when disconnected unexpectedly.
    /// </summary>
    public class ReconnectHandler : MonoBehaviour
    {
        // Private fields
        private INexusTransport _transport;
        private INexusRoomManager _roomManager;
        private NexusConfig _config;
        private RoomInfo _lastRoom;
        private int _attemptCount;
        private bool _isReconnecting;
        private Coroutine _reconnectCoroutine;

        // Public properties
        public bool IsReconnecting => _isReconnecting;
        public int AttemptCount => _attemptCount;

        // Events
        public event Action OnReconnectStarted;
        public event Action OnReconnectSucceeded;
        public event Action OnReconnectFailed;

        // Unity callbacks
        private void OnDestroy()
        {
            if (_transport != null)
            {
                _transport.OnStopped -= HandleTransportStopped;
                _transport.OnStarted -= HandleTransportStarted;
            }

            StopReconnect();
        }

        // Public methods
        public void Initialize(INexusTransport transport, INexusRoomManager roomManager, NexusConfig config)
        {
            _transport = transport;
            _roomManager = roomManager;
            _config = config;

            _transport.OnStopped += HandleTransportStopped;
            _transport.OnStarted += HandleTransportStarted;
        }

        // Private methods
        private void HandleTransportStopped()
        {
            if (_roomManager.CurrentState != RoomState.InRoom)
            {
                return;
            }

            if (_roomManager.CurrentRoom == null)
            {
                return;
            }

            // Host doesn't reconnect — only clients do
            if (_transport.Mode == NetworkMode.Host || _transport.Mode == NetworkMode.Server)
            {
                return;
            }

            _lastRoom = _roomManager.CurrentRoom;
            StartReconnect();
        }

        private void HandleTransportStarted()
        {
            if (!_isReconnecting)
            {
                return;
            }

            _isReconnecting = false;
            _attemptCount = 0;

            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }

            Debug.Log("[ReconnectHandler] Reconnected successfully.");
            OnReconnectSucceeded?.Invoke();
        }

        private void StartReconnect()
        {
            if (_isReconnecting)
            {
                return;
            }

            _isReconnecting = true;
            _attemptCount = 0;

            Debug.Log("[ReconnectHandler] Connection lost. Starting reconnection...");
            OnReconnectStarted?.Invoke();

            _reconnectCoroutine = StartCoroutine(ReconnectRoutine());
        }

        private void StopReconnect()
        {
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }

            _isReconnecting = false;
            _attemptCount = 0;
        }

        private IEnumerator ReconnectRoutine()
        {
            while (_attemptCount < _config.MaxReconnectAttempts)
            {
                _attemptCount++;

                // Exponential backoff: baseDelay * 2^(attempt-1)
                float delay = _config.ReconnectBaseDelay * Mathf.Pow(2f, _attemptCount - 1);
                Debug.Log($"[ReconnectHandler] Attempt {_attemptCount}/{_config.MaxReconnectAttempts} in {delay:F1}s...");

                yield return new WaitForSeconds(delay);

                if (!_isReconnecting || _lastRoom == null)
                {
                    yield break;
                }

                _transport.StartClient(_lastRoom.HostAddress, _lastRoom.Port);

                // Wait a bit to see if connection succeeds
                yield return new WaitForSeconds(2f);

                if (!_isReconnecting)
                {
                    yield break;
                }
            }

            _isReconnecting = false;
            Debug.LogWarning("[ReconnectHandler] Reconnection failed after all attempts.");
            OnReconnectFailed?.Invoke();
        }
    }
}
