# View layer (2D render + RTS input)

`scripts/View/`. Nothing here is deterministic; it reads engine frames and writes to `OrderQueue`.

## Why not Klotho's view layer

Klotho's `EntityViewNode` / `EntityViewUpdaterNode` are `Node3D`-based. This is a 2D project, so `ArenaView`
does the same two jobs in 2D and stays small.

## `ArenaView : Node2D`

| Hook | Does |
| --- | --- |
| `Initialize(engine, localPlayerId, orders)` | Subscribes to `engine.OnTickExecuted`. |
| `OnTickExecuted` (per executed tick) | **Reconcile**: walks `PredictedFrame` live entities (`GetAllLiveEntities`); one `ShipView` per `ShipComponent` entity and one `BulletView` per `BulletComponent` entity, keyed by entity index and checked by version. Stale views are freed; a ship's disappearance spawns an `ExplosionView` at its last drawn position. Pushes `Hp` and selection state into ship views. |
| `_Process` (per rendered frame) | **Interpolate**: position/rotation = lerp(`PredictedPreviousFrame`, `PredictedFrame`, `RenderClock.PredictedAlpha`), rounded to whole pixels. |
| `_UnhandledInput` | Left drag = box-select own ships (a click picks the nearest within 12 px); `Ctrl+A` = select all; right click = enqueue a move order for the selection. |
| `_Draw` | Starfield (fixed-seed LCG, identical on every peer), drag rectangle, fading order marker. |

Selection is view-local, stored as a bitmask over army slots (stable across entity respawn/rollback).

## Sprites

- `Palette`: per-player colors (blue, red, green, yellow) and ASCII pixel maps (`#` body, `+` highlight,
  `o` cockpit). Fighter 9×9, flagship 13×11, authored facing up.
- `ShipView._Draw` paints one `DrawRect` per pixel; rotation snaps to 16 directions (`SetFacing`) for a
  sprite-sheet look. HP bar appears when damaged, selection ring when selected — both drawn level (inverse
  rotation transform).
- `BulletView`: 4×1 streak with a white tip. `ExplosionView`: 8-point pixel burst, frees itself after 0.35 s.

## Presentation settings (`project.godot`)

- Base viewport 640×360, window override 1280×720, `canvas_items` stretch with `integer` scale → crisp 2×
  pixels; nearest texture filter; dark navy clear color.
- Launch with `--resolution 640x360` for 1:1 windows (what `just demo` does so four fit on one screen).

## HUD (`Hud : Control`)

- Built in code. Lobby panel: address, port, Host / Join / Ready / Stop. Top bar: status (phase or tick +
  player count) and per-player live ship counts (`*` marks the local player). Center result label on
  `GameOverEvent` (VICTORY / P*n* WINS / DRAW).
- `Main` owns the lobby state machine (`Idle → Connecting → InSession → Playing`) and drives `SetLobbyMode`.
