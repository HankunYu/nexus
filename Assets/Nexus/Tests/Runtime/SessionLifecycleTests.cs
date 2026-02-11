using System.Collections;
using Nexus.Networking.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// Play Mode tests for NexusSession state machine transitions.
    /// Covers initialization, room creation, leaving, shutdown, and edge cases.
    /// </summary>
    public class SessionLifecycleTests : NexusTestBase
    {
        [UnityTest]
        public IEnumerator InitialState_ShouldBeIdle()
        {
            Assert.AreEqual(SessionState.Idle, _session.State,
                "Session should be in Idle state after Initialize().");

            yield break;
        }

        [UnityTest]
        public IEnumerator CreateRoom_ShouldTransitionToInRoom()
        {
            _session.CreateRoom("Test Room");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            Assert.AreEqual(SessionState.InRoom, _session.State);
        }

        [UnityTest]
        public IEnumerator CreateRoom_ShouldPopulateRoomInfo()
        {
            string roomName = "My Test Room";

            _session.CreateRoom(roomName);

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            RoomInfo currentRoom = _roomManager.CurrentRoom;

            Assert.IsNotNull(currentRoom, "CurrentRoom should not be null after CreateRoom().");
            Assert.AreEqual(roomName, currentRoom.RoomName,
                "RoomName should match the name passed to CreateRoom().");
            Assert.AreEqual(_config.MaxPlayers, currentRoom.MaxPlayers,
                "MaxPlayers should match config value.");
            Assert.AreEqual(_config.Port, currentRoom.Port,
                "Port should match config value.");
            Assert.IsFalse(string.IsNullOrEmpty(currentRoom.RoomId),
                "RoomId should be a non-empty string.");
        }

        [UnityTest]
        public IEnumerator LeaveRoom_FromHosting_ShouldReturnToIdle()
        {
            _session.CreateRoom("Leave Test Room");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            _session.LeaveRoom();

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.Idle,
                timeout: 5f,
                message: "Session did not return to Idle state after LeaveRoom().");

            Assert.AreEqual(SessionState.Idle, _session.State);
        }

        [UnityTest]
        public IEnumerator LeaveRoom_ShouldStopTransport()
        {
            _session.CreateRoom("Transport Stop Test");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            Assert.IsTrue(_localTransport.IsActive,
                "Transport should be active while in room.");

            _session.LeaveRoom();

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.Idle,
                timeout: 5f,
                message: "Session did not return to Idle state after LeaveRoom().");

            Assert.IsFalse(_localTransport.IsActive,
                "Transport should not be active after LeaveRoom().");
        }

        [UnityTest]
        public IEnumerator Shutdown_ShouldCleanupEverything()
        {
            _session.CreateRoom("Shutdown Test Room");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after CreateRoom().");

            _session.Shutdown();

            yield return null;

            Assert.AreEqual(SessionState.Idle, _session.State,
                "Session state should be Idle after Shutdown().");
            Assert.IsFalse(_localTransport.IsActive,
                "Transport should not be active after Shutdown().");
            Assert.IsFalse(_discovery.IsActive,
                "Discovery should not be active after Shutdown().");
        }

        [UnityTest]
        public IEnumerator CreateRoom_WhenAlreadyInRoom_ShouldNotCrash()
        {
            string originalRoomName = "Original Room";

            _session.CreateRoom(originalRoomName);

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session did not reach InRoom state after first CreateRoom().");

            LogAssert.Expect(LogType.Warning,
                $"[NexusSession] Cannot create room in state {SessionState.InRoom}.");

            Assert.DoesNotThrow(() => _session.CreateRoom("Second Room"),
                "CreateRoom while InRoom should not throw an exception.");

            Assert.AreEqual(SessionState.InRoom, _session.State,
                "State should still be InRoom after duplicate CreateRoom().");
            Assert.AreEqual(originalRoomName, _roomManager.CurrentRoom.RoomName,
                "Room name should remain unchanged after duplicate CreateRoom().");

            yield return null;
        }

        [UnityTest]
        public IEnumerator DoubleInitialize_ShouldBeIdempotent()
        {
            Assert.DoesNotThrow(
                () => _session.Initialize(_localTransport, _discovery, _roomManager),
                "Calling Initialize() a second time should not throw an exception.");

            Assert.AreEqual(SessionState.Idle, _session.State,
                "State should remain Idle after double Initialize().");

            _session.CreateRoom("Double Init Test");

            yield return TestHelpers.WaitForCondition(
                () => _session.State == SessionState.InRoom,
                timeout: 5f,
                message: "Session should still function after double Initialize().");

            Assert.AreEqual(SessionState.InRoom, _session.State);
        }
    }
}
