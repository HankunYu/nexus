using System.Collections;
using Nexus.Networking.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// Play Mode tests for ReconnectHandler guard logic.
    /// Verifies that reconnection only activates for clients in a room,
    /// and that host/idle/shutdown/leave scenarios do not trigger reconnection.
    /// </summary>
    public class ReconnectTests : NexusTestBase
    {
        // Private fields
        private bool _reconnectStarted;
        private bool _reconnectSucceeded;
        private bool _reconnectFailed;

        protected override NexusConfig CreateTestConfig()
        {
            return TestHelpers.CreateConfig(
                maxReconnectAttempts: 2,
                reconnectBaseDelay: 0.2f);
        }

        [SetUp]
        public void SetUp()
        {
            _reconnectStarted = false;
            _reconnectSucceeded = false;
            _reconnectFailed = false;

            _reconnect.OnReconnectStarted += () => _reconnectStarted = true;
            _reconnect.OnReconnectSucceeded += () => _reconnectSucceeded = true;
            _reconnect.OnReconnectFailed += () => _reconnectFailed = true;
        }

        [UnityTest]
        public IEnumerator Disconnect_ShouldTransitionToDisconnected()
        {
            _session.CreateRoom("Disconnect Test");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            _localTransport.Stop();

            yield return null;

            Assert.IsFalse(_reconnect.IsReconnecting,
                "ReconnectHandler should not activate for Host mode.");
            Assert.IsFalse(_reconnectStarted,
                "OnReconnectStarted should not fire for Host mode.");
            Assert.AreNotEqual(SessionState.Reconnecting, _session.State,
                "Session should NOT be in Reconnecting state for Host disconnect.");
        }

        [UnityTest]
        public IEnumerator Reconnect_OnlyForClient_NotHost()
        {
            _session.CreateRoom("Host No Reconnect");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            Assert.AreEqual(NetworkMode.Host, _localTransport.Mode,
                "Transport should be in Host mode after CreateRoom().");

            _localTransport.Stop();

            yield return TestHelpers.WaitForSeconds(0.5f);

            Assert.IsFalse(_reconnect.IsReconnecting,
                "IsReconnecting should stay false for Host mode.");
            Assert.AreEqual(0, _reconnect.AttemptCount,
                "AttemptCount should remain 0 for Host mode.");
            Assert.IsFalse(_reconnectStarted,
                "OnReconnectStarted should not fire for Host mode.");
        }

        [UnityTest]
        public IEnumerator Disconnect_WhileNotInRoom_ShouldNotReconnect()
        {
            Assert.AreEqual(SessionState.Idle, _session.State,
                "Session should start in Idle state.");

            _localTransport.Stop();

            yield return TestHelpers.WaitForSeconds(0.5f);

            Assert.IsFalse(_reconnect.IsReconnecting,
                "IsReconnecting should stay false when not in a room.");
            Assert.IsFalse(_reconnectStarted,
                "OnReconnectStarted should not fire when not in a room.");
            Assert.AreEqual(SessionState.Idle, _session.State,
                "Session should remain in Idle state.");
        }

        [UnityTest]
        public IEnumerator ReconnectHandler_ShouldHaveCorrectInitialState()
        {
            Assert.IsFalse(_reconnect.IsReconnecting,
                "IsReconnecting should be false on initialization.");
            Assert.AreEqual(0, _reconnect.AttemptCount,
                "AttemptCount should be 0 on initialization.");

            yield break;
        }

        [UnityTest]
        public IEnumerator ReconnectHandler_ShouldNotActivate_WhenRoomIsNull()
        {
            Assert.IsNull(_roomManager.CurrentRoom,
                "CurrentRoom should be null before any room is created.");

            _localTransport.Stop();

            yield return TestHelpers.WaitForSeconds(0.5f);

            Assert.IsFalse(_reconnect.IsReconnecting,
                "IsReconnecting should stay false when CurrentRoom is null.");
            Assert.AreEqual(0, _reconnect.AttemptCount,
                "AttemptCount should remain 0 when CurrentRoom is null.");
            Assert.IsFalse(_reconnectStarted,
                "OnReconnectStarted should not fire when CurrentRoom is null.");
        }

        [UnityTest]
        public IEnumerator Shutdown_ShouldPreventReconnect()
        {
            _session.CreateRoom("Shutdown Reconnect Test");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            _session.Shutdown();

            yield return TestHelpers.WaitForSeconds(0.5f);

            Assert.IsFalse(_reconnect.IsReconnecting,
                "IsReconnecting should stay false after Shutdown().");
            Assert.IsFalse(_reconnectStarted,
                "OnReconnectStarted should not fire after Shutdown().");
            Assert.AreEqual(SessionState.Idle, _session.State,
                "Session should be in Idle state after Shutdown().");
        }

        [UnityTest]
        public IEnumerator LeaveRoom_ShouldNotTriggerReconnect()
        {
            _session.CreateRoom("Leave Reconnect Test");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            _session.LeaveRoom();

            yield return TestHelpers.WaitForSeconds(0.5f);

            Assert.IsFalse(_reconnect.IsReconnecting,
                "IsReconnecting should stay false after LeaveRoom().");
            Assert.IsFalse(_reconnectStarted,
                "OnReconnectStarted should not fire after LeaveRoom().");
            Assert.AreEqual(SessionState.Idle, _session.State,
                "Session should be in Idle state after LeaveRoom().");
        }
    }
}
