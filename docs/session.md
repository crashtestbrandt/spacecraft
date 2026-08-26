# Session, lobby and local multi-instance testing

## One process = one peer

`scripts/Main.cs` bootstraps a single Klotho session per process. To play locally, run several processes;
each is a host or a guest. Nothing shares memory between them, so a loopback match goes through the real
ENet socket path.

## Lobby (`Hud`)

| Button | Effect |
| --- | --- |
| Host | `StartHostAndListen` on `*:<port>`; the room takes `players=N` (default 2) as `MinPlayers`, `MaxPlayers` 4 |
| Join | `JoinP2PAsync(address, port)`; failure returns to Idle with a status line |
| Ready | `SetReady(true)`; the match starts when everyone in the room is ready and the room has ≥ `MinPlayers` |
| Stop | `GodotSessionDriver.DetachAndStop()`; the socket stays bound for reuse |

Lifecycle is observed through `IKlothoSessionObserver` on `Main`: `OnSessionCreated` attaches the driver,
`OnStateChanged(Running)` initializes the arena view, `OnSessionStopping` tears the view down,
`OnSessionStopped` / `OnIdleDisconnected` return to the lobby.

## CLI flags (after Godot's `--`)

| Flag | Meaning |
| --- | --- |
| `host` / `join` | skip the buttons |
| `ip=<addr>` `port=<n>` | prefill the lobby fields (default `127.0.0.1:7777`) |
| `players=<n>` | host only: `MinPlayers` (2..4) |
| `autotest` | auto-ready when `Synchronized`; at tick 120 verify the verified frame holds `players × 9` ships and the view matches; print `=== SPACECRAFT DEMO OK ===` and exit 0, else 1. Works headless. |
| `autoplay` | at tick 30 order the whole army to the arena center (a scripted engagement). With `autotest`, verification moves to tick 240 and checks view == verified frame and survivors > 0 |
| `screenshot=<path>` | save the rendered frame at tick 150 (windowed runs) |

## `just` recipes

| Recipe | Does |
| --- | --- |
| `just build` | `dotnet build` (needs the .NET 8 SDK on PATH) |
| `just demo [N] [-AutoPlay]` | `tools/demo.ps1`: N tiled 640×360 windows on `127.0.0.1:7777`, host first. Click Ready in each, or `-AutoPlay` to ready + script the charge |
| `just demo-test [N] [-AutoPlay]` | `tools/demo-test.ps1`: N headless processes on port 7791 with `autotest`; waits for all, exits nonzero if any peer fails or times out (90 s). Logs in `%TEMP%\spacecraft-demo-test\` |

Verified in this state: `demo-test 2`, `demo-test 4`, `demo-test 2 -AutoPlay`, `demo-test 4 -AutoPlay` all
pass (every peer reports the same verified ship count; no desync).

## Logs

`GodotKlothoLogger.CreateDefault` writes Klotho's structured log to Godot's console and to
`%APPDATA%\Godot\app_userdata\Spacecraft\logs\` (rolling files, prefix `Spacecraft`).
