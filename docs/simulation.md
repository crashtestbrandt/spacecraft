# Simulation (deterministic ECS)

Everything under `scripts/Sim/` runs inside Klotho's deterministic step on every peer. Rules: `FP64` / `int`
only (no `float`, no `Godot.*`, no wall clock, no `System.Random`); collect-then-mutate when destroying or
creating entities during a filter walk. Klotho's `DeterminismAnalyzer` flags float use in these files at
build time.

## Units and arena

- 1 sim unit = 1 pixel at the 640×360 base resolution. Top 24 px is HUD; ships are clamped to
  `x ∈ [8, 632]`, `y ∈ [32, 352]` (`Rules.Clamp`).
- 30 Hz tick (`DeltaTimeMs = 33`); speeds are px/s and multiplied by `dt = DeltaTimeMs / 1000` in FP64.

## Components

| Component | Id | Fields | Owner |
| --- | --- | --- | --- |
| `TransformComponent` | 1 (Klotho) | `Position` (x, y used; z = 0), `Rotation` = facing in radians | ships, bullets |
| `OwnerComponent` | 2 (Klotho) | `OwnerId` = player id | ships, bullets |
| `SessionParticipantComponent` | 4 (Klotho) | `PlayerId` | one per active player, engine-written at start |
| `ShipComponent` | 100 | `Slot`, `Kind`, `Hp`, `HasTarget`, `TargetX/Y`, `FireCooldown` | ships |
| `BulletComponent` | 101 | `VelX/Y`, `Ttl`, `Damage` | bullets |
| `MatchStateComponent` | 102, singleton | `Ended`, `WinnerPlayerId` | one entity, created at init |

## Fixed armies (`Rules`)

- Every player gets the same army: slot 0 **flagship** + slots 1–8 **fighters** in two staggered rows ahead of
  it. Spawn anchors: P1 left, P2 right, P3 top, P4 bottom, all facing the center. Integer forward/side bases,
  so formations need no trig.
- Only the engine's `SessionParticipantComponent` slots get an army, not `MaxPlayers`, so a 2-player match on
  a 4-slot room seeds identical entities everywhere.

| Kind | Hp | Speed px/s | Range px | Cooldown ticks | Damage | Bullet px/s | Hit radius |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Flagship | 40 | 36 | 110 | 18 | 3 | 170 | 9 |
| Fighter | 6 | 80 | 72 | 14 | 1 | 220 | 5 |

Opening volleys are staggered by slot (`FireCooldown = base + slot`).

## Commands and events

- `MoveOrderCommand` (serializable id 100): `SelectionMask` (bit per slot), `TargetX/Y`.
  `IsContinuousInput = false`: an order is a discrete event, so prediction assumes "no new order".
- `GameOverEvent` (serializable id 101, `EventMode.Synced`): fired on the verified timeline only.

## Systems, in phase order

| Phase | System | Does |
| --- | --- | --- |
| PreUpdate | `OrderSystem` (`ICommandSystem`) | Applies a move order to the issuer's selected ships. Each ship's target = order target + (ship − selection centroid), so groups keep formation. |
| Update | `ShipMoveSystem` | Moves toward target at kind speed, faces travel direction, clears `HasTarget` on arrival. |
| Update | `TargetingSystem` | Cooldown countdown; a ready ship fires at the nearest enemy in range (O(n²), n ≤ 36) and faces it. Shots are collected, then bullet entities created. |
| Update | `BulletSystem` | Integrates bullets, expires on `Ttl`/arena exit, first enemy ship within hit radius takes `Damage`; bullet destroyed. |
| LateUpdate | `DeathSystem` | Destroys ships with `Hp ≤ 0`. |
| LateUpdate | `MatchEndSystem` | After tick 30, if ≤ 1 player has ships: set `MatchStateComponent.Ended`, raise `GameOverEvent` once. |
| LateUpdate | `EventSystem` (Klotho) | Publishes queued events. |

## Input path

`ArenaView` (view) → `OrderQueue.EnqueueMove` → `SpacecraftSimulationCallbacks.OnPollInput` dequeues **one**
order per tick and sends it as the local player's command → Klotho delivers it to every peer at the same tick →
`OrderSystem.OnCommand`.

## Not yet

- Ship–ship separation / collision (ships can overlap).
- Data assets: stats are constants in `Rules`; Klotho's `DataAssetRegistry` is built empty.
- Match end only shows a result; the session keeps running until Stop. Implementing `IMatchEndEvent` on
  `GameOverEvent` would hand it to Klotho's end-of-match ladder.
