using System.Collections;
using Nexus.Networking.Core;
using Nexus.Networking.VR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    public class VRPlayerManagerTests : NexusTestBase
    {
        private NexusVRPlayerManager _vrManager;
        private MockTrackingProvider _mockProvider;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BaseSetUp();

            _vrManager = _rootObject.AddComponent<NexusVRPlayerManager>();
            _mockProvider = new MockTrackingProvider();
            _mockProvider.NextPose = new VRPose
            {
                Head = new Pose(Vector3.up * 1.7f, Quaternion.identity),
                LeftHand = new Pose(Vector3.left * 0.3f, Quaternion.identity),
                RightHand = new Pose(Vector3.right * 0.3f, Quaternion.identity)
            };

            _vrManager.Initialize(_roomManager, _session.Config);
            _vrManager.TrackingProvider = _mockProvider;
        }

        [UnityTest]
        public IEnumerator CreateRoom_ShouldSpawnLocalVRPlayer()
        {
            _session.CreateRoom("VR Test Room");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f,
                message: "LocalPlayer should be spawned after CreateRoom.");

            Assert.IsNotNull(_vrManager.LocalPlayer);
            Assert.IsNotNull(_vrManager.LocalPlayer.Head);
            Assert.IsNotNull(_vrManager.LocalPlayer.LeftHand);
            Assert.IsNotNull(_vrManager.LocalPlayer.RightHand);
        }

        [UnityTest]
        public IEnumerator LocalPlayer_ShouldHaveTrackingProvider()
        {
            _session.CreateRoom("VR Provider Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            Assert.AreEqual(_mockProvider, _vrManager.LocalPlayer.TrackingProvider,
                "Local player should have the registered TrackingProvider.");
        }

        [UnityTest]
        public IEnumerator OnVRPlayerSpawned_ShouldFireForLocalPlayer()
        {
            NexusVRPlayer spawnedPlayer = null;
            _vrManager.OnVRPlayerSpawned += p => spawnedPlayer = p;

            _session.CreateRoom("Event Test Room");

            yield return TestHelpers.WaitForCondition(
                () => spawnedPlayer != null,
                timeout: 5f,
                message: "OnVRPlayerSpawned should fire after CreateRoom.");

            Assert.AreEqual(_vrManager.LocalPlayer, spawnedPlayer);
        }

        [UnityTest]
        public IEnumerator LeaveRoom_ShouldDespawnAllPlayers()
        {
            _session.CreateRoom("Leave VR Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            _session.LeaveRoom();
            yield return null;

            Assert.IsNull(_vrManager.LocalPlayer,
                "LocalPlayer should be null after LeaveRoom.");
        }

        [UnityTest]
        public IEnumerator OnVRPlayerDespawned_ShouldFireOnLeave()
        {
            NexusVRPlayer despawnedPlayer = null;
            _vrManager.OnVRPlayerDespawned += p => despawnedPlayer = p;

            _session.CreateRoom("Despawn Event Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            _session.LeaveRoom();
            yield return null;

            Assert.IsNotNull(despawnedPlayer, "OnVRPlayerDespawned should have fired.");
        }
    }
}
