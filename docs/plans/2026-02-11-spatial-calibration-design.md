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

```csharp
public class SpatialCalibrationManager : NetworkBehaviour
{
    private ICalibrationProvider _provider;

    public CalibrationState State { get; }
    public CalibrationData LocalCalibration { get; }
    public event Action<CalibrationData> OnCalibrationComplete;

    public void Initialize(ICalibrationProvider provider);

    // Host: record reference points and broadcast
    public void RecordReferencePoint(Vector3 controllerPosition);
    [ClientRpc] private void RpcSetReferencePoints(Vector3 a, Vector3 b);

    // Client: report calibration result
    [Command] private void CmdReportCalibration(CalibrationData data);
    [ClientRpc] private void RpcApplyCalibration(int connectionId, CalibrationData data);

    // Apply offset to NexusVRPlayer.transform
    private void ApplyCalibrationToPlayer(NexusVRPlayer player, CalibrationData data);
}
```

**Network Flow:**
```
Host: RecordReferencePoint(A) → RecordReferencePoint(B)
  ↓ RpcSetReferencePoints(A, B)
Client: receives reference points → StartCalibration()
  ↓ user completes calibration
Client: CmdReportCalibration(data)
  ↓ Host forwards
All: RpcApplyCalibration(connId, data) → set VRPlayer.transform
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
