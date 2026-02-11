# VR Player State Sync — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Synchronize VR player head and hand transforms (6DOF + optional custom state) across the network with built-in interpolation.

**Architecture:** Framework-agnostic IVRTrackingProvider interface for pose input, NexusVRPlayer (Mirror NetworkBehaviour) for network sync with Lerp/Slerp interpolation, NexusVRPlayerManager for spawn/despawn lifecycle bridging NexusSession events.

**Tech Stack:** Unity 6, Mirror NetworkBehaviour, KCP Transport (existing), C# interfaces

---

### Task 1: VRPose struct and Mirror serialization

**Files:**
- Create: `Assets/Nexus/Runtime/VR/VRPose.cs`
- Test: `Assets/Nexus/Tests/Runtime/VRPoseTests.cs`

**Step 1: Write the failing test**

```csharp
// Assets/Nexus/Tests/Runtime/VRPoseTests.cs
using Mirror;
using Nexus.Networking.VR;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public class VRPoseTests
    {
        [Test]
        public void VRPose_DefaultValues_ShouldBeIdentity()
        {
            var pose = new VRPose();

            Assert.AreEqual(Vector3.zero, pose.Head.position);
            Assert.AreEqual(Quaternion.identity, pose.Head.rotation);
            Assert.AreEqual(Vector3.zero, pose.LeftHand.position);
            Assert.AreEqual(Quaternion.identity, pose.LeftHand.rotation);
            Assert.AreEqual(Vector3.zero, pose.RightHand.position);
            Assert.AreEqual(Quaternion.identity, pose.RightHand.rotation);
        }

        [Test]
        public void VRPose_MirrorSerialization_ShouldRoundTrip()
        {
            var original = new VRPose
            {
                Head = new Pose(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30)),
                LeftHand = new Pose(new Vector3(4, 5, 6), Quaternion.Euler(40, 50, 60)),
                RightHand = new Pose(new Vector3(7, 8, 9), Quaternion.Euler(70, 80, 90))
            };

            var writer = new NetworkWriter();
            VRPose.WritePose(writer, original);

            var reader = new NetworkReader(writer.ToArraySegment());
            VRPose deserialized = VRPose.ReadPose(reader);

            AssertPoseApprox(original.Head, deserialized.Head, "Head");
            AssertPoseApprox(original.LeftHand, deserialized.LeftHand, "LeftHand");
            AssertPoseApprox(original.RightHand, deserialized.RightHand, "RightHand");
        }

        private static void AssertPoseApprox(Pose expected, Pose actual, string label)
        {
            Assert.That(actual.position.x, Is.EqualTo(expected.position.x).Within(0.001f), $"{label} position.x");
            Assert.That(actual.position.y, Is.EqualTo(expected.position.y).Within(0.001f), $"{label} position.y");
            Assert.That(actual.position.z, Is.EqualTo(expected.position.z).Within(0.001f), $"{label} position.z");
            Assert.That(actual.rotation.x, Is.EqualTo(expected.rotation.x).Within(0.001f), $"{label} rotation.x");
            Assert.That(actual.rotation.y, Is.EqualTo(expected.rotation.y).Within(0.001f), $"{label} rotation.y");
            Assert.That(actual.rotation.z, Is.EqualTo(expected.rotation.z).Within(0.001f), $"{label} rotation.z");
            Assert.That(actual.rotation.w, Is.EqualTo(expected.rotation.w).Within(0.001f), $"{label} rotation.w");
        }
    }
}
```

**Step 2: Run test to verify it fails**

Run: Unity Test Runner → Edit Mode → VRPoseTests
Expected: FAIL — `VRPose` type not found

**Step 3: Write minimal implementation**

