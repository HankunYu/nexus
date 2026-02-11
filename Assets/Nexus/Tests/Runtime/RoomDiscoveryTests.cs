using System;
using System.Collections;
using System.Collections.Generic;
using Nexus.Networking.Core;
using Nexus.Networking.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// Play Mode tests for LAN discovery broadcast and timeout behavior.
    /// </summary>
    public class RoomDiscoveryTests : NexusTestBase
    {
        // Constants
        private const int TestDiscoveryPort = 48888;
        private const float TestRoomTimeout = 2f;
        private const string TestRoomName = "DiscoveryTestRoom";

        // Private fields
        private GameObject _listenerObject;
        private LanDiscovery _listener;
        private List<RoomInfo> _foundRooms;
        private List<RoomInfo> _lostRooms;

        [SetUp]
        public void SetUp()
        {
            _foundRooms = new List<RoomInfo>();
            _lostRooms = new List<RoomInfo>();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupListener();
            CleanupExtraBroadcasters();
        }

        [UnityTest]
        public IEnumerator StartBroadcast_ShouldMakeDiscoveryActive()
        {
            // Act — CreateRoom triggers HandleRoomCreated which calls Discovery.StartBroadcast
            _session.CreateRoom(TestRoomName);

            // Wait a frame for everything to settle
            yield return null;

            // Assert
            Assert.IsTrue(_discovery.IsActive, "Discovery should be active after room creation starts broadcast.");
        }

        [UnityTest]
        public IEnumerator StartListening_ShouldDiscoverBroadcastingRoom()
        {
            // Arrange — Host creates and broadcasts a room
            _session.CreateRoom(TestRoomName);
            yield return null;

            // Create a listener and subscribe to events
            CreateListener();
            _listener.OnRoomFound += HandleRoomFound;
            _listener.StartListening();

            // Wait for the listener to discover the room
            yield return TestHelpers.WaitForCondition(
                () => _foundRooms.Count > 0,
                5f,
                "Listener should discover the broadcasting room.");

            // Assert
            Assert.AreEqual(1, _foundRooms.Count);
            Assert.AreEqual(TestRoomName, _foundRooms[0].RoomName);
        }

        [UnityTest]
        public IEnumerator DiscoveredRoom_ShouldContainCorrectInfo()
        {
            // Arrange — Host creates and broadcasts a room
            _session.CreateRoom(TestRoomName);
            yield return null;

            // Create listener
            CreateListener();
            _listener.OnRoomFound += HandleRoomFound;
            _listener.StartListening();

            // Wait for discovery
            yield return TestHelpers.WaitForCondition(
                () => _foundRooms.Count > 0,
                5f,
                "Listener should discover the broadcasting room.");

            // Assert — verify all RoomInfo fields
            RoomInfo room = _foundRooms[0];
            Assert.AreEqual(TestRoomName, room.RoomName, "RoomName should match the broadcasted room name.");
            Assert.AreEqual(_config.Port, room.Port, "Port should match the configured port.");
            Assert.AreEqual(_config.MaxPlayers, room.MaxPlayers, "MaxPlayers should match config.");
            Assert.GreaterOrEqual(room.CurrentPlayers, 1, "CurrentPlayers should be at least 1 (the host).");
            Assert.IsNotNull(room.HostAddress, "HostAddress should not be null.");
            Assert.IsNotEmpty(room.HostAddress, "HostAddress should not be empty.");
        }

        [UnityTest]
        public IEnumerator StopBroadcast_ShouldTriggerRoomLost()
        {
            // Arrange — Host creates and broadcasts a room
            _session.CreateRoom(TestRoomName);
            yield return null;

            // Create listener and discover the room
            CreateListener();
            _listener.OnRoomFound += HandleRoomFound;
            _listener.OnRoomLost += HandleRoomLost;
            _listener.StartListening();

            yield return TestHelpers.WaitForCondition(
                () => _foundRooms.Count > 0,
                5f,
                "Listener should discover the broadcasting room.");

            // Act — Host stops broadcasting by leaving the room
            _session.LeaveRoom();

            // Wait for room timeout + 1s buffer for the listener to detect the loss
            yield return TestHelpers.WaitForSeconds(TestRoomTimeout + 1f);

            // Assert
            Assert.GreaterOrEqual(_lostRooms.Count, 1, "OnRoomLost should fire after broadcast stops and timeout expires.");
            Assert.AreEqual(TestRoomName, _lostRooms[0].RoomName, "Lost room name should match.");
        }

        [UnityTest]
        public IEnumerator MultipleRooms_ShouldDiscoverAll()
        {
            // Arrange — First host broadcasts
            _session.CreateRoom("Room_A");
            yield return null;

            // Create a second broadcaster manually on a separate GameObject
            var secondBroadcasterObject = new GameObject("SecondBroadcaster");
            var secondBroadcaster = secondBroadcasterObject.AddComponent<LanDiscovery>();
            yield return null;

            secondBroadcaster.Configure(TestDiscoveryPort, TestRoomTimeout);

            var secondRoom = new RoomInfo
            {
                RoomId = Guid.NewGuid().ToString(),
                RoomName = "Room_B",
                Port = 7778,
                MaxPlayers = 4,
                CurrentPlayers = 1
            };
            secondBroadcaster.StartBroadcast(secondRoom);

            // Create listener
            CreateListener();
            _listener.OnRoomFound += HandleRoomFound;
            _listener.StartListening();

            // Wait for both rooms to be discovered
            yield return TestHelpers.WaitForCondition(
                () => _foundRooms.Count >= 2,
                5f,
                "Listener should discover both broadcasting rooms.");

            // Assert
            Assert.AreEqual(2, _foundRooms.Count, "Should discover exactly 2 rooms.");

            bool foundA = _foundRooms.Exists(r => r.RoomName == "Room_A");
            bool foundB = _foundRooms.Exists(r => r.RoomName == "Room_B");
            Assert.IsTrue(foundA, "Should discover Room_A.");
            Assert.IsTrue(foundB, "Should discover Room_B.");

            // Cleanup second broadcaster
            secondBroadcaster.Stop();
            UnityEngine.Object.DestroyImmediate(secondBroadcasterObject);
        }

        [UnityTest]
        public IEnumerator StopListening_ShouldStopReceivingUpdates()
        {
            // Arrange — Host broadcasts
            _session.CreateRoom(TestRoomName);
            yield return null;

            // Create listener and discover the room
            CreateListener();
            _listener.OnRoomFound += HandleRoomFound;
            _listener.StartListening();

            yield return TestHelpers.WaitForCondition(
                () => _foundRooms.Count > 0,
                5f,
                "Listener should discover the broadcasting room.");

            // Act — Stop listener and clear found list
            _listener.Stop();
            _foundRooms.Clear();

            // Wait a bit to see if any new events arrive
            yield return TestHelpers.WaitForSeconds(2f);

            // Assert — no new rooms should be discovered after stopping
            Assert.AreEqual(0, _foundRooms.Count,
                "No new OnRoomFound events should fire after StopListening.");
        }

        // Protected overrides
        protected override NexusConfig CreateTestConfig()
        {
            return TestHelpers.CreateConfig(
                discoveryPort: TestDiscoveryPort,
                roomTimeoutSeconds: TestRoomTimeout,
                broadcastInterval: 0.5f);
        }

        // Private methods
        private void CreateListener()
        {
            _listenerObject = new GameObject("DiscoveryListener");
            _listener = _listenerObject.AddComponent<LanDiscovery>();
            // Must wait a frame for Awake, but Configure can be called immediately
            _listener.Configure(TestDiscoveryPort, TestRoomTimeout);
        }

        private void CleanupListener()
        {
            if (_listener != null)
            {
                _listener.Stop();
            }

            if (_listenerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_listenerObject);
                _listenerObject = null;
                _listener = null;
            }
        }

        private void CleanupExtraBroadcasters()
        {
            // Safety net: destroy any leftover second broadcaster objects
            var leftover = GameObject.Find("SecondBroadcaster");
            if (leftover != null)
            {
                var disc = leftover.GetComponent<LanDiscovery>();
                if (disc != null)
                {
                    disc.Stop();
                }

                UnityEngine.Object.DestroyImmediate(leftover);
            }
        }

        private void HandleRoomFound(RoomInfo room)
        {
            _foundRooms.Add(room);
        }

        private void HandleRoomLost(RoomInfo room)
        {
            _lostRooms.Add(room);
        }
    }
}
