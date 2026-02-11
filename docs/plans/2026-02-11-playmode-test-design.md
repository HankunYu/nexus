# Nexus Play Mode Test Design

## Overview

Play Mode integration tests for the Nexus VR networking framework.
Single-process loopback via Mirror Host mode (server + client coexist in one process).

## Test Structure

```
Assets/Nexus/Tests/Runtime/
├── Nexus.Networking.Tests.asmdef
├── NexusTestBase.cs
├── TestHelpers.cs
├── SessionLifecycleTests.cs
├── RoomDiscoveryTests.cs
├── PlayerManagementTests.cs
└── ReconnectTests.cs
```

## Assembly Definition

`Nexus.Networking.Tests.asmdef`:
- Type: Play Mode test assembly
- References: Nexus.Networking, Mirror, Mirror.Transports, UnityEngine.TestRunner, UnityEditor.TestRunner
- Root Namespace: Nexus.Networking.Tests

## NexusTestBase

Shared base class for all test fixtures.

**[SetUp] responsibilities:**
- Create root GameObject
- Add Mirror NetworkManager + KcpTransport
- Set KCP as active transport
- Add Nexus components: LocalTransport, LanDiscovery, LocalRoomManager, ReconnectHandler
- Create NexusConfig with short timeouts for fast tests:
  - RoomTimeoutSeconds: 1-2s
  - ReconnectBaseDelay: 0.2s
  - MaxReconnectAttempts: 2
- Call NexusSession.Initialize(transport, discovery, roomManager)

**[TearDown] responsibilities:**
- Call session.Shutdown()
- Stop all networking (transport, discovery)
- Destroy all test GameObjects
- Reset Mirror singleton state (NetworkManager, Transport.active)
- Small yield to let Unity clean up

## TestHelpers

Utility methods shared across tests:

- `WaitForCondition(Func<bool> condition, float timeout = 5f)` → IEnumerator that yields until condition is true or timeout
- `WaitForEvent<T>(Action<T> subscribe, Action<T> unsubscribe, float timeout)` → IEnumerator that captures event args
- `WaitForSeconds(float seconds)` → wrapper around WaitForSecondsRealtime
- `CreateNexusConfig(overrides)` → quick ScriptableObject.CreateInstance with test-friendly defaults

## Test Cases

### 1. SessionLifecycleTests (8 tests)

Tests NexusSession state machine transitions.

```
1.1 InitialState_ShouldBeIdle
    → After Initialize(), session.CurrentState == SessionState.Idle

1.2 CreateRoom_ShouldTransitionToInRoom
    → CreateRoom() → wait → state passes through Creating → Hosting → InRoom

1.3 CreateRoom_ShouldPopulateRoomInfo
    → CreateRoom("Test Room") → CurrentRoom.RoomName == "Test Room"
    → CurrentRoom.MaxPlayers matches config value

1.4 LeaveRoom_FromHosting_ShouldReturnToIdle
    → CreateRoom() → wait InRoom → LeaveRoom() → wait Idle

1.5 LeaveRoom_ShouldStopTransport
    → CreateRoom() → wait InRoom → LeaveRoom() → Transport.IsActive == false

1.6 Shutdown_ShouldCleanupEverything
    → CreateRoom() → wait InRoom → Shutdown() → State == Idle
    → Transport, Discovery, RoomManager all stopped

1.7 CreateRoom_WhenAlreadyInRoom_ShouldNotCrash
    → Already InRoom → CreateRoom() again → no exception, state unchanged

1.8 DoubleInitialize_ShouldBeIdempotent
    → Initialize() twice → no exception, state remains valid
```

### 2. RoomDiscoveryTests (6 tests)

Tests LanDiscovery UDP broadcast and timeout.
Requires multiple LanDiscovery instances on separate GameObjects.
Use short RoomTimeoutSeconds (1-2s) to speed up timeout tests.

```
2.1 StartBroadcast_ShouldMakeDiscoveryActive
    → Host creates room → StartBroadcast(roomInfo) → discovery.IsActive == true

2.2 StartListening_ShouldDiscoverBroadcastingRoom
    → Host broadcasts → second LanDiscovery StartListening()
    → wait OnRoomFound → received RoomInfo.RoomName matches

2.3 DiscoveredRoom_ShouldContainCorrectInfo
    → Verify OnRoomFound RoomInfo fields:
      HostAddress, Port, MaxPlayers, CurrentPlayers all correct

2.4 StopBroadcast_ShouldTriggerRoomLost
    → Host broadcasts → Listener finds room → Host stops broadcast
    → wait timeout → OnRoomLost fires

2.5 MultipleRooms_ShouldDiscoverAll
    → Two Hosts broadcast different room names
    → Listener should receive two OnRoomFound events

2.6 StopListening_ShouldStopReceivingUpdates
    → Listener finds room → StopListening() → Host broadcasts new room
    → OnRoomFound should NOT fire again
```