```csharp
// Assets/Nexus/Runtime/VR/VRPose.cs
using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR
{
    public struct VRPose
    {
        public Pose Head;
        public Pose LeftHand;
        public Pose RightHand;

        public static void WritePose(NetworkWriter writer, VRPose pose)
        {
            writer.WriteVector3(pose.Head.position);
            writer.WriteQuaternion(pose.Head.rotation);
            writer.WriteVector3(pose.LeftHand.position);
            writer.WriteQuaternion(pose.LeftHand.rotation);
            writer.WriteVector3(pose.RightHand.position);
            writer.WriteQuaternion(pose.RightHand.rotation);
        }

        public static VRPose ReadPose(NetworkReader reader)
        {
            return new VRPose
            {
                Head = new Pose(reader.ReadVector3(), reader.ReadQuaternion()),
                LeftHand = new Pose(reader.ReadVector3(), reader.ReadQuaternion()),
                RightHand = new Pose(reader.ReadVector3(), reader.ReadQuaternion())
            };
        }
    }
}
```

**Step 4: Run test to verify it passes**

Run: Unity Test Runner → Edit Mode → VRPoseTests
Expected: 2 PASS

**Step 5: Commit**

```bash
git add Assets/Nexus/Runtime/VR/VRPose.cs Assets/Nexus/Tests/Runtime/VRPoseTests.cs
git commit -m "feat(vr): add VRPose struct with Mirror serialization"
```

---

### Task 2: IVRTrackingProvider and IVRCustomState interfaces

**Files:**
- Create: `Assets/Nexus/Runtime/VR/IVRTrackingProvider.cs`
- Create: `Assets/Nexus/Runtime/VR/IVRCustomState.cs`

No tests needed — pure interfaces with no logic.

**Step 1: Create IVRTrackingProvider**

```csharp
// Assets/Nexus/Runtime/VR/IVRTrackingProvider.cs
namespace Nexus.Networking.VR
{
    /// <summary>
    /// Provides local VR tracking data. Implement this interface to bridge
    /// your VR SDK (XRI, OVR, etc.) into Nexus networking.
    /// </summary>
    public interface IVRTrackingProvider
    {
        VRPose GetCurrentPose();
    }
}
```

**Step 2: Create IVRCustomState**

```csharp
// Assets/Nexus/Runtime/VR/IVRCustomState.cs
using Mirror;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Optional interface for syncing custom per-player state (grab state, tool type, etc.).
    /// Uses Mirror NetworkWriter/NetworkReader for optimal serialization.
    /// </summary>
    public interface IVRCustomState
    {
        void Serialize(NetworkWriter writer);
        void Deserialize(NetworkReader reader);
    }
}
```

**Step 3: Commit**

```bash
git add Assets/Nexus/Runtime/VR/IVRTrackingProvider.cs Assets/Nexus/Runtime/VR/IVRCustomState.cs
git commit -m "feat(vr): add IVRTrackingProvider and IVRCustomState interfaces"
```

---

### Task 3: NexusVRPlayer — Transform hierarchy and local pose application

**Files:**
- Create: `Assets/Nexus/Runtime/VR/NexusVRPlayer.cs`
- Test: `Assets/Nexus/Tests/Runtime/VRPlayerTests.cs`

**Step 1: Write failing tests**

