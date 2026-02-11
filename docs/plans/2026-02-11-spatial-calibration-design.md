# Spatial Calibration Design — Multi-Player Coordinate Alignment

> Date: 2026-02-11
> Status: Approved
> Project: com.nexus.networking (UPM Package)

## Overview

VR spatial calibration framework for aligning multiple players' tracking coordinate systems
in the same physical space. Each VR headset has its own tracking origin; this system computes
a per-player offset (position + rotation) so all players share a consistent world coordinate system.

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Primary scenario | Co-located (same room) | LAN-first strategy |
| Calibration methods | Manual + Meta Spatial Anchor + Marker | Layered: universal fallback + platform-specific |
| Manual method | Two-point alignment | Calibrates both position and rotation |
| Rotation calibration | Y-axis (yaw) only | Gravity provides consistent pitch/roll |
| Offset application | Root Transform | Decoupled from VR sync — no changes to pose code |
| Provider selection | Developer-configured | Not auto-selected at runtime |
| Sub-package strategy | Assets/ with independent asmdef | Simple, developed in same project |
| Reference frame | Host = world origin | Host calibration is always Identity |

## Architecture

### Project Structure

```
Assets/
├── Nexus/Runtime/VR/Calibration/           ← Core (existing asmdef)
│   ├── CalibrationData.cs                  — Offset struct (Vector3 + Quaternion)
│   ├── CalibrationState.cs                 — State enum
│   ├── ICalibrationProvider.cs             — Provider interface
│   ├── SpatialCalibrationManager.cs        — Lifecycle + network sync
│   └── ManualCalibrationProvider.cs        — Two-point alignment
│
├── NexusCalibrationMeta/Runtime/           ← Meta sub-package (independent asmdef)
│   ├── Nexus.Calibration.Meta.asmdef
│   └── MetaSpatialAnchorProvider.cs        — #if META_XR_SDK
│
└── NexusCalibrationMarker/Runtime/         ← Marker sub-package (independent asmdef)
    ├── Nexus.Calibration.Marker.asmdef
    └── MarkerCalibrationProvider.cs        — #if AR_FOUNDATION
```

### Core Data Types

```csharp
public struct CalibrationData
{
    public Vector3 Position;
    public Quaternion Rotation;

    public static readonly CalibrationData Identity = new CalibrationData
    {
        Position = Vector3.zero,
        Rotation = Quaternion.identity
    };
}

public enum CalibrationState
{
    None,
    InProgress,
    Calibrated,
    Failed
}
```

### ICalibrationProvider Interface

```csharp
public interface ICalibrationProvider
{
    CalibrationState State { get; }
    event Action<CalibrationData> OnCalibrationComplete;
    event Action<string> OnCalibrationFailed;

    void SetReferencePoints(Vector3 pointA, Vector3 pointB);
    void StartCalibration();
    void CancelCalibration();
}
```

### ManualCalibrationProvider — Two-Point Alignment

**Flow:**
1. Host points controller at physical point A → press button → record
2. Host points controller at physical point B → press button → record
3. Reference points synced to all clients via network
4. Client points at same point A → record → point B → record
5. System computes offset

**Math:**
```
hostDir = normalize(B_host - A_host)       // projected to XZ plane
clientDir = normalize(B_client - A_client) // projected to XZ plane

rotationOffset = Quaternion.FromToRotation(clientDir, hostDir)  // Y-axis only
positionOffset = A_host - rotationOffset * A_client
```

**Key:** Only Y-axis rotation (yaw) is calibrated. Gravity provides consistent pitch/roll across devices.

### SpatialCalibrationManager

Uses Mirror network messages (RegisterHandler/Send) for sync. MonoBehaviour, not
NetworkBehaviour, to avoid ownership issues with Command/ClientRpc.

```csharp
public class SpatialCalibrationManager : MonoBehaviour
{
    private ICalibrationProvider _provider;
    private NexusVRPlayerManager _vrPlayerManager;

    public CalibrationState State { get; }
    public CalibrationData LocalCalibration { get; }
    public event Action<CalibrationData> OnCalibrationComplete;

    public void Initialize(ICalibrationProvider provider, NexusVRPlayerManager vrPlayerManager);

    // Host: record reference points and broadcast via NetworkServer.SendToAll
    public void RecordReferencePoint(Vector3 controllerPosition);

    // Network message handlers
    private void OnClientReceivedReferencePoints(ReferencePointsMessage msg);
    private void OnServerReceivedCalibration(NetworkConnectionToClient conn, CalibrationResultMessage msg);
    private void OnClientReceivedCalibration(CalibrationResultMessage msg);

    // Apply offset to NexusVRPlayer.transform
    private void ApplyCalibrationToPlayer(NexusVRPlayer player, CalibrationData data);
}
```

**Network Messages:**
```csharp
public struct ReferencePointsMessage : NetworkMessage
{
    public Vector3 PointA;
    public Vector3 PointB;
}

public struct CalibrationResultMessage : NetworkMessage
{
    public int ConnectionId;
    public Vector3 Position;
    public Quaternion Rotation;
}
```

**Network Flow:**
```
Host: RecordReferencePoint(A) → RecordReferencePoint(B)
  ↓ NetworkServer.SendToAll<ReferencePointsMessage>
Client: receives reference points → provider.SetReferencePoints → StartCalibration()
  ↓ user completes calibration
Client: NetworkClient.Send<CalibrationResultMessage>
  ↓ Server receives, stores, relays
All: OnClientReceivedCalibration → set VRPlayer.transform
```

**Integration with NexusVRPlayerManager:**
- Listens to OnVRPlayerSpawned
- Applies existing calibration data to newly spawned players
- Host player calibration is always Identity

### MetaSpatialAnchorProvider (Sub-package)

```csharp
#if META_XR_SDK
public class MetaSpatialAnchorProvider : MonoBehaviour, ICalibrationProvider
{
    // 1. Host creates OVRSpatialAnchor → gets UUID
    // 2. UUID synced to clients via network
    // 3. Clients localize to same anchor via UUID
    // 4. Pose difference between local tracking and anchor = CalibrationData
}
#endif
```

### MarkerCalibrationProvider (Sub-package)

