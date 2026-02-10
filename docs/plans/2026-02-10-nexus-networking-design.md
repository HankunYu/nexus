# Nexus — VR Multiplayer Networking Framework Design

> Date: 2026-02-10
> Status: Approved
> Project: com.nexus.networking (UPM Package)

## Overview

Unity 6 UPM package providing VR multiplayer networking for Quest + PCVR.
Built on Mirror + KCP Transport, supporting 4-8 player rooms.
Core design: abstraction layer with Local/Remote swappable implementations.

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Code structure | UPM Package | Reusable across projects |
| VR platform | Quest + PCVR | Cross-platform requirement |
| Room size | 4-8 players | Medium rooms, Host-Client + optional Dedicated |
| Priority | LAN first | Faster iteration, extend to remote later |
| Networking lib | Mirror | Mature, Unity-native, good VR support |
| LAN transport | KCP | Low latency UDP, ideal for LAN |
| Remote transport | SimpleWebTransport | WebSocket-based, firewall-friendly |

---

## Package Structure

```
Packages/com.nexus.networking/
├── package.json
├── Runtime/
│   ├── Nexus.Networking.asmdef
│   ├── Core/
│   │   ├── INexusTransport.cs
│   │   ├── INexusDiscovery.cs
│   │   ├── INexusRoomManager.cs
│   │   ├── NexusSession.cs
│   │   ├── NexusConfig.cs
│   │   ├── NexusMode.cs
│   │   ├── RoomInfo.cs
│   │   ├── RoomConfig.cs
│   │   ├── RoomState.cs
│   │   └── NexusPlayer.cs
│   ├── Local/
│   │   ├── LocalTransport.cs
│   │   ├── LanDiscovery.cs
│   │   ├── LocalRoomManager.cs
│   │   └── ReconnectHandler.cs
│   ├── Remote/
│   │   ├── RemoteTransport.cs
│   │   ├── RemoteRoomManager.cs
│   │   ├── RelayClient.cs
│   │   ├── MatchmakingApi.cs
│   │   └── RemoteDiscovery.cs
│   ├── VR/
│   │   ├── VRPlayerSync.cs
│   │   ├── VRNetworkPlayer.cs
│   │   ├── SpatialAnchor.cs
│   │   ├── SpatialCalibration.cs
│   │   └── TransformCompressor.cs
│   └── Utils/
│       └── ReconnectConfig.cs
├── Editor/
│   └── Nexus.Networking.Editor.asmdef
├── Samples~/
│   └── VRMultiplayerDemo/
│       ├── Scenes/
│       ├── Prefabs/
│       ├── Scripts/
│       └── Materials/
└── Tests/
    ├── Runtime/
    └── Editor/
```

---

## Sprint Roadmap

```
S1 Foundation ──▶ S2 LAN ──▶ S3 VR Sync ──▶ S4 Remote ──▶ S5 Demo
```

---

## Sprint 1 — Foundation

**Tasks:**
- Network abstraction layer design (Core interfaces)
- Mirror integration + KCP Transport configuration
- UPM package skeleton

### Core Interfaces

```csharp
public interface INexusTransport
{
    void StartHost();
    void StartClient(string address);
    void StartServer();
    void Stop();
    bool IsActive { get; }
    NetworkMode Mode { get; }
}

public interface INexusDiscovery
{
    void StartBroadcast(RoomInfo room);
    void StartListening();
    void Stop();
    event Action<RoomInfo> OnRoomFound;
    event Action<RoomInfo> OnRoomLost;
}

public interface INexusRoomManager
{
    void CreateRoom(RoomConfig config);
    void JoinRoom(RoomInfo room);
    void LeaveRoom();
    RoomState CurrentState { get; }
    event Action<NexusPlayer> OnPlayerJoined;
    event Action<NexusPlayer> OnPlayerLeft;
}
```

### NexusSession State Machine

```
Idle ──▶ Creating ──▶ Hosting ──▶ InRoom
  │                                  │
  └──▶ Joining ──▶ Connected ──▶ InRoom
                                     │
                              Disconnected ──▶ Reconnecting ──▶ InRoom
                                     │
                                    Idle
```

NexusSession is the central hub. It holds references to the current Transport /
Discovery / RoomManager and injects Local or Remote implementations based on
NexusConfig.Mode.

### Mirror Integration

Mirror is wrapped internally, never exposed to consumers:
- LocalTransport holds Mirror's NetworkManager + KcpTransport
- NexusSession.StartHost() → LocalTransport.StartHost() → Mirror.NetworkManager.StartHost()
- Upper layers only interact with INexusTransport

### Output Files

Core/: INexusTransport.cs, INexusDiscovery.cs, INexusRoomManager.cs,
NexusSession.cs, NexusConfig.cs, NexusMode.cs, RoomInfo.cs, RoomConfig.cs,
RoomState.cs, NexusPlayer.cs

Local/: LocalTransport.cs

### Milestone

Two devices establish a Mirror/KCP connection on LAN and exchange messages.

---

## Sprint 2 — LAN Networking