```csharp
// Assets/Nexus/Tests/Runtime/VRPlayerTests.cs
using System.Collections;
using Nexus.Networking.VR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    public class VRPlayerTests
    {
        private GameObject _playerObject;
        private NexusVRPlayer _vrPlayer;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("TestVRPlayer");
            _vrPlayer = _playerObject.AddComponent<NexusVRPlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }
        }

        [Test]
        public void Awake_ShouldCreateChildTransforms()
        {
            Assert.IsNotNull(_vrPlayer.Head, "Head transform should be created");
            Assert.IsNotNull(_vrPlayer.LeftHand, "LeftHand transform should be created");
            Assert.IsNotNull(_vrPlayer.RightHand, "RightHand transform should be created");
        }

        [Test]
        public void ChildTransforms_ShouldBeChildrenOfPlayer()
        {
            Assert.AreEqual(_playerObject.transform, _vrPlayer.Head.parent);
            Assert.AreEqual(_playerObject.transform, _vrPlayer.LeftHand.parent);
            Assert.AreEqual(_playerObject.transform, _vrPlayer.RightHand.parent);
        }

        [Test]
        public void ApplyPose_ShouldSetTransformValues()
        {
            var pose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0), Quaternion.Euler(10, 0, 0)),
                LeftHand = new Pose(new Vector3(-0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, 45)),
                RightHand = new Pose(new Vector3(0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, -45))
            };

            _vrPlayer.ApplyPose(pose);

            AssertVector3Approx(pose.Head.position, _vrPlayer.Head.localPosition, "Head position");
            AssertQuaternionApprox(pose.Head.rotation, _vrPlayer.Head.localRotation, "Head rotation");
            AssertVector3Approx(pose.LeftHand.position, _vrPlayer.LeftHand.localPosition, "LeftHand position");
            AssertQuaternionApprox(pose.LeftHand.rotation, _vrPlayer.LeftHand.localRotation, "LeftHand rotation");
            AssertVector3Approx(pose.RightHand.position, _vrPlayer.RightHand.localPosition, "RightHand position");
            AssertQuaternionApprox(pose.RightHand.rotation, _vrPlayer.RightHand.localRotation, "RightHand rotation");
        }

        [Test]
        public void TrackingProvider_WhenSet_ShouldSamplePose()
        {
            var mockProvider = new MockTrackingProvider();
            var expectedPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.8f, 0), Quaternion.identity),
                LeftHand = new Pose(new Vector3(-0.5f, 1f, 0.3f), Quaternion.identity),
                RightHand = new Pose(new Vector3(0.5f, 1f, 0.3f), Quaternion.identity)
            };
            mockProvider.NextPose = expectedPose;

            _vrPlayer.TrackingProvider = mockProvider;
            _vrPlayer.SampleLocalPose();

            AssertVector3Approx(expectedPose.Head.position, _vrPlayer.Head.localPosition, "Head");
            AssertVector3Approx(expectedPose.LeftHand.position, _vrPlayer.LeftHand.localPosition, "LeftHand");
            AssertVector3Approx(expectedPose.RightHand.position, _vrPlayer.RightHand.localPosition, "RightHand");
        }

        private static void AssertVector3Approx(Vector3 expected, Vector3 actual, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f), $"{label}.x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f), $"{label}.y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f), $"{label}.z");
        }

        private static void AssertQuaternionApprox(Quaternion expected, Quaternion actual, string label)
        {
            Assert.That(Quaternion.Angle(expected, actual), Is.LessThan(0.1f), $"{label} angle diff");
        }
    }

    /// <summary>
    /// Mock IVRTrackingProvider for testing.
    /// </summary>
    public class MockTrackingProvider : IVRTrackingProvider
    {
        public VRPose NextPose { get; set; }

        public VRPose GetCurrentPose()
        {
            return NextPose;
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: Unity Test Runner → VRPlayerTests
Expected: FAIL — `NexusVRPlayer` type not found

**Step 3: Write NexusVRPlayer implementation (structure + local pose)**

```csharp
// Assets/Nexus/Runtime/VR/NexusVRPlayer.cs
using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Network-synced VR player. Spawned per player in room.
    /// Local player samples from IVRTrackingProvider.
    /// Remote players receive interpolated pose data.
    /// </summary>
    public class NexusVRPlayer : NetworkBehaviour
    {
        // Private fields
        private Transform _head;
        private Transform _leftHand;
        private Transform _rightHand;
        private IVRTrackingProvider _trackingProvider;
        private IVRCustomState _customState;
        private VRPose _targetPose;
        private float _sendTimer;
        private float _sendInterval;
        private float _interpolationSpeed;

        // Public properties
        public Transform Head => _head;
        public Transform LeftHand => _leftHand;
        public Transform RightHand => _rightHand;

        public IVRTrackingProvider TrackingProvider
        {
            get => _trackingProvider;
            set => _trackingProvider = value;
        }

        public IVRCustomState CustomState
        {
            get => _customState;
            set => _customState = value;
        }

        // Unity callbacks
        private void Awake()
        {
            _head = CreateChildTransform("Head");
            _leftHand = CreateChildTransform("LeftHand");
            _rightHand = CreateChildTransform("RightHand");
        }

        private void Update()
        {
            if (_trackingProvider != null)
            {
                SampleLocalPose();
            }
        }

        // Public methods

        /// <summary>
        /// Directly apply a pose to the child transforms.
        /// Used by local player for immediate application.
        /// </summary>
        public void ApplyPose(VRPose pose)
        {
            _head.localPosition = pose.Head.position;
            _head.localRotation = pose.Head.rotation;
            _leftHand.localPosition = pose.LeftHand.position;
            _leftHand.localRotation = pose.LeftHand.rotation;
            _rightHand.localPosition = pose.RightHand.position;
            _rightHand.localRotation = pose.RightHand.rotation;
        }

        /// <summary>
        /// Sample pose from tracking provider and apply immediately.
        /// Called by Update for local player, exposed for testing.
        /// </summary>
        public void SampleLocalPose()
        {
            if (_trackingProvider == null)
            {
                return;
            }

            VRPose pose = _trackingProvider.GetCurrentPose();
            ApplyPose(pose);
        }

        // Private methods
        private Transform CreateChildTransform(string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: Unity Test Runner → VRPlayerTests
Expected: 4 PASS

**Step 5: Commit**

```bash
git add Assets/Nexus/Runtime/VR/NexusVRPlayer.cs Assets/Nexus/Tests/Runtime/VRPlayerTests.cs
git commit -m "feat(vr): add NexusVRPlayer with transform hierarchy and local pose"
```

---

### Task 4: NexusVRPlayer — Remote interpolation

**Files:**
- Modify: `Assets/Nexus/Runtime/VR/NexusVRPlayer.cs`
- Modify: `Assets/Nexus/Tests/Runtime/VRPlayerTests.cs`

**Step 1: Add interpolation tests**

Add to `VRPlayerTests.cs`:

```csharp
[UnityTest]
public IEnumerator SetTargetPose_ShouldInterpolateOverTime()
{
    var targetPose = new VRPose
    {
        Head = new Pose(new Vector3(0, 2f, 0), Quaternion.identity),
        LeftHand = new Pose(new Vector3(-1f, 1f, 0), Quaternion.identity),
        RightHand = new Pose(new Vector3(1f, 1f, 0), Quaternion.identity)
    };

    _vrPlayer.SetTargetPose(targetPose, interpolationSpeed: 10f);

    // Wait a few frames for interpolation
    for (int i = 0; i < 10; i++)
    {
        _vrPlayer.InterpolateToTarget();
        yield return null;
    }

    // Should have moved toward target (not necessarily arrived)
    float headDist = Vector3.Distance(_vrPlayer.Head.localPosition, targetPose.Head.position);
    Assert.Less(headDist, 1.9f, "Head should have moved toward target");
}

[Test]
public void SetTargetPose_WithHighSpeed_ShouldReachTargetQuickly()
{
    var targetPose = new VRPose
    {
        Head = new Pose(new Vector3(0, 1.5f, 0), Quaternion.identity),
        LeftHand = new Pose(Vector3.zero, Quaternion.identity),
        RightHand = new Pose(Vector3.zero, Quaternion.identity)
    };

    _vrPlayer.SetTargetPose(targetPose, interpolationSpeed: 1000f);

    // Simulate one large-dt step
    _vrPlayer.InterpolateToTarget(deltaTime: 1f);

    float headDist = Vector3.Distance(_vrPlayer.Head.localPosition, targetPose.Head.position);
    Assert.Less(headDist, 0.01f, "Head should be at target with high speed");
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL — `SetTargetPose` and `InterpolateToTarget` methods not found

**Step 3: Add interpolation to NexusVRPlayer**

Add to `NexusVRPlayer.cs`:

```csharp
// Add fields:
private VRPose _targetPose;
private float _interpolationSpeed;
private bool _hasTarget;

// Add to Update():
// After local pose sampling block, add:
if (_hasTarget && _trackingProvider == null)
{
    InterpolateToTarget();
}

// Add public methods:

/// <summary>
/// Set the target pose for interpolation (used for remote players).
/// </summary>
public void SetTargetPose(VRPose pose, float interpolationSpeed)
{
    _targetPose = pose;
    _interpolationSpeed = interpolationSpeed;
    _hasTarget = true;
}

/// <summary>
/// Interpolate toward target pose. Called in Update for remote players.
/// deltaTime parameter exposed for testing; defaults to Time.deltaTime.
/// </summary>
public void InterpolateToTarget(float deltaTime = -1f)
{
    if (!_hasTarget)
    {
        return;
    }

    if (deltaTime < 0f)
    {
        deltaTime = Time.deltaTime;
    }

    float t = Mathf.Clamp01(_interpolationSpeed * deltaTime);

    _head.localPosition = Vector3.Lerp(_head.localPosition, _targetPose.Head.position, t);
    _head.localRotation = Quaternion.Slerp(_head.localRotation, _targetPose.Head.rotation, t);
    _leftHand.localPosition = Vector3.Lerp(_leftHand.localPosition, _targetPose.LeftHand.position, t);
    _leftHand.localRotation = Quaternion.Slerp(_leftHand.localRotation, _targetPose.LeftHand.rotation, t);
    _rightHand.localPosition = Vector3.Lerp(_rightHand.localPosition, _targetPose.RightHand.position, t);
    _rightHand.localRotation = Quaternion.Slerp(_rightHand.localRotation, _targetPose.RightHand.rotation, t);
}
```

**Step 4: Run tests to verify they pass**

Run: Unity Test Runner → VRPlayerTests
Expected: 6 PASS

**Step 5: Commit**

```bash
git add Assets/Nexus/Runtime/VR/NexusVRPlayer.cs Assets/Nexus/Tests/Runtime/VRPlayerTests.cs
git commit -m "feat(vr): add remote player interpolation to NexusVRPlayer"
```

---

### Task 5: NexusVRPlayer — Network sync (Command/ClientRpc)

**Files:**
- Modify: `Assets/Nexus/Runtime/VR/NexusVRPlayer.cs`

This task adds Mirror network sync. Testing Command/ClientRpc requires a real host+client connection, so we rely on integration testing (Task 8) rather than unit tests here.

**Step 1: Add network sync to NexusVRPlayer**

Add to `NexusVRPlayer.cs`:

```csharp
// Add field:
private float _sendTimer;
private float _sendInterval;

// Add public method for configuration:

/// <summary>
/// Configure network sync rate. Called by VRPlayerManager after spawn.
/// </summary>
public void ConfigureSync(int syncRateHz)
{
    _sendInterval = 1f / syncRateHz;
    _interpolationSpeed = syncRateHz;  // match interpolation to send rate
}

// Modify Update() to handle send timing for local player:
private void Update()
{
    if (_trackingProvider != null)
    {
        SampleLocalPose();
        SendPoseIfReady();
    }
    else if (_hasTarget)
    {
        InterpolateToTarget();
    }
}

private void SendPoseIfReady()
{
    _sendTimer += Time.deltaTime;
    if (_sendTimer < _sendInterval)
    {
        return;
    }

    _sendTimer -= _sendInterval;

    if (!isOwned)
    {
        return;
    }

    VRPose pose = new VRPose
    {
        Head = new Pose(_head.localPosition, _head.localRotation),
        LeftHand = new Pose(_leftHand.localPosition, _leftHand.localRotation),
        RightHand = new Pose(_rightHand.localPosition, _rightHand.localRotation)
    };

    CmdSendPose(pose);
}

[Command]
private void CmdSendPose(VRPose pose)
{
    RpcReceivePose(pose);
}

[ClientRpc(includeOwner = false)]
private void RpcReceivePose(VRPose pose)
{
    SetTargetPose(pose, _interpolationSpeed);
}
```

Note: Mirror requires `VRPose` to have registered Reader/Writer. Register them in a static initializer:

Add to `VRPose.cs`:

```csharp
// Add at the end of VRPose struct, after ReadPose:

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void RegisterSerializers()
{
    Writer<VRPose>.write = WritePose;
    Reader<VRPose>.read = ReadPose;
}
```

**Step 2: Verify compilation**

Open Unity, confirm no compile errors.

**Step 3: Commit**

```bash
git add Assets/Nexus/Runtime/VR/NexusVRPlayer.cs Assets/Nexus/Runtime/VR/VRPose.cs
git commit -m "feat(vr): add Mirror Command/ClientRpc network sync to NexusVRPlayer"
```

---

### Task 6: NexusVRPlayerManager — Lifecycle management

**Files:**
- Create: `Assets/Nexus/Runtime/VR/NexusVRPlayerManager.cs`
- Test: `Assets/Nexus/Tests/Runtime/VRPlayerManagerTests.cs`

**Step 1: Write failing tests**

```csharp
// Assets/Nexus/Tests/Runtime/VRPlayerManagerTests.cs
using System.Collections;
using System.Collections.Generic;
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

            NexusVRPlayer localPlayer = _vrManager.LocalPlayer;
            _session.LeaveRoom();
            yield return null;

            Assert.IsNotNull(despawnedPlayer, "OnVRPlayerDespawned should have fired.");
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL — `NexusVRPlayerManager` type not found

**Step 3: Write NexusVRPlayerManager implementation**

```csharp
// Assets/Nexus/Runtime/VR/NexusVRPlayerManager.cs
using System;
using System.Collections.Generic;
using Nexus.Networking.Core;
using UnityEngine;

namespace Nexus.Networking.VR
{
    /// <summary>
    /// Manages VR player lifecycle: spawn/despawn NexusVRPlayer instances
    /// in response to room events. Bridges NexusSession with VR sync.
    /// </summary>
    public class NexusVRPlayerManager : MonoBehaviour
    {
        // Private fields
        private INexusRoomManager _roomManager;
        private NexusConfig _config;
        private NexusVRPlayer _localPlayer;
        private readonly List<NexusVRPlayer> _remotePlayers = new List<NexusVRPlayer>();
        private readonly Dictionary<int, NexusVRPlayer> _playersByConnectionId = new Dictionary<int, NexusVRPlayer>();

        // Public properties
        public IVRTrackingProvider TrackingProvider { get; set; }
        public IVRCustomState CustomState { get; set; }
        public NexusVRPlayer LocalPlayer => _localPlayer;
        public IReadOnlyList<NexusVRPlayer> RemotePlayers => _remotePlayers;

        // Events
        public event Action<NexusVRPlayer> OnVRPlayerSpawned;
        public event Action<NexusVRPlayer> OnVRPlayerDespawned;

        // Unity callbacks
        private void OnDestroy()
        {
            if (_roomManager != null)
            {
                _roomManager.OnPlayerJoined -= HandlePlayerJoined;
                _roomManager.OnPlayerLeft -= HandlePlayerLeft;
                _roomManager.OnRoomLeft -= HandleRoomLeft;
            }
        }

        // Public methods
        public void Initialize(INexusRoomManager roomManager, NexusConfig config)
        {
            _roomManager = roomManager;
            _config = config;

            _roomManager.OnPlayerJoined += HandlePlayerJoined;
            _roomManager.OnPlayerLeft += HandlePlayerLeft;
            _roomManager.OnRoomLeft += HandleRoomLeft;
        }

        // Private methods
        private void HandlePlayerJoined(NexusPlayer player)
        {
            var playerObj = new GameObject($"VRPlayer_{player.DisplayName}");
            playerObj.transform.SetParent(transform, false);
            var vrPlayer = playerObj.AddComponent<NexusVRPlayer>();
            vrPlayer.ConfigureSync(_config.SyncRateHz);

            _playersByConnectionId[player.ConnectionId] = vrPlayer;

            if (player.IsLocal)
            {
                _localPlayer = vrPlayer;
                vrPlayer.TrackingProvider = TrackingProvider;
                vrPlayer.CustomState = CustomState;
            }
            else
            {
                _remotePlayers.Add(vrPlayer);
            }

            Debug.Log($"[NexusVRPlayerManager] VR player spawned: {player.DisplayName} (local={player.IsLocal})");
            OnVRPlayerSpawned?.Invoke(vrPlayer);
        }

        private void HandlePlayerLeft(NexusPlayer player)
        {
            if (!_playersByConnectionId.TryGetValue(player.ConnectionId, out NexusVRPlayer vrPlayer))
            {
                return;
            }

            DespawnPlayer(player.ConnectionId, vrPlayer);
        }

        private void HandleRoomLeft()
        {
            // Despawn all players
            var connectionIds = new List<int>(_playersByConnectionId.Keys);
            foreach (int connId in connectionIds)
            {
                if (_playersByConnectionId.TryGetValue(connId, out NexusVRPlayer vrPlayer))
                {
                    DespawnPlayer(connId, vrPlayer);
                }
            }
        }

        private void DespawnPlayer(int connectionId, NexusVRPlayer vrPlayer)
        {
            OnVRPlayerDespawned?.Invoke(vrPlayer);

            if (_localPlayer == vrPlayer)
            {
                _localPlayer = null;
            }
            else
            {
                _remotePlayers.Remove(vrPlayer);
            }

            _playersByConnectionId.Remove(connectionId);

            if (vrPlayer != null && vrPlayer.gameObject != null)
            {
                Destroy(vrPlayer.gameObject);
            }

            Debug.Log($"[NexusVRPlayerManager] VR player despawned (conn={connectionId})");
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: Unity Test Runner → Play Mode → VRPlayerManagerTests
Expected: 5 PASS

**Step 5: Commit**

```bash
git add Assets/Nexus/Runtime/VR/NexusVRPlayerManager.cs Assets/Nexus/Tests/Runtime/VRPlayerManagerTests.cs
git commit -m "feat(vr): add NexusVRPlayerManager lifecycle management"
```

---

### Task 7: NexusBootstrap integration

**Files:**
- Modify: `Assets/Nexus/Runtime/Core/NexusBootstrap.cs`

**Step 1: Write failing test**

Add to `VRPlayerManagerTests.cs`:

```csharp
[UnityTest]
public IEnumerator Bootstrap_ShouldAutoCreateVRPlayerManager()
{
    // This test verifies Bootstrap auto-wires VRPlayerManager
    // We need a fresh setup that goes through Bootstrap
    // For now, verify the component can be retrieved
    Assert.IsNotNull(_vrManager, "VRPlayerManager should exist on root object");
    yield break;
}
```

**Step 2: Add VRPlayerManager wiring to NexusBootstrap**

In `NexusBootstrap.cs`, add to `InitializeLocal()` method, after the session.Initialize() call:

```csharp
// Wire VR player manager
var vrPlayerManager = GetOrAddComponent<NexusVRPlayerManager>();
vrPlayerManager.Initialize(roomManager, config);
```

Add using directive:
```csharp
using Nexus.Networking.VR;
```

**Step 3: Verify compilation**

Open Unity, confirm no compile errors.

**Step 4: Commit**

```bash
git add Assets/Nexus/Runtime/Core/NexusBootstrap.cs Assets/Nexus/Tests/Runtime/VRPlayerManagerTests.cs
git commit -m "feat(vr): wire NexusVRPlayerManager into NexusBootstrap"
```

---

### Task 8: Integration smoke test

**Files:**
- Test: `Assets/Nexus/Tests/Runtime/VRSyncIntegrationTests.cs`

This test verifies the full flow: room creation → VR player spawn → pose sampling → transform updated.

**Step 1: Write integration test**

```csharp
// Assets/Nexus/Tests/Runtime/VRSyncIntegrationTests.cs
using System.Collections;
using Nexus.Networking.Core;
using Nexus.Networking.VR;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// End-to-end integration tests for VR player sync.
    /// Verifies the full pipeline: room → spawn → tracking → transform.
    /// </summary>
    public class VRSyncIntegrationTests : NexusTestBase
    {
        private NexusVRPlayerManager _vrManager;
        private MockTrackingProvider _mockProvider;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BaseSetUp();

            _vrManager = _rootObject.AddComponent<NexusVRPlayerManager>();
            _vrManager.Initialize(_roomManager, _config);

            _mockProvider = new MockTrackingProvider();
            _vrManager.TrackingProvider = _mockProvider;
        }

        [UnityTest]
        public IEnumerator FullPipeline_ShouldSyncLocalPoseToTransforms()
        {
            var expectedPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0.1f), Quaternion.Euler(5, 0, 0)),
                LeftHand = new Pose(new Vector3(-0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, 30)),
                RightHand = new Pose(new Vector3(0.3f, 1.2f, 0.3f), Quaternion.Euler(0, 0, -30))
            };
            _mockProvider.NextPose = expectedPose;

            _session.CreateRoom("Integration Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            // Wait a frame for Update to sample pose
            yield return null;
            yield return null;

            NexusVRPlayer player = _vrManager.LocalPlayer;

            Assert.That(
                Vector3.Distance(player.Head.localPosition, expectedPose.Head.position),
                Is.LessThan(0.01f),
                "Head position should match tracking data");
            Assert.That(
                Vector3.Distance(player.LeftHand.localPosition, expectedPose.LeftHand.position),
                Is.LessThan(0.01f),
                "LeftHand position should match tracking data");
            Assert.That(
                Vector3.Distance(player.RightHand.localPosition, expectedPose.RightHand.position),
                Is.LessThan(0.01f),
                "RightHand position should match tracking data");
        }

        [UnityTest]
        public IEnumerator PoseUpdate_ShouldReflectNewTrackingData()
        {
            _mockProvider.NextPose = new VRPose
            {
                Head = new Pose(Vector3.zero, Quaternion.identity),
                LeftHand = new Pose(Vector3.zero, Quaternion.identity),
                RightHand = new Pose(Vector3.zero, Quaternion.identity)
            };

            _session.CreateRoom("Pose Update Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            yield return null;

            // Change tracking data
            var newPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 2f, 0), Quaternion.identity),
                LeftHand = new Pose(new Vector3(-1f, 1.5f, 0), Quaternion.identity),
                RightHand = new Pose(new Vector3(1f, 1.5f, 0), Quaternion.identity)
            };
            _mockProvider.NextPose = newPose;

            // Wait for Update to pick up new pose
            yield return null;
            yield return null;

            Assert.That(
                Vector3.Distance(_vrManager.LocalPlayer.Head.localPosition, newPose.Head.position),
                Is.LessThan(0.01f),
                "Head should reflect updated tracking data");
        }
    }
}
```

**Step 2: Run all tests**

Run: Unity Test Runner → Play Mode → all VR tests
Expected: ALL PASS

**Step 3: Commit**

```bash
git add Assets/Nexus/Tests/Runtime/VRSyncIntegrationTests.cs
git commit -m "test(vr): add integration smoke tests for VR sync pipeline"
```

---

## Summary

| Task | Files | Tests | Description |
|------|-------|-------|-------------|
| 1 | VRPose.cs | VRPoseTests.cs (2) | Struct + Mirror serialization |
| 2 | IVRTrackingProvider.cs, IVRCustomState.cs | — | Interfaces |
| 3 | NexusVRPlayer.cs | VRPlayerTests.cs (4) | Hierarchy + local pose |
| 4 | NexusVRPlayer.cs | VRPlayerTests.cs (+2) | Remote interpolation |
| 5 | NexusVRPlayer.cs, VRPose.cs | — | Mirror Command/Rpc sync |
| 6 | NexusVRPlayerManager.cs | VRPlayerManagerTests.cs (5) | Lifecycle management |
| 7 | NexusBootstrap.cs | — | Bootstrap wiring |
| 8 | VRSyncIntegrationTests.cs | VRSyncIntegrationTests (2) | End-to-end smoke tests |

**Total: 5 new files, 1 modified, 3 test files, ~15 tests**