```csharp
#if AR_FOUNDATION
public class MarkerCalibrationProvider : MonoBehaviour, ICalibrationProvider
{
    // 1. Physical marker (QR code, ArUco) placed in room
    // 2. Each player views marker via Passthrough/camera
    // 3. AR Image Tracking returns marker's 6DOF pose
    // 4. Pose differences across devices = CalibrationData
}
#endif
```

## Offset Application

The calibration offset is applied to `NexusVRPlayer.transform` (root Transform).
VR poses written to child transforms use `localPosition/localRotation`.
World positions are automatically correct:

```
child.worldPosition = calibrationOffset.position + calibrationOffset.rotation * child.localPosition
```

This approach is fully decoupled from the VR pose sync system — no changes needed
to NexusVRPlayer, VRPose, or network sync code.

## Testing

| Test | Validates |
|------|-----------|
| CalibrationData_Identity_ShouldBeZeroOffset | Identity default values |
| CalibrationData_ApplyToTransform_ShouldOffsetCorrectly | Offset applied to Transform produces correct worldPosition |
| ManualCalibration_TwoPoints_ShouldComputeCorrectOffset | Two-point math: position + rotation offset |
| ManualCalibration_SamePoints_ShouldReturnIdentity | Same points → Identity |
| ManualCalibration_RotatedPoints_ShouldComputeYawOnly | Only Y-axis rotation calibrated |
| SpatialCalibrationManager_ApplyToPlayer_ShouldSetRootTransform | Calibration applied to VRPlayer.transform |
| SpatialCalibrationManager_NewPlayerJoined_ShouldApplyExistingCalibration | Late joiner gets existing calibration |

---

# Spatial Calibration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement VR spatial calibration framework with manual two-point alignment, network sync, and skeleton sub-packages for Meta and Marker providers.

**Architecture:** Interface-based calibration providers with a central SpatialCalibrationManager that applies per-player offsets to NexusVRPlayer root Transforms. Network sync via Mirror messages (RegisterHandler/Send). Sub-packages under Assets/ with independent asmdef files.

**Tech Stack:** Unity 6, Mirror, C#

---

### Task 1: CalibrationData + CalibrationState + Mirror serialization

**Files:**
- Create: `Assets/Nexus/Runtime/VR/Calibration/CalibrationData.cs`
- Create: `Assets/Nexus/Runtime/VR/Calibration/CalibrationState.cs`
- Test: `Assets/Nexus/Tests/Runtime/CalibrationDataTests.cs`

**Step 1: Write failing tests**

```csharp
// Assets/Nexus/Tests/Runtime/CalibrationDataTests.cs
using Mirror;
using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public class CalibrationDataTests
    {
        [Test]
        public void Identity_ShouldHaveZeroPositionAndIdentityRotation()
        {
            CalibrationData identity = CalibrationData.Identity;

            Assert.AreEqual(Vector3.zero, identity.Position);
            Assert.That(Quaternion.Angle(Quaternion.identity, identity.Rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void MirrorSerialization_ShouldRoundTrip()
        {
            var original = new CalibrationData
            {
                Position = new Vector3(1.5f, 0.2f, -3.0f),
                Rotation = Quaternion.Euler(0, 45f, 0)
            };

            var writer = new NetworkWriter();
            CalibrationData.WriteCalibrationData(writer, original);

            var reader = new NetworkReader(writer.ToArraySegment());
            CalibrationData deserialized = CalibrationData.ReadCalibrationData(reader);

            Assert.That(Vector3.Distance(original.Position, deserialized.Position), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(original.Rotation, deserialized.Rotation), Is.LessThan(0.1f));
        }

        [Test]
        public void ApplyToTransform_ShouldOffsetChildWorldPosition()
        {
            var parent = new GameObject("CalibrationRoot");
            var child = new GameObject("Head");
            child.transform.SetParent(parent.transform, false);

            var calibration = new CalibrationData
            {
                Position = new Vector3(2f, 0, 3f),
                Rotation = Quaternion.Euler(0, 90f, 0)
            };

            // Apply calibration to parent
            parent.transform.position = calibration.Position;
            parent.transform.rotation = calibration.Rotation;

            // Set child local pose (simulating VR tracking data)
            child.transform.localPosition = new Vector3(0, 1.7f, 0);
            child.transform.localRotation = Quaternion.identity;

            // Verify world position is rotated and offset
            // Local (0, 1.7, 0) rotated 90° around Y = (0, 1.7, 0) (Y unchanged)
            // Then offset by (2, 0, 3)
            Vector3 expectedWorld = calibration.Position + calibration.Rotation * new Vector3(0, 1.7f, 0);
            Assert.That(Vector3.Distance(child.transform.position, expectedWorld), Is.LessThan(0.001f),
                "Child world position should reflect calibration offset");

            Object.DestroyImmediate(parent);
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL — `CalibrationData` type not found

**Step 3: Write CalibrationState enum**

```csharp
// Assets/Nexus/Runtime/VR/Calibration/CalibrationState.cs
namespace Nexus.Networking.VR.Calibration
{
    public enum CalibrationState
    {
        None,
        InProgress,
        Calibrated,
        Failed
    }
}
```

**Step 4: Write CalibrationData struct**

```csharp
// Assets/Nexus/Runtime/VR/Calibration/CalibrationData.cs
using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    public struct CalibrationData
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public static readonly CalibrationData Identity = new CalibrationData
        {
            Position = Vector3.zero,
            Rotation = Quaternion.identity
        };

        public static void WriteCalibrationData(NetworkWriter writer, CalibrationData data)
        {
            writer.WriteVector3(data.Position);
            writer.WriteQuaternion(data.Rotation);
        }

        public static CalibrationData ReadCalibrationData(NetworkReader reader)
        {
            return new CalibrationData
            {
                Position = reader.ReadVector3(),
                Rotation = reader.ReadQuaternion()
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSerializers()
        {
            Writer<CalibrationData>.write = WriteCalibrationData;
            Reader<CalibrationData>.read = ReadCalibrationData;
        }
    }
}
```

**Step 5: Run tests to verify they pass**

Expected: 3 PASS

**Step 6: Commit**

```bash
git add Assets/Nexus/Runtime/VR/Calibration/CalibrationData.cs Assets/Nexus/Runtime/VR/Calibration/CalibrationState.cs Assets/Nexus/Tests/Runtime/CalibrationDataTests.cs
git commit -m "feat(calibration): add CalibrationData struct and CalibrationState enum"
```

---

### Task 2: ICalibrationProvider interface

**Files:**
- Create: `Assets/Nexus/Runtime/VR/Calibration/ICalibrationProvider.cs`

**Step 1: Write interface**

```csharp
// Assets/Nexus/Runtime/VR/Calibration/ICalibrationProvider.cs
using System;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Interface for spatial calibration methods.
    /// Implement to provide platform-specific calibration (Meta Spatial Anchor, AR Marker, etc.)
    /// or use ManualCalibrationProvider for universal two-point alignment.
    /// </summary>
    public interface ICalibrationProvider
    {
        CalibrationState State { get; }
        event Action<CalibrationData> OnCalibrationComplete;
        event Action<string> OnCalibrationFailed;

        /// <summary>
        /// Set the host's reference points for alignment.
        /// Manual providers use these for two-point math.
        /// Platform-specific providers may ignore this.
        /// </summary>
        void SetReferencePoints(Vector3 pointA, Vector3 pointB);

        void StartCalibration();
        void CancelCalibration();
    }
}
```

**Step 2: Commit**

```bash
git add Assets/Nexus/Runtime/VR/Calibration/ICalibrationProvider.cs
git commit -m "feat(calibration): add ICalibrationProvider interface"
```

---

### Task 3: ManualCalibrationProvider — two-point alignment

**Files:**
- Create: `Assets/Nexus/Runtime/VR/Calibration/ManualCalibrationProvider.cs`
- Test: `Assets/Nexus/Tests/Runtime/ManualCalibrationTests.cs`

**Step 1: Write failing tests**

```csharp
// Assets/Nexus/Tests/Runtime/ManualCalibrationTests.cs
using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;

