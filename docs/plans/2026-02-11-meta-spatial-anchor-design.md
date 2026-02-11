# Meta Spatial Anchor Calibration Design

> Date: 2026-02-11
> Status: Implemented
> Parent: spatial-calibration-design.md

## Overview

Implements `MetaSpatialAnchorProvider` using Meta Quest Shared Spatial Anchors with
Group-based sharing. Host creates a spatial anchor and shares it to a group; clients
load and localize the same anchor to compute calibration offsets automatically.

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Sharing method | Group-based (`ShareAsync(Guid)`) | No Oculus user IDs needed, fits LAN co-located scenario |
| Provider interface | `IAnchorCalibrationProvider` extends `ICalibrationProvider` | Clean extension, reusable for Marker provider later |
| Network transport | New `AnchorShareMessage` via Mirror | Carries anchor UUID, group UUID, and host pose |
| Rotation calibration | Y-axis only | Consistent with ManualCalibrationProvider |
| Retry on load | 3 retries, 2s delay | Cloud propagation delay for shared anchors |

## Architecture

### New Interface

```csharp
public interface IAnchorCalibrationProvider : ICalibrationProvider
{
    event Action<AnchorShareData> OnAnchorShared;
    void LoadSharedAnchor(AnchorShareData data);
}

public struct AnchorShareData
{
    public Guid AnchorUuid;
    public Guid GroupUuid;
    public Vector3 HostPosition;
    public Quaternion HostRotation;
}
```

### Flow

```
Host: StartCalibration()
  -> OVRSpatialAnchor.Create + WhenLocalizedAsync
  -> ShareAsync(groupUuid)
  -> Fire OnAnchorShared
  -> SpatialCalibrationManager broadcasts AnchorShareMessage

Client: receives AnchorShareMessage
  -> SpatialCalibrationManager calls LoadSharedAnchor()
  -> LoadUnboundSharedAnchorsAsync(groupUuid, [anchorUuid])
  -> LocalizeAsync(timeout)
  -> BindTo(OVRSpatialAnchor)
  -> ComputeOffset(hostPose, clientPose) -> Y-axis only
  -> Fire OnCalibrationComplete
  -> SpatialCalibrationManager sends CalibrationResultMessage
```

## Files Changed

| File | Change |
|------|--------|
| `Assets/Nexus/Runtime/VR/Calibration/IAnchorCalibrationProvider.cs` | New |
| `Assets/Nexus/Runtime/VR/Calibration/CalibrationMessages.cs` | Added AnchorShareMessage |
| `Assets/Nexus/Runtime/VR/Calibration/SpatialCalibrationManager.cs` | Anchor provider support |
| `Assets/NexusCalibrationMeta/Runtime/MetaSpatialAnchorProvider.cs` | Full implementation |
| `Assets/NexusCalibrationMeta/Runtime/Nexus.Calibration.Meta.asmdef` | Added Oculus.VR reference |
