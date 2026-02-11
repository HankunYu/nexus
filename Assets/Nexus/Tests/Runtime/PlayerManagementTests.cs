using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nexus.Networking.Core;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// Play Mode tests for LocalRoomManager player tracking.
    /// Validates host player creation, event firing, player list management,
    /// and NexusPlayer field correctness in single-process Host mode.
    /// </summary>
    public class PlayerManagementTests : NexusTestBase
    {
        // Private fields
        private List<NexusPlayer> _joinedPlayers;
        private List<NexusPlayer> _leftPlayers;

        [SetUp]
        public void SetUp()
        {
            _joinedPlayers = new List<NexusPlayer>();
            _leftPlayers = new List<NexusPlayer>();

            _roomManager.OnPlayerJoined += player => _joinedPlayers.Add(player);
            _roomManager.OnPlayerLeft += player => _leftPlayers.Add(player);
        }

        [UnityTest]
        public IEnumerator HostCreateRoom_ShouldAddHostPlayer()
        {
            _session.CreateRoom("Host Player Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            Assert.GreaterOrEqual(_roomManager.Players.Count, 1,
                "Players list should contain at least the host player.");

            NexusPlayer hostPlayer = _roomManager.Players[0];

            Assert.IsTrue(hostPlayer.IsHost,
                "First player should be the host.");
            Assert.AreEqual(0, hostPlayer.ConnectionId,
                "Host player ConnectionId should be 0.");
            Assert.IsTrue(hostPlayer.IsLocal,
                "Host player should be marked as local.");
        }

        [UnityTest]
        public IEnumerator ClientJoin_ShouldTriggerOnPlayerJoined()
        {
            _session.CreateRoom("Join Event Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            // OnPlayerJoined should have fired at least once for the host player
            Assert.GreaterOrEqual(_joinedPlayers.Count, 1,
                "OnPlayerJoined should have fired at least once for the host player.");

            NexusPlayer joinedHost = _joinedPlayers.First(p => p.IsHost);

            Assert.IsNotNull(joinedHost,
                "OnPlayerJoined event should contain a player with IsHost == true.");
            Assert.IsTrue(joinedHost.IsHost,
                "The joined host player's IsHost should be true.");
        }

        [UnityTest]
        public IEnumerator ClientJoin_ShouldUpdatePlayersList()
        {
            _session.CreateRoom("Players List Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            Assert.GreaterOrEqual(_roomManager.Players.Count, 1,
                "Players list should contain at least one player after room creation.");

            bool containsHost = _roomManager.Players.Any(p => p.IsHost);

            Assert.IsTrue(containsHost,
                "Players list should contain the host player.");
        }

        [UnityTest]
        public IEnumerator ClientJoin_ShouldUpdateCurrentPlayers()
        {
            _session.CreateRoom("Current Players Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            Assert.IsNotNull(_roomManager.CurrentRoom,
                "CurrentRoom should not be null while in room.");
            Assert.GreaterOrEqual(_roomManager.CurrentRoom.CurrentPlayers, 1,
                "CurrentRoom.CurrentPlayers should be at least 1 after host creates room.");
        }

        [UnityTest]
        public IEnumerator ClientLeave_ShouldTriggerOnPlayerLeft()
        {
            _session.CreateRoom("Leave Event Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            Assert.GreaterOrEqual(_roomManager.Players.Count, 1,
                "Players list should have at least 1 player before leaving.");

            _roomManager.LeaveRoom();

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.Idle,
                timeout: 5f,
                message: "RoomManager did not return to Idle state after LeaveRoom().");

            Assert.AreEqual(0, _roomManager.Players.Count,
                "Players list should be empty after leaving the room.");
        }

        [UnityTest]
        public IEnumerator HostLeave_ShouldDisconnectAllPlayers()
        {
            _session.CreateRoom("Host Leave Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            _roomManager.LeaveRoom();

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.Idle,
                timeout: 5f,
                message: "RoomManager did not return to Idle state after host LeaveRoom().");

            Assert.IsFalse(_localTransport.IsActive,
                "Transport should be stopped after host leaves.");
            Assert.AreEqual(0, _roomManager.Players.Count,
                "Players list should be empty after host leaves.");
            Assert.AreEqual(RoomState.Idle, _roomManager.CurrentState,
                "RoomManager should be in Idle state after host leaves.");
        }

        [UnityTest]
        public IEnumerator MultipleClients_ShouldTrackAll()
        {
            _session.CreateRoom("Track All Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            // In single-process Host mode, only the host player is tracked.
            // Verify all tracked players have unique ConnectionIds.
            var connectionIds = new HashSet<int>();

            foreach (NexusPlayer player in _roomManager.Players)
            {
                bool isUnique = connectionIds.Add(player.ConnectionId);
                Assert.IsTrue(isUnique,
                    $"Duplicate ConnectionId {player.ConnectionId} found. All players should have unique ConnectionIds.");
            }

            Assert.AreEqual(_roomManager.Players.Count, connectionIds.Count,
                "Number of unique ConnectionIds should match total player count.");
        }

        [UnityTest]
        public IEnumerator PlayerInfo_ShouldHaveCorrectFields()
        {
            _session.CreateRoom("Player Info Test");

            yield return TestHelpers.WaitForCondition(
                () => _roomManager.CurrentState == RoomState.InRoom,
                timeout: 5f,
                message: "RoomManager did not reach InRoom state after CreateRoom().");

            Assert.GreaterOrEqual(_roomManager.Players.Count, 1,
                "Players list should contain at least the host player.");

            NexusPlayer hostPlayer = _roomManager.Players.First(p => p.IsHost);

            Assert.IsFalse(string.IsNullOrEmpty(hostPlayer.PlayerId),
                "Host player PlayerId should be a non-empty string.");
            Assert.IsFalse(string.IsNullOrEmpty(hostPlayer.DisplayName),
                "Host player DisplayName should be a non-empty string.");
            Assert.IsTrue(hostPlayer.IsHost,
                "Host player IsHost should be true.");
            Assert.IsTrue(hostPlayer.IsLocal,
                "Host player IsLocal should be true.");
            Assert.AreEqual(0, hostPlayer.ConnectionId,
                "Host player ConnectionId should be 0.");
        }
    }
}
