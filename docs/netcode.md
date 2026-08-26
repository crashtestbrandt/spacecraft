# Netcode: Klotho over ENet

## What it is

- **Klotho** ([xpTURN/Klotho](https://github.com/xpTURN/Klotho), v0.9.2, Apache-2.0) provides the deterministic
  lockstep/rollback engine. Peers exchange **inputs only**; every peer simulates the same ticks and compares
  state hashes. Vendored as `addons/klotho/` (prebuilt core DLL + Godot adapter source + source generator),
  imported by one line in `Spacecraft.csproj`.
- **ENet** (Godot's built-in `ENetConnection`) is the wire. Klotho ships LiteNetLib by default; this project
  swaps it for `scripts/Net/GodotEnetTransport.cs`, an `INetworkTransport` implementation. The LiteNetLib
  NuGet package stays referenced because the core DLL links against it, but it is never instantiated.
- Mode is **P2P**: the host relays, every peer holds equal simulation authority. No dedicated server.

## Transport contract (`GodotEnetTransport`)

| Klotho `DeliveryMethod` | ENet channel | ENet flags | Notes |
| --- | --- | --- | --- |
| `ReliableOrdered` | 0 | `FlagReliable` | handshake, roster, full-state resync |
| `Sequenced` | 1 | none | ENet unreliable-sequenced (drops stale packets) |
| `Unreliable` | 1 | `FlagUnsequenced` | |
| `Reliable` | 2 | `FlagReliable` | ENet has no reliable-unordered; own channel so it never blocks channel 0 |

- **One channel per method** satisfies Klotho's rule that `Send` and `Broadcast` with the same method share
  one per-peer ordered stream (late-join depends on it).
- **Peer ids**: host hands guests the smallest free id from 0; host `LocalPeerId` = 0. Guest: server peer = 0,
  `LocalPeerId` = 0. Mirrors LiteNetLib, which Klotho was written against.
- **Disconnect payload**: Klotho attaches one reject-reason byte to `DisconnectPeer`. It travels in ENet's
  32-bit disconnect data as `0x100 | byte`; data without bit 8 means "no payload" (`LastDisconnectPayload = -1`).
  Uses `PeerDisconnectLater` so a queued reject message is flushed first.
- **Disconnect reasons**: ENet reports none. Mapping: local request → `LocalDisconnect`; never connected →
  `NetworkFailure`; payload present → `ConnectionRejected`; otherwise `RemoteDisconnect` (covers timeouts too).
- **Threading**: main thread only. `GodotSessionDriver._Process` pumps `PollEvents`; Klotho's async joins are
  `TaskCompletionSource`-driven from that pump, no background threads.
- `PollEvents` drains `Service(0)` until `EventType.None`; a `Receive` event pops every queued packet on that
  peer. Packets are handed to Klotho as the `byte[]` ENet returns (no pooling).
- Hostnames resolve through `IP.ResolveHostname`; IP literals pass straight through. Host binds `*`.

## Simulation config (`scripts/Main.cs`)

| Field | Value | Why |
| --- | --- | --- |
| `TickIntervalMs` | 33 | 30 Hz lockstep |
| `InputDelayTicks` | 3 | ~100 ms of jitter absorption before a rollback is needed |
| `MaxRollbackTicks` | 30 | 1 s window |
| `SyncCheckInterval` | 15 | must be ≤ `MaxRollbackTicks / 2` (engine clamps otherwise) |
| `UsePrediction` | true | predicted chain runs ahead on guessed input, rolls back on mismatch |
| `QuorumMissDropTicks` | 20 | P2P presumed-drop watchdog |
| `MaxEntities` | 256 | 4 armies × 9 ships + bullets |

Session config: `MaxPlayers 4`, `MinPlayers` from `players=N` (default 2), `AllowLateJoin false` (armies are
seeded at tick 0 only), 3 s countdown.

## Session flow

1. Host: `KlothoSessionFlow.StartHostAndListen` → transport `Listen("*", port)`.
2. Guest: `JoinP2PAsync` → transport `Connect` → Klotho handshake + clock sync (5 sync requests).
3. Each peer calls `SetReady(true)`; when `PlayerCount ≥ MinPlayers` and all are ready the host broadcasts
   `GameStart` (seed, start time, roster) → countdown → `Playing`.
4. Tick 0: every peer runs `OnInitializeWorld`, spawning armies for the engine-written
   `SessionParticipantComponent` slots. Host broadcasts the initial full state; hashes must match.
5. Per tick: `OnPollInput` sends at most one `MoveOrderCommand`; no order → Klotho injects an empty command.

## Known gaps / next steps

- Steam transport: implement `INetworkTransport` over Steam Networking Sockets beside this one; the game code
  never touches the transport directly.
- Reconnect/late-join are off. Both are Klotho features; late-join would need `OnPlayerJoinedWorld` to seed an army.
- `GodotEnetTransport` cannot tell a clean remote close from a timeout (both `RemoteDisconnect`).
- The benign `[FullStateResync] … not in Requested state, ignoring` warning on guests is documented by Klotho's
  own sample (a late full-state reply, dropped).