### 3. PlayerManagementTests (8 tests)

Tests LocalRoomManager player tracking.
Client joins via KCP loopback to 127.0.0.1.

```
3.1 HostCreateRoom_ShouldAddHostPlayer
    → CreateRoom() → wait InRoom → Players.Count == 1
    → Players[0].IsHost == true, ConnectionId == 0

3.2 ClientJoin_ShouldTriggerOnPlayerJoined
    → Host creates room → Client connects via loopback
    → wait OnPlayerJoined → event arg NexusPlayer.IsHost == false

3.3 ClientJoin_ShouldUpdatePlayersList
    → Host + Client joined → Players.Count == 2
    → Contains one IsHost==true and one IsHost==false

3.4 ClientJoin_ShouldUpdateCurrentPlayers
    → Host + Client joined → CurrentRoom.CurrentPlayers == 2

3.5 ClientLeave_ShouldTriggerOnPlayerLeft
    → Host + Client in room → Client disconnects
    → wait OnPlayerLeft → Players.Count back to 1

3.6 HostLeave_ShouldDisconnectAllPlayers
    → Host + Client in room → Host calls LeaveRoom()
    → Room closes, all connections terminated

3.7 MultipleClients_ShouldTrackAll
    → Host creates room → two Clients join sequentially
    → Players.Count == 3, each ConnectionId unique

3.8 PlayerInfo_ShouldHaveCorrectFields
    → Client joins → verify PlayerId non-empty,
      DisplayName has value, IsLocal flags correct
```

### 4. ReconnectTests (7 tests)

Tests ReconnectHandler exponential backoff.
Use short ReconnectBaseDelay (0.2s) and MaxReconnectAttempts (2-3).
Simulate disconnect via Transport.Stop() or NetworkClient.Disconnect().

```
4.1 Disconnect_ShouldTransitionToDisconnected
    → Host + Client in InRoom → force disconnect Client
    → Client session.CurrentState == SessionState.Disconnected

4.2 Disconnect_ShouldTriggerReconnectStarted
    → Client disconnects → wait OnReconnectStarted
    → session.CurrentState == SessionState.Reconnecting

4.3 Reconnect_ShouldSucceedWhenHostStillAlive
    → Host alive → Client disconnects → wait reconnect
    → OnReconnectSucceeded fires → session.CurrentState == InRoom

4.4 Reconnect_ShouldUseExponentialBackoff
    → Record timestamps of each reconnect attempt
    → Intervals should approximate baseDelay * 2^(attempt-1)

4.5 Reconnect_ShouldFailAfterMaxAttempts
    → Host shut down → Client disconnects
    → wait MaxReconnectAttempts exhausted
    → OnReconnectFailed fires → session.CurrentState == Idle

4.6 Reconnect_OnlyForClient_NotHost
    → Host mode → force stop Transport
    → ReconnectHandler should NOT activate → state goes to Idle

4.7 Disconnect_WhileNotInRoom_ShouldNotReconnect
    → In Idle state → stop Transport
    → ReconnectHandler should not intervene
```

## Total: 29 Test Cases

| Suite | Count | Focus |
|-------|-------|-------|
| SessionLifecycleTests | 8 | State machine transitions |
| RoomDiscoveryTests | 6 | UDP broadcast, timeout, multi-room |
| PlayerManagementTests | 8 | Player join/leave, tracking |
| ReconnectTests | 7 | Exponential backoff, failure modes |

## Implementation Notes

### Async Waiting Pattern
All tests use `[UnityTest]` returning `IEnumerator`.
Wait for state changes via:
```csharp
yield return TestHelpers.WaitForCondition(
    () => session.CurrentState == SessionState.InRoom,
    timeout: 5f
);
```

### Mirror Singleton Cleanup
Mirror's NetworkManager is a singleton. Each test must fully destroy it in TearDown
to prevent cross-test pollution. Order matters:
1. NetworkServer.Shutdown()
2. NetworkClient.Shutdown()
3. Object.DestroyImmediate(networkManagerGameObject)
4. Transport.active = null

### Client Simulation in Single Process
For player management and reconnect tests, simulate external client by:
- Creating a separate GameObject with its own NetworkManager is not possible (singleton)
- Instead, use Mirror's Host mode which already runs server + local client
- For "external client" scenarios, use NetworkClient.Connect("127.0.0.1") after Host starts
- Mirror supports multiple connections via KCP on loopback

### Test-Friendly Config
Create NexusConfig via ScriptableObject.CreateInstance<NexusConfig>() with:
- RoomTimeoutSeconds: 2 (faster timeout tests)
- ReconnectBaseDelay: 0.2 (faster reconnect tests)
- MaxReconnectAttempts: 2 (fewer wait cycles)
- DiscoveryPort: randomized per test run to avoid conflicts
