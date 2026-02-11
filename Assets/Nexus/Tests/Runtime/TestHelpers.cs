using System;
using System.Collections;
using System.Reflection;
using Nexus.Networking.Core;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public static class TestHelpers
    {
        /// <summary>
        /// Yields until condition is true or timeout expires.
        /// Fails the test on timeout.
        /// </summary>
        public static IEnumerator WaitForCondition(
            Func<bool> condition,
            float timeout = 5f,
            string message = null)
        {
            float elapsed = 0f;
            while (!condition())
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed > timeout)
                {
                    Assert.Fail(message ?? $"Condition not met within {timeout}s.");
                }

                yield return null;
            }
        }

        /// <summary>
        /// Yields for specified seconds using unscaled time.
        /// </summary>
        public static IEnumerator WaitForSeconds(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        /// <summary>
        /// Creates a NexusConfig with test-friendly defaults using reflection
        /// to set private serialized fields.
        /// </summary>
        public static NexusConfig CreateConfig(
            NexusMode mode = NexusMode.Local,
            int port = 7777,
            int maxPlayers = 8,
            int discoveryPort = 0,
            float broadcastInterval = 0.5f,
            float roomTimeoutSeconds = 2f,
            int maxReconnectAttempts = 2,
            float reconnectBaseDelay = 0.2f)
        {
            // Randomize discovery port to avoid conflicts between test runs
            if (discoveryPort == 0)
            {
                discoveryPort = UnityEngine.Random.Range(48000, 49000);
            }

            var config = ScriptableObject.CreateInstance<NexusConfig>();
            SetPrivateField(config, "_mode", mode);
            SetPrivateField(config, "_port", port);
            SetPrivateField(config, "_maxPlayers", maxPlayers);
            SetPrivateField(config, "_discoveryPort", discoveryPort);
            SetPrivateField(config, "_broadcastInterval", broadcastInterval);
            SetPrivateField(config, "_roomTimeoutSeconds", roomTimeoutSeconds);
            SetPrivateField(config, "_maxReconnectAttempts", maxReconnectAttempts);
            SetPrivateField(config, "_reconnectBaseDelay", reconnectBaseDelay);
            return config;
        }

        /// <summary>
        /// Sets a private field on an object via reflection.
        /// </summary>
        public static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