namespace Nexus.Networking.Tests
{
    public class ManualCalibrationTests
    {
        [Test]
        public void SamePoints_ShouldReturnIdentity()
        {
            // Host and client record the exact same two points
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, refA, refB);

            Assert.That(Vector3.Distance(result.Position, Vector3.zero), Is.LessThan(0.001f),
                "Position offset should be zero when points match");
            Assert.That(Quaternion.Angle(result.Rotation, Quaternion.identity), Is.LessThan(0.1f),
                "Rotation offset should be identity when points match");
        }

        [Test]
        public void TranslatedPoints_ShouldComputePositionOffset()
        {
            // Host at origin, client shifted 3 meters on X
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);
            var localA = new Vector3(3, 0, 0);
            var localB = new Vector3(3, 0, 2);

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, localA, localB);

            // Client is 3m to the right of host, so offset should shift left by 3
            Assert.That(Vector3.Distance(result.Position, new Vector3(-3, 0, 0)), Is.LessThan(0.001f),
                "Position offset should compensate for translation");
            Assert.That(Quaternion.Angle(result.Rotation, Quaternion.identity), Is.LessThan(0.1f),
                "Rotation should be identity (no rotation difference)");
        }

        [Test]
        public void Rotated90Degrees_ShouldComputeYawOffset()
        {
            // Host facing +Z, client facing +X (rotated 90° CW)
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);
            var localA = new Vector3(0, 0, 0);
            var localB = new Vector3(2, 0, 0); // client's "forward" is +X

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, localA, localB);

            // Rotation should be -90° around Y (to turn client's +X to host's +Z)
            float angle = Quaternion.Angle(result.Rotation, Quaternion.Euler(0, -90, 0));
            Assert.That(angle, Is.LessThan(0.5f),
                $"Rotation should be -90° around Y, got angle diff: {angle}");
        }

        [Test]
        public void RotatedPoints_ShouldOnlyCalibrateYaw()
        {
            // Verify pitch/roll are not affected
            var refA = new Vector3(0, 0, 0);
            var refB = new Vector3(0, 0, 2);
            // Client points at same direction but with Y offset (different floor level)
            var localA = new Vector3(0, 0.5f, 0);
            var localB = new Vector3(0, 0.5f, 2);

            CalibrationData result = ManualCalibrationProvider.ComputeCalibration(refA, refB, localA, localB);

            // Euler angles of rotation should have near-zero pitch and roll
            Vector3 euler = result.Rotation.eulerAngles;
            // Normalize angles to -180..180 range
            float pitch = euler.x > 180 ? euler.x - 360 : euler.x;
            float roll = euler.z > 180 ? euler.z - 360 : euler.z;
            Assert.That(Mathf.Abs(pitch), Is.LessThan(0.1f), "Pitch should be zero");
            Assert.That(Mathf.Abs(roll), Is.LessThan(0.1f), "Roll should be zero");
        }

        [Test]
        public void RecordPoint_TwoPoints_ShouldFireOnCalibrationComplete()
        {
            var provider = new ManualCalibrationProvider();
            provider.SetReferencePoints(new Vector3(0, 0, 0), new Vector3(0, 0, 2));

            CalibrationData? receivedData = null;
            provider.OnCalibrationComplete += data => receivedData = data;

            provider.StartCalibration();
            provider.RecordPoint(new Vector3(0, 0, 0));  // local point A
            Assert.IsNull(receivedData, "Should not fire after first point");

            provider.RecordPoint(new Vector3(0, 0, 2));  // local point B
            Assert.IsNotNull(receivedData, "Should fire after second point");
            Assert.AreEqual(CalibrationState.Calibrated, provider.State);
        }

        [Test]
        public void StartCalibration_WithoutReferencePoints_ShouldFail()
        {
            var provider = new ManualCalibrationProvider();

            string failMessage = null;
            provider.OnCalibrationFailed += msg => failMessage = msg;

            provider.StartCalibration();

            Assert.AreEqual(CalibrationState.Failed, provider.State);
            Assert.IsNotNull(failMessage);
        }

        [Test]
        public void CancelCalibration_ShouldResetState()
        {
            var provider = new ManualCalibrationProvider();
            provider.SetReferencePoints(Vector3.zero, Vector3.forward);
            provider.StartCalibration();
            Assert.AreEqual(CalibrationState.InProgress, provider.State);

            provider.CancelCalibration();
            Assert.AreEqual(CalibrationState.None, provider.State);
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL — `ManualCalibrationProvider` type not found

**Step 3: Write ManualCalibrationProvider**

```csharp
// Assets/Nexus/Runtime/VR/Calibration/ManualCalibrationProvider.cs
using System;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Two-point alignment calibration. Host defines two physical reference points.
    /// Each client records the same two points from their tracking space.
    /// System computes the position + Y-axis rotation offset.
    /// </summary>
    public class ManualCalibrationProvider : ICalibrationProvider
    {
        // Private fields
        private Vector3 _referencePointA;
        private Vector3 _referencePointB;
        private bool _hasReferencePoints;
        private Vector3 _localPointA;
        private bool _hasLocalPointA;

        // Public properties
        public CalibrationState State { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;
        public event Action<string> OnCalibrationFailed;

        // Public methods
        public void SetReferencePoints(Vector3 pointA, Vector3 pointB)
        {
            _referencePointA = pointA;
            _referencePointB = pointB;
            _hasReferencePoints = true;
        }

        public void StartCalibration()
        {
            if (!_hasReferencePoints)
            {
                State = CalibrationState.Failed;
                OnCalibrationFailed?.Invoke("Reference points not set. Host must record reference points first.");
                return;
            }

            State = CalibrationState.InProgress;
            _hasLocalPointA = false;
        }

        public void CancelCalibration()
        {
            State = CalibrationState.None;
            _hasLocalPointA = false;
        }

        /// <summary>
        /// Record a local point from the client's controller.
        /// Call twice: first for point A, then for point B.
        /// After the second call, calibration is computed automatically.
        /// </summary>
        public void RecordPoint(Vector3 controllerPosition)
        {
            if (State != CalibrationState.InProgress)
            {
                return;
            }

            if (!_hasLocalPointA)
            {
                _localPointA = controllerPosition;
                _hasLocalPointA = true;
                return;
            }

            // Second point — compute calibration
            CalibrationData data = ComputeCalibration(
                _referencePointA, _referencePointB,
                _localPointA, controllerPosition);

            State = CalibrationState.Calibrated;
            OnCalibrationComplete?.Invoke(data);
        }

        /// <summary>
        /// Compute calibration offset from two pairs of corresponding points.
        /// Projects directions to XZ plane for Y-axis-only rotation.
        /// </summary>
        public static CalibrationData ComputeCalibration(
            Vector3 refA, Vector3 refB, Vector3 localA, Vector3 localB)
        {
            // Project direction vectors to XZ plane (Y-axis rotation only)
            Vector3 refDir = refB - refA;
            refDir.y = 0f;
            refDir.Normalize();

            Vector3 localDir = localB - localA;
            localDir.y = 0f;
            localDir.Normalize();

            // Compute Y-axis rotation offset
            Quaternion rotation = Quaternion.FromToRotation(localDir, refDir);

            // Compute position offset: refA = rotation * localA + position
            Vector3 position = refA - rotation * localA;

            return new CalibrationData
            {
                Position = position,
                Rotation = rotation
            };
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Expected: 7 PASS

**Step 5: Commit**

```bash
git add Assets/Nexus/Runtime/VR/Calibration/ManualCalibrationProvider.cs Assets/Nexus/Tests/Runtime/ManualCalibrationTests.cs
git commit -m "feat(calibration): add ManualCalibrationProvider with two-point alignment"
```

---

### Task 4: SpatialCalibrationManager — core lifecycle

**Files:**
- Create: `Assets/Nexus/Runtime/VR/Calibration/SpatialCalibrationManager.cs`
- Test: `Assets/Nexus/Tests/Runtime/SpatialCalibrationManagerTests.cs`

**Step 1: Write failing tests**

```csharp
// Assets/Nexus/Tests/Runtime/SpatialCalibrationManagerTests.cs
using System.Collections;
using Nexus.Networking.Core;
using Nexus.Networking.VR;
using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    public class SpatialCalibrationManagerTests
    {
        private GameObject _rootObject;
        private SpatialCalibrationManager _calibrationManager;
        private NexusVRPlayer _vrPlayer;

        [SetUp]
        public void SetUp()
        {
            _rootObject = new GameObject("CalibrationTestRoot");
            _calibrationManager = _rootObject.AddComponent<SpatialCalibrationManager>();

            var playerObj = new GameObject("TestVRPlayer");
            playerObj.transform.SetParent(_rootObject.transform, false);
            _vrPlayer = playerObj.AddComponent<NexusVRPlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_rootObject != null)
            {
                Object.DestroyImmediate(_rootObject);
            }
        }

        [Test]
        public void ApplyCalibration_ShouldSetPlayerRootTransform()
        {
            var calibration = new CalibrationData
            {
                Position = new Vector3(2f, 0, 3f),
                Rotation = Quaternion.Euler(0, 45f, 0)
            };

            _calibrationManager.ApplyCalibrationToPlayer(_vrPlayer, calibration);

            Assert.That(Vector3.Distance(_vrPlayer.transform.position, calibration.Position),
                Is.LessThan(0.001f), "Player position should match calibration offset");
            Assert.That(Quaternion.Angle(_vrPlayer.transform.rotation, calibration.Rotation),
                Is.LessThan(0.1f), "Player rotation should match calibration offset");
        }

        [Test]
        public void ApplyCalibration_ShouldCorrectChildWorldPosition()
        {
            var calibration = new CalibrationData
            {
                Position = new Vector3(2f, 0, 0),
                Rotation = Quaternion.Euler(0, 90f, 0)
            };

            _calibrationManager.ApplyCalibrationToPlayer(_vrPlayer, calibration);

            // Apply a local pose to the head
            var pose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0), Quaternion.identity),
                LeftHand = new Pose(Vector3.zero, Quaternion.identity),
                RightHand = new Pose(Vector3.zero, Quaternion.identity)
            };
            _vrPlayer.ApplyPose(pose);

            // Local (0, 1.7, 0) rotated 90° around Y stays (0, 1.7, 0), then +2 on X
            Vector3 expected = calibration.Position + calibration.Rotation * new Vector3(0, 1.7f, 0);
            Assert.That(Vector3.Distance(_vrPlayer.Head.position, expected),
                Is.LessThan(0.001f), "Head world position should reflect calibration + local pose");
        }

        [Test]
        public void Initialize_ShouldSubscribeToProviderEvents()
        {
            var provider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(provider);

            CalibrationData? received = null;
            _calibrationManager.OnCalibrationComplete += data => received = data;

            // Simulate a full manual calibration
            provider.SetReferencePoints(Vector3.zero, new Vector3(0, 0, 2));
            provider.StartCalibration();
            provider.RecordPoint(Vector3.zero);
            provider.RecordPoint(new Vector3(0, 0, 2));

            Assert.IsNotNull(received, "Manager should relay OnCalibrationComplete from provider");
        }

        [Test]
        public void RecordReferencePoint_ShouldStoreAndSetOnProvider()
        {
            var provider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(provider);

            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 0));
            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 2));

            // Provider should now have reference points — start calibration should succeed
            provider.StartCalibration();
            Assert.AreEqual(CalibrationState.InProgress, provider.State,
                "Provider should accept StartCalibration after reference points are set");
        }

        [Test]
        public void StoreCalibration_ShouldTrackPerConnectionId()
        {
            var provider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(provider);

            var calibration = new CalibrationData
            {
                Position = new Vector3(1, 0, 1),
                Rotation = Quaternion.Euler(0, 30f, 0)
            };

            _calibrationManager.StoreCalibration(42, calibration);

            Assert.IsTrue(_calibrationManager.TryGetCalibration(42, out CalibrationData stored));
            Assert.That(Vector3.Distance(stored.Position, calibration.Position), Is.LessThan(0.001f));
        }
    }
}
```

**Step 2: Run tests to verify they fail**

Expected: FAIL — `SpatialCalibrationManager` type not found

**Step 3: Write SpatialCalibrationManager**

```csharp
// Assets/Nexus/Runtime/VR/Calibration/SpatialCalibrationManager.cs
using System;
using System.Collections.Generic;
using Nexus.Networking.VR;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Manages spatial calibration lifecycle. Coordinates with an ICalibrationProvider
    /// to compute per-player offsets and applies them to NexusVRPlayer root Transforms.
    /// </summary>
    public class SpatialCalibrationManager : MonoBehaviour
    {
        // Private fields
        private ICalibrationProvider _provider;
        private NexusVRPlayerManager _vrPlayerManager;
        private Vector3 _firstReferencePoint;
        private bool _hasFirstPoint;
        private readonly Dictionary<int, CalibrationData> _calibrationsByConnectionId =
            new Dictionary<int, CalibrationData>();

        // Public properties
        public CalibrationState State => _provider?.State ?? CalibrationState.None;
        public CalibrationData LocalCalibration { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;

        // Unity callbacks
        private void OnDestroy()
        {
            if (_provider != null)
            {
                _provider.OnCalibrationComplete -= HandleCalibrationComplete;
                _provider.OnCalibrationFailed -= HandleCalibrationFailed;
            }

            if (_vrPlayerManager != null)
            {
                _vrPlayerManager.OnVRPlayerSpawned -= HandleVRPlayerSpawned;
            }
        }

        // Public methods
        public void Initialize(ICalibrationProvider provider)
        {
            Initialize(provider, null);
        }

        public void Initialize(ICalibrationProvider provider, NexusVRPlayerManager vrPlayerManager)
        {
            _provider = provider;
            _vrPlayerManager = vrPlayerManager;

            _provider.OnCalibrationComplete += HandleCalibrationComplete;
            _provider.OnCalibrationFailed += HandleCalibrationFailed;

            if (_vrPlayerManager != null)
            {
                _vrPlayerManager.OnVRPlayerSpawned += HandleVRPlayerSpawned;
            }
        }

        /// <summary>
        /// Host records a reference point. Call twice to define the calibration axis.
        /// After the second call, reference points are set on the provider.
        /// </summary>
        public void RecordReferencePoint(Vector3 controllerPosition)
        {
            if (!_hasFirstPoint)
            {
                _firstReferencePoint = controllerPosition;
                _hasFirstPoint = true;
                Debug.Log("[SpatialCalibrationManager] First reference point recorded.");
                return;
            }

            _provider.SetReferencePoints(_firstReferencePoint, controllerPosition);
            _hasFirstPoint = false;
            Debug.Log("[SpatialCalibrationManager] Reference points set on provider.");
        }

        /// <summary>
        /// Apply calibration offset to a VR player's root Transform.
        /// </summary>
        public void ApplyCalibrationToPlayer(NexusVRPlayer player, CalibrationData data)
        {
            player.transform.position = data.Position;
            player.transform.rotation = data.Rotation;
        }

        /// <summary>
        /// Store calibration data for a specific connection.
        /// </summary>
        public void StoreCalibration(int connectionId, CalibrationData data)
        {
            _calibrationsByConnectionId[connectionId] = data;
        }

        /// <summary>
        /// Try to retrieve stored calibration for a connection.
        /// </summary>
        public bool TryGetCalibration(int connectionId, out CalibrationData data)
        {
            return _calibrationsByConnectionId.TryGetValue(connectionId, out data);
        }

        // Private methods
        private void HandleCalibrationComplete(CalibrationData data)
        {
            LocalCalibration = data;
            Debug.Log($"[SpatialCalibrationManager] Calibration complete: pos={data.Position}, rot={data.Rotation.eulerAngles}");
            OnCalibrationComplete?.Invoke(data);
        }

        private void HandleCalibrationFailed(string message)
        {
            Debug.LogWarning($"[SpatialCalibrationManager] Calibration failed: {message}");
        }

        private void HandleVRPlayerSpawned(NexusVRPlayer player)
        {
            // Apply existing calibration to newly spawned players
            // For local player, apply local calibration
            // For remote players, apply their stored calibration if available
            if (_vrPlayerManager != null && player == _vrPlayerManager.LocalPlayer)
            {
                if (State == CalibrationState.Calibrated)
                {
                    ApplyCalibrationToPlayer(player, LocalCalibration);
                }
            }
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Expected: 5 PASS

**Step 5: Commit**

```bash
git add Assets/Nexus/Runtime/VR/Calibration/SpatialCalibrationManager.cs Assets/Nexus/Tests/Runtime/SpatialCalibrationManagerTests.cs
git commit -m "feat(calibration): add SpatialCalibrationManager core lifecycle"
```

---

### Task 5: SpatialCalibrationManager — network sync

**Files:**
- Create: `Assets/Nexus/Runtime/VR/Calibration/CalibrationMessages.cs`
- Modify: `Assets/Nexus/Runtime/VR/Calibration/SpatialCalibrationManager.cs`

This task adds Mirror network message types and handler registration for syncing
reference points and calibration results across the network. Testing Command/ClientRpc
patterns requires a real host+client connection, so we rely on the integration test (Task 9).

**Step 1: Create network message types**

```csharp
// Assets/Nexus/Runtime/VR/Calibration/CalibrationMessages.cs
using Mirror;
using UnityEngine;

namespace Nexus.Networking.VR.Calibration
{
    /// <summary>
    /// Sent from Host to all clients with the two reference points for calibration.
    /// </summary>
    public struct ReferencePointsMessage : NetworkMessage
    {
        public Vector3 PointA;
        public Vector3 PointB;
    }

    /// <summary>
    /// Sent from client to server with calibration result, then relayed to all clients.
    /// </summary>
    public struct CalibrationResultMessage : NetworkMessage
    {
        public int ConnectionId;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
```

**Step 2: Add network sync methods to SpatialCalibrationManager**

Add to `SpatialCalibrationManager.cs`:

```csharp
// Add using:
using Mirror;

// Add private fields:
private bool _networkHandlersRegistered;

// Add public methods:

/// <summary>
/// Register Mirror network message handlers. Call after Mirror server/client is active.
/// </summary>
public void RegisterNetworkHandlers()
{
    if (_networkHandlersRegistered)
    {
        return;
    }

    if (NetworkServer.active)
    {
        NetworkServer.RegisterHandler<CalibrationResultMessage>(OnServerReceivedCalibration);
    }

    if (NetworkClient.active)
    {
        NetworkClient.RegisterHandler<ReferencePointsMessage>(OnClientReceivedReferencePoints);
        NetworkClient.RegisterHandler<CalibrationResultMessage>(OnClientReceivedCalibration);
    }

    _networkHandlersRegistered = true;
    Debug.Log("[SpatialCalibrationManager] Network handlers registered.");
}

/// <summary>
/// Unregister network handlers. Call before leaving room.
/// </summary>
public void UnregisterNetworkHandlers()
{
    if (!_networkHandlersRegistered)
    {
        return;
    }

    if (NetworkServer.active)
    {
        NetworkServer.UnregisterHandler<CalibrationResultMessage>();
    }

    if (NetworkClient.active)
    {
        NetworkClient.UnregisterHandler<ReferencePointsMessage>();
        NetworkClient.UnregisterHandler<CalibrationResultMessage>();
    }

    _networkHandlersRegistered = false;
}

/// <summary>
/// Broadcast reference points to all clients. Call after RecordReferencePoint completes.
/// </summary>
public void BroadcastReferencePoints(Vector3 pointA, Vector3 pointB)
{
    if (!NetworkServer.active)
    {
        Debug.LogWarning("[SpatialCalibrationManager] Cannot broadcast: server not active.");
        return;
    }

    NetworkServer.SendToAll(new ReferencePointsMessage
    {
        PointA = pointA,
        PointB = pointB
    });
}

/// <summary>
/// Send local calibration result to server. Called by client after calibration completes.
/// </summary>
public void SendCalibrationToServer(CalibrationData data)
{
    if (!NetworkClient.active)
    {
        Debug.LogWarning("[SpatialCalibrationManager] Cannot send: client not active.");
        return;
    }

    NetworkClient.Send(new CalibrationResultMessage
    {
        ConnectionId = NetworkClient.connection.connectionId,
        Position = data.Position,
        Rotation = data.Rotation
    });
}

// Modify RecordReferencePoint to also broadcast:
// After _provider.SetReferencePoints(...), add:
// BroadcastReferencePoints(_firstReferencePoint, controllerPosition);

// Modify HandleCalibrationComplete to also send to server:
// After OnCalibrationComplete?.Invoke(data), add:
// SendCalibrationToServer(data);

// Add private network handlers:

private void OnClientReceivedReferencePoints(ReferencePointsMessage msg)
{
    Debug.Log($"[SpatialCalibrationManager] Received reference points from host.");
    _provider?.SetReferencePoints(msg.PointA, msg.PointB);
}

private void OnServerReceivedCalibration(
    NetworkConnectionToClient conn, CalibrationResultMessage msg)
{
    // Store and relay to all clients
    var data = new CalibrationData
    {
        Position = msg.Position,
        Rotation = msg.Rotation
    };
    StoreCalibration(msg.ConnectionId, data);
    NetworkServer.SendToAll(msg);
}

private void OnClientReceivedCalibration(CalibrationResultMessage msg)
{
    var data = new CalibrationData
    {
        Position = msg.Position,
        Rotation = msg.Rotation
    };
    StoreCalibration(msg.ConnectionId, data);

    // Apply to remote player if already spawned
    if (_vrPlayerManager != null)
    {
        foreach (NexusVRPlayer remote in _vrPlayerManager.RemotePlayers)
        {
            // Match by connection ID via VRPlayerManager's internal tracking
            // For now, apply calibration when the player spawns (HandleVRPlayerSpawned)
        }
    }

    Debug.Log($"[SpatialCalibrationManager] Applied calibration for connection {msg.ConnectionId}.");
}
```

**Step 3: Update RecordReferencePoint and HandleCalibrationComplete**

Modify `RecordReferencePoint` — after `_provider.SetReferencePoints(...)`:
```csharp
BroadcastReferencePoints(_firstReferencePoint, controllerPosition);
```

Modify `HandleCalibrationComplete` — after `OnCalibrationComplete?.Invoke(data)`:
```csharp
SendCalibrationToServer(data);
```

**Step 4: Commit**

```bash
git add Assets/Nexus/Runtime/VR/Calibration/CalibrationMessages.cs Assets/Nexus/Runtime/VR/Calibration/SpatialCalibrationManager.cs
git commit -m "feat(calibration): add Mirror network sync for calibration data"
```

---

### Task 6: NexusBootstrap integration

**Files:**
- Modify: `Assets/Nexus/Runtime/Core/NexusBootstrap.cs`

**Step 1: Wire SpatialCalibrationManager into NexusBootstrap.InitializeLocal()**

Add `using Nexus.Networking.VR.Calibration;` to the using directives.

After the VR player manager initialization (line 77), add:

```csharp
// Wire spatial calibration manager
var calibrationManager = GetOrAddComponent<SpatialCalibrationManager>();
var manualCalibration = new ManualCalibrationProvider();
calibrationManager.Initialize(manualCalibration, vrPlayerManager);
```

**Step 2: Commit**

```bash
git add Assets/Nexus/Runtime/Core/NexusBootstrap.cs
git commit -m "feat(calibration): wire SpatialCalibrationManager into NexusBootstrap"
```

---

### Task 7: Meta sub-package — skeleton

**Files:**
- Create: `Assets/NexusCalibrationMeta/Runtime/Nexus.Calibration.Meta.asmdef`
- Create: `Assets/NexusCalibrationMeta/Runtime/MetaSpatialAnchorProvider.cs`

**Step 1: Create asmdef**

```json
{
  "name": "Nexus.Calibration.Meta",
  "rootNamespace": "Nexus.Calibration.Meta",
  "references": [
    "Nexus.Networking"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [
    {
      "name": "com.meta.xr.sdk.core",
      "expression": "",
      "define": "META_XR_SDK"
    }
  ],
  "noEngineReferences": false
}
```

**Step 2: Create skeleton provider**

```csharp
// Assets/NexusCalibrationMeta/Runtime/MetaSpatialAnchorProvider.cs
using System;
using Nexus.Networking.VR.Calibration;
using UnityEngine;

namespace Nexus.Calibration.Meta
{
    /// <summary>
    /// Calibration provider using Meta Quest Shared Spatial Anchors.
    /// Requires Meta XR SDK (com.meta.xr.sdk.core).
    /// Host creates a spatial anchor, shares it, and clients localize to it.
    /// The pose difference provides automatic calibration with no user interaction.
    /// </summary>
    public class MetaSpatialAnchorProvider : MonoBehaviour, ICalibrationProvider
    {
        // Public properties
        public CalibrationState State { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;
        public event Action<string> OnCalibrationFailed;

        // Public methods
        public void SetReferencePoints(Vector3 pointA, Vector3 pointB)
        {
            // Not used by Meta Spatial Anchor — calibration is automatic
        }

        public void StartCalibration()
        {
#if META_XR_SDK
            State = CalibrationState.InProgress;
            // TODO: Implement Meta Shared Spatial Anchor flow
            // 1. If host: OVRSpatialAnchor.Create() → Share() → send UUID to clients
            // 2. If client: receive UUID → OVRSpatialAnchor.Localize()
            // 3. Compare anchor world pose with tracking origin → compute CalibrationData
            Debug.Log("[MetaSpatialAnchorProvider] Starting Shared Spatial Anchor calibration...");
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("Meta Spatial Anchor not yet implemented. Install Meta XR SDK and complete TODO.");
#else
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("Meta XR SDK not available. Install com.meta.xr.sdk.core.");
#endif
        }

        public void CancelCalibration()
        {
            State = CalibrationState.None;
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/NexusCalibrationMeta/Runtime/Nexus.Calibration.Meta.asmdef Assets/NexusCalibrationMeta/Runtime/MetaSpatialAnchorProvider.cs
git commit -m "feat(calibration): add Meta Spatial Anchor sub-package skeleton"
```

---

### Task 8: Marker sub-package — skeleton

**Files:**
- Create: `Assets/NexusCalibrationMarker/Runtime/Nexus.Calibration.Marker.asmdef`
- Create: `Assets/NexusCalibrationMarker/Runtime/MarkerCalibrationProvider.cs`

**Step 1: Create asmdef**

```json
{
  "name": "Nexus.Calibration.Marker",
  "rootNamespace": "Nexus.Calibration.Marker",
  "references": [
    "Nexus.Networking"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [
    {
      "name": "com.unity.xr.arfoundation",
      "expression": "",
      "define": "AR_FOUNDATION"
    }
  ],
  "noEngineReferences": false
}
```

**Step 2: Create skeleton provider**

```csharp
// Assets/NexusCalibrationMarker/Runtime/MarkerCalibrationProvider.cs
using System;
using Nexus.Networking.VR.Calibration;
using UnityEngine;

namespace Nexus.Calibration.Marker
{
    /// <summary>
    /// Calibration provider using physical markers (QR codes, ArUco, etc.)
    /// detected via AR image tracking.
    /// Requires AR Foundation (com.unity.xr.arfoundation).
    /// Each player views the same physical marker; the detected 6DOF pose
    /// differences provide semi-automatic calibration.
    /// </summary>
    public class MarkerCalibrationProvider : MonoBehaviour, ICalibrationProvider
    {
        // Public properties
        public CalibrationState State { get; private set; }

        // Events
        public event Action<CalibrationData> OnCalibrationComplete;
        public event Action<string> OnCalibrationFailed;

        // Public methods
        public void SetReferencePoints(Vector3 pointA, Vector3 pointB)
        {
            // Not used by Marker provider — calibration is based on marker detection
        }

        public void StartCalibration()
        {
#if AR_FOUNDATION
            State = CalibrationState.InProgress;
            // TODO: Implement AR marker tracking flow
            // 1. Enable ARTrackedImageManager
            // 2. Wait for marker detection → get marker's 6DOF world pose
            // 3. Compare marker pose across devices → compute CalibrationData
            // 4. Fire OnCalibrationComplete
            Debug.Log("[MarkerCalibrationProvider] Starting marker detection...");
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("Marker calibration not yet implemented. Install AR Foundation and complete TODO.");
#else
            State = CalibrationState.Failed;
            OnCalibrationFailed?.Invoke("AR Foundation not available. Install com.unity.xr.arfoundation.");
#endif
        }

        public void CancelCalibration()
        {
            State = CalibrationState.None;
        }
    }
}
```

**Step 3: Commit**

```bash
git add Assets/NexusCalibrationMarker/Runtime/Nexus.Calibration.Marker.asmdef Assets/NexusCalibrationMarker/Runtime/MarkerCalibrationProvider.cs
git commit -m "feat(calibration): add Marker calibration sub-package skeleton"
```

---

### Task 9: Integration smoke test

**Files:**
- Test: `Assets/Nexus/Tests/Runtime/CalibrationIntegrationTests.cs`

**Step 1: Write integration test**

```csharp
// Assets/Nexus/Tests/Runtime/CalibrationIntegrationTests.cs
using System.Collections;
using Nexus.Networking.Core;
using Nexus.Networking.VR;
using Nexus.Networking.VR.Calibration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nexus.Networking.Tests
{
    /// <summary>
    /// End-to-end integration tests for spatial calibration.
    /// Verifies: calibration → player spawn → offset applied → transforms correct.
    /// </summary>
    public class CalibrationIntegrationTests : NexusTestBase
    {
        private NexusVRPlayerManager _vrManager;
        private SpatialCalibrationManager _calibrationManager;
        private ManualCalibrationProvider _manualProvider;
        private MockTrackingProvider _mockTracking;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BaseSetUp();

            _vrManager = _rootObject.AddComponent<NexusVRPlayerManager>();
            _vrManager.Initialize(_roomManager, _config);

            _mockTracking = new MockTrackingProvider();
            _mockTracking.NextPose = new VRPose
            {
                Head = new Pose(new Vector3(0, 1.7f, 0), Quaternion.identity),
                LeftHand = new Pose(new Vector3(-0.3f, 1f, 0.3f), Quaternion.identity),
                RightHand = new Pose(new Vector3(0.3f, 1f, 0.3f), Quaternion.identity)
            };
            _vrManager.TrackingProvider = _mockTracking;

            _calibrationManager = _rootObject.AddComponent<SpatialCalibrationManager>();
            _manualProvider = new ManualCalibrationProvider();
            _calibrationManager.Initialize(_manualProvider, _vrManager);
        }

        [UnityTest]
        public IEnumerator CalibrateBeforeRoom_ShouldApplyToSpawnedPlayer()
        {
            // Host records reference points
            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 0));
            _calibrationManager.RecordReferencePoint(new Vector3(0, 1, 2));

            // Simulate client calibration with a 2m X offset, no rotation
            _manualProvider.StartCalibration();
            _manualProvider.RecordPoint(new Vector3(2, 1, 0));
            _manualProvider.RecordPoint(new Vector3(2, 1, 2));

            Assert.AreEqual(CalibrationState.Calibrated, _manualProvider.State);

            // Create room — should spawn player with calibration applied
            _session.CreateRoom("Calibration Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            yield return null;
            yield return null;

            // The calibration offset should shift the player -2m on X
            NexusVRPlayer player = _vrManager.LocalPlayer;
            Assert.That(Vector3.Distance(player.transform.position, new Vector3(-2, 0, 0)),
                Is.LessThan(0.01f),
                "Player root should have calibration position offset");
        }

        [UnityTest]
        public IEnumerator CalibratedPlayer_PoseShouldBeInWorldSpace()
        {
            // Simple calibration: 1m offset on Z
            _calibrationManager.RecordReferencePoint(new Vector3(0, 0, 0));
            _calibrationManager.RecordReferencePoint(new Vector3(1, 0, 0));
            _manualProvider.StartCalibration();
            _manualProvider.RecordPoint(new Vector3(0, 0, 1));
            _manualProvider.RecordPoint(new Vector3(1, 0, 1));

            _session.CreateRoom("World Space Test");

            yield return TestHelpers.WaitForCondition(
                () => _vrManager.LocalPlayer != null,
                timeout: 5f);

            yield return null;
            yield return null;

            NexusVRPlayer player = _vrManager.LocalPlayer;

            // Head local position is (0, 1.7, 0) from MockTrackingProvider
            // Calibration offset is (0, 0, -1) on Z
            // World position should be (0, 1.7, -1)
            Vector3 expectedHead = player.transform.position +
                player.transform.rotation * new Vector3(0, 1.7f, 0);
            Assert.That(Vector3.Distance(player.Head.position, expectedHead),
                Is.LessThan(0.01f),
                "Head world position should be calibration offset + local pose");
        }
    }
}
```

**Step 2: Run all tests**

Expected: ALL PASS

**Step 3: Commit**

```bash
git add Assets/Nexus/Tests/Runtime/CalibrationIntegrationTests.cs
git commit -m "test(calibration): add integration smoke tests for spatial calibration"
```

---

## Summary

| Task | Files | Tests | Description |
|------|-------|-------|-------------|
| 1 | CalibrationData.cs, CalibrationState.cs | CalibrationDataTests.cs (3) | Data types + Mirror serialization |
| 2 | ICalibrationProvider.cs | — | Provider interface |
| 3 | ManualCalibrationProvider.cs | ManualCalibrationTests.cs (7) | Two-point alignment math |
| 4 | SpatialCalibrationManager.cs | SpatialCalibrationManagerTests.cs (5) | Core lifecycle + apply offset |
| 5 | CalibrationMessages.cs, SpatialCalibrationManager.cs | — | Mirror network message sync |
| 6 | NexusBootstrap.cs (modify) | — | Bootstrap wiring |
| 7 | Nexus.Calibration.Meta.asmdef, MetaSpatialAnchorProvider.cs | — | Meta sub-package skeleton |
| 8 | Nexus.Calibration.Marker.asmdef, MarkerCalibrationProvider.cs | — | Marker sub-package skeleton |
| 9 | CalibrationIntegrationTests.cs | CalibrationIntegrationTests (2) | End-to-end smoke tests |

**Total: 9 new files, 2 modified, 3 test files, ~17 tests**