**Tasks:**
- LAN room auto-discovery (UDP broadcast)
- Local connection management (join/leave/reconnect)

### LAN Discovery

- Based on Mirror's NetworkDiscovery, wrapped behind INexusDiscovery
- Broadcast RoomInfo every 1s on UDP port 47777
- Rooms that stop broadcasting trigger OnRoomLost after timeout
- Fallback: mDNS for routers that block UDP broadcast

### Connection Management

- LocalRoomManager implements INexusRoomManager
- Host: StartHost() + StartBroadcast() + manage player list
- Client: StartClient(addr) + monitor disconnection

### Reconnection Strategy

- Client detects disconnect → enters Reconnecting state
- Auto-retry 3 times with exponential backoff (2s / 4s / 8s)
- Reconnect carries original playerId for state recovery
- Exceeds retry limit → fires OnDisconnected event

### Output Files

Local/: LanDiscovery.cs, LocalRoomManager.cs, ReconnectHandler.cs
Core/: ReconnectConfig.cs

### Milestone

Devices on same WiFi auto-discover rooms → join → disconnect WiFi →
auto-reconnect and recover.

---

## Sprint 3 — VR Sync

**Tasks:**
- VR player state sync framework (head + hands Transform)
- Multi-player spatial calibration (coordinate alignment / anchor system)

### VR Player Sync

Each VR player syncs 3 Transforms: head + left hand + right hand.

| Aspect | Approach |
|--------|----------|
| Sync rate | Configurable, default 30Hz |
| Data | Vector3 position + Quaternion rotation × 3 nodes |
| Compression | Quaternion smallest-three (3 × float16), Position half-precision |
| Interpolation | Double-buffer interpolation on proxy side |
| Bandwidth | ~54 bytes/frame × 30Hz ≈ 1.6 KB/s per player, 8 players ≈ 12.8 KB/s |

VRNetworkPlayer extends Mirror's NetworkBehaviour — the only place Mirror
is directly used by VR sync code.

### Spatial Calibration

Problem: each VR player has a different world origin (their physical room center).

Solution — shared anchor alignment:
1. Host places a calibration anchor (e.g., table center)
2. Each Client identifies the same anchor in their physical space
3. Framework computes offset: offsetMatrix = localAnchor.inverse * sharedAnchor
4. All sync data is transformed through this offset before sending

### Output Files

VR/: VRPlayerSync.cs, VRNetworkPlayer.cs, SpatialAnchor.cs,
SpatialCalibration.cs, TransformCompressor.cs

### Milestone

Two VR devices on LAN, complete spatial calibration, see each other's head
and hands moving in real-time with correct alignment.

---

## Sprint 4 — Remote Networking

**Tasks:**
- Remote Transport (Mirror + SimpleWebTransport)
- Self-hosted Relay Server (headless Mirror)
- Remote room/matchmaking system

### Network Topology

All players are Clients connecting to a cloud Relay Server (Dedicated Server
mode). This differs from LAN where one player is the Host.

### Relay Server

Headless Mirror instance, no rendering, no XR:
- Unity headless build → Linux Server → Docker container
- Each process supports multiple rooms (~50 rooms of 4-8 players)
- Exposes WebSocket port (SimpleWebTransport) + HTTP port (matchmaking API)

### Matchmaking Flow

```
Client                         Relay Server
  │── POST /rooms/create ─────▶  Create room, return roomId
  │── GET  /rooms/list ────────▶  Get room list
  │── POST /rooms/{id}/join ───▶  Join room, return WebSocket address
  │══ WebSocket connection ═════  Mirror SimpleWebTransport
```

### Output Files

Remote/: RemoteTransport.cs, RemoteRoomManager.cs, RelayClient.cs,
MatchmakingApi.cs, RemoteDiscovery.cs

relay-server/: RelayServer.cs, RoomInstance.cs, MatchmakingService.cs,
Dockerfile

### Milestone

Deploy Relay Server to VPS. Two devices on different networks connect via
room matching, reuse Sprint 3 VR sync to see each other.

---

## Sprint 5 — Integration Demo

**Tasks:**
- VR multiplayer test Demo

### Demo Flow

App Start → Main Menu → Choose Local/Remote → Scan/Query Rooms →
Create or Join → Calibration → VR Multiplayer Room

### Demo Components

| Component | Purpose |
|-----------|---------|
| DemoScene.unity | Simple VR room (floor + table + ambient light) |
| DemoUI.cs | World-space UI — mode selection, room list, create/join |
| DemoPlayerVisual.cs | Simple visual for remote players (sphere head + capsule hands) |
| DemoCalibration.cs | Guided calibration flow |
| DemoNetworkHUD.cs | Connection status, latency, player count |

### Output Files

Samples~/VRMultiplayerDemo/: Scenes/, Prefabs/, Scripts/, Materials/

### Final Milestone

1. LAN mode: two VR devices same WiFi → discover → join → calibrate → see each other
2. Remote mode: two VR devices different networks → match → join → calibrate → see each other
3. Reconnect: disconnect WiFi → auto-reconnect → state recovery
