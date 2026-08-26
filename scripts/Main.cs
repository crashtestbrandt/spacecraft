// Bootstrap: one process = one peer. Builds the Klotho flow over the ENet transport, drives the
// session with GodotSessionDriver, and wires the lobby/HUD and the 2D arena view.
//
// CLI (after Godot's own `--`):
//   host | join            auto host / join instead of waiting for the lobby buttons
//   ip=<addr> port=<n>     override the lobby fields (default 127.0.0.1:7777)
//   players=<n>            host: MinPlayers/MaxPlayers for the room (2..4, default 2)
//   autotest               auto-ready, verify the armies spawned on the verified timeline, then quit
//                          with exit code 0/1 (used by `just demo-test`, works headless)
//   autoplay               after the countdown, order the whole army to the arena center (a scripted
//                          engagement, so the demo plays itself and exercises commands over the wire)
//   screenshot=<path>      save the rendered frame at tick 150 to <path> (PNG); windowed runs only
using System;
using System.Threading.Tasks;
using global::Godot;
using Spacecraft.Net;
using Spacecraft.Sim;
using Spacecraft.View;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace Spacecraft
{
    public partial class Main : Node2D, IKlothoSessionObserver
    {
        const int  DefaultPort = 7777;
        const int  VerifyTick  = 120;

        IKLogger            _logger;
        GodotEnetTransport  _transport;
        KlothoSessionFlow   _flow;
        GodotSessionDriver  _driver;
        KlothoSession       _session;
        SimulationConfig    _simCfg;
        SessionConfig       _sesCfg;
        readonly OrderQueue _orders = new();

        ArenaView _arena;
        Hud       _hud;

        Task<KlothoSession> _joinTask;
        bool _readySent;

        // CLI-driven automation.
        int  _autoMode;        // 0 = lobby, 1 = host, 2 = join
        bool _autoTest;
        bool _autoPlay;
        bool _autoPlayed;
        bool _verified;
        string _screenshotPath;
        int  _players = 2;
        const int AutoPlayTick   = 30;
        const int ScreenshotTick = 150;

        public override void _Ready()
        {
            WarmupRegistry.RunAll();

            _logger    = GodotKlothoLogger.CreateDefault(filePrefix: "Spacecraft", categoryName: "Spacecraft", timestampFormat: "HH:mm:ss.fff");
            _transport = new GodotEnetTransport(_logger);
            _arena     = GetNode<ArenaView>("Arena");
            _hud       = GetNode<Hud>("UI/Hud");

            ParseArgs();

            _simCfg = new SimulationConfig
            {
                Mode = NetworkMode.P2P,
                TickIntervalMs = 33,          // 30 Hz lockstep
                InputDelayTicks = 3,
                MaxRollbackTicks = 30,        // 1 s of rollback window; SyncCheckInterval must fit inside it
                SyncCheckInterval = 15,       // must be <= MaxRollbackTicks/2
                UsePrediction = true,
                QuorumMissDropTicks = 20,
                EnableErrorCorrection = false,
                InterpolationDelayTicks = 1,
                MaxEntities = 256,
            };
            _sesCfg = new SessionConfig
            {
                MaxPlayers = Rules.MaxPlayers,
                MinPlayers = _players,
                AllowLateJoin = false,        // fixed armies are seeded at tick 0 only
                CountdownDurationMs = 3000,
            };

            IDataAssetRegistryBuilder registryBuilder = new DataAssetRegistry();
            var registry = registryBuilder.Build();   // no data assets yet; rules are constants

            _flow = new KlothoSessionFlow(
                new KlothoFlowSetupBuilder((sim, ses) =>
                        new SessionCallbacks(new SpacecraftSimulationCallbacks(_orders), new SpacecraftViewCallbacks(this)))
                    .WithLogger(_logger)
                    .WithTransport(_transport)
                    .WithAssetRegistry(registry)
                    .WithGodotDefaults()
                    .WithLifecycleObserver(this)
                    .Build());

            _driver = new GodotSessionDriver();
            AddChild(_driver);
            _driver.BindTransport(_transport);

            _hud.HostClicked  += Host;
            _hud.JoinClicked  += Join;
            _hud.ReadyClicked += SendReady;
            _hud.StopClicked  += Stop;

            if (_autoMode == 1) Host();
            else if (_autoMode == 2) Join();
        }

        void ParseArgs()
        {
            string ip = null, port = null;
            foreach (var a in OS.GetCmdlineUserArgs())
            {
                if (a == "host") _autoMode = 1;
                else if (a == "join") _autoMode = 2;
                else if (a == "autotest") _autoTest = true;
                else if (a == "autoplay") _autoPlay = true;
                else if (a.StartsWith("screenshot=", StringComparison.Ordinal)) _screenshotPath = a.Substring(11);
                else if (a.StartsWith("ip=", StringComparison.Ordinal)) ip = a.Substring(3);
                else if (a.StartsWith("port=", StringComparison.Ordinal)) port = a.Substring(5);
                else if (a.StartsWith("players=", StringComparison.Ordinal) && int.TryParse(a.Substring(8), out int n))
                    _players = Math.Clamp(n, 2, Rules.MaxPlayers);
            }
            if (ip != null) _hud.SetAddress(ip);
            if (port != null && int.TryParse(port, out int p)) _hud.SetPort(p);
        }

        // ── lobby actions ──

        void Host()
        {
            if (_session != null) return;
            _hud.SetLobbyMode(Hud.LobbyMode.Connecting);
            _hud.SetStatus("Hosting…");
            var session = _flow.StartHostAndListen(_simCfg, _sesCfg, "Spacecraft", "*", _hud.Port);
            if (session == null)
            {
                _hud.SetStatus($"Host failed (port {_hud.Port} in use?)");
                _hud.SetLobbyMode(Hud.LobbyMode.Idle);
            }
        }

        void Join()
        {
            if (_session != null || _joinTask != null) return;
            _hud.SetLobbyMode(Hud.LobbyMode.Connecting);
            _hud.SetStatus($"Joining {_hud.Address}:{_hud.Port}…");
            _joinTask = _flow.JoinP2PAsync(_transport, _hud.Address, _hud.Port, _sesCfg);
        }

        void SendReady()
        {
            if (_session == null || _readySent) return;
            _readySent = true;
            _hud.SetReadyPressed(true);
            _session.SetReady(true);
        }

        void Stop()
        {
            if (_session == null) return;
            _driver.DetachAndStop();
        }

        // ── per frame ──

        public override void _Process(double delta)
        {
            if (_joinTask != null)
            {
                if (_joinTask.IsFaulted)
                {
                    _logger.KError($"[Main] join failed: {_joinTask.Exception?.GetBaseException().Message}");
                    _hud.SetStatus("Join failed (is the host running?)");
                    _hud.SetLobbyMode(Hud.LobbyMode.Idle);
                    _joinTask = null;
                }
                else if (_joinTask.IsCompleted)
                {
                    _joinTask = null;   // OnSessionCreated already attached the session
                }
            }

            if (_session == null) return;

            bool running = _session.State == KlothoState.Running;
            _hud.SetStatus(running
                ? $"tick {_session.Engine.CurrentTick}   players {_session.PlayerCount}"
                : $"{_session.Phase}   players {_session.PlayerCount}/{_sesCfg.MinPlayers}+");
            if (running) _hud.SetArmies(_arena, _session.LocalPlayerId);

            if (running && _autoPlay && !_autoPlayed && _session.Engine.CurrentTick >= AutoPlayTick)
            {
                _autoPlayed = true;
                const int allSlots = (1 << Rules.ArmySize) - 1;
                _orders.EnqueueMove(allSlots, FP64.FromInt(Rules.ArenaWidth / 2), FP64.FromInt((Rules.ArenaHeight + 24) / 2));
            }
            if (running && _screenshotPath != null && _session.Engine.CurrentTick >= ScreenshotTick)
            {
                var img = GetViewport().GetTexture().GetImage();
                var err = img.SavePng(_screenshotPath);
                _logger.KInformation($"[Main] screenshot -> {_screenshotPath} ({err})");
                _screenshotPath = null;
            }

            if (_autoTest) AutoTestStep(running);
        }

        void AutoTestStep(bool running)
        {
            if (!_readySent && _session.Phase == SessionPhase.Synchronized) SendReady();
            int verifyTick = _autoPlay ? VerifyTick * 2 : VerifyTick;   // autoplay: let the armies meet first
            if (_verified || !running || _session.Engine.CurrentTick < verifyTick) return;
            _verified = true;

            var frame = _session.Engine.VerifiedFrame.Frame;
            int ships = 0;
            if (frame != null)
            {
                var f = frame.Filter<ShipComponent>();
                while (f.Next(out _)) ships++;
            }
            // Without autoplay nobody moves or fires, so every army must be intact. With autoplay the armies
            // have engaged; the check is then that the view tracks the verified frame and someone survived.
            int expected = _autoPlay ? -1 : _session.PlayerCount * Rules.ArmySize;
            bool ok = _autoPlay
                ? ships > 0 && ships == _arena.LiveShipCount
                : ships == expected && _arena.LiveShipCount == expected;
            _logger.KInformation($"[Main] autotest tick={_session.Engine.CurrentTick} players={_session.PlayerCount} verifiedShips={ships} viewShips={_arena.LiveShipCount} expected={expected}");
            if (ok) _logger.KInformation($"=== SPACECRAFT DEMO OK ===");
            else    _logger.KError($"=== SPACECRAFT DEMO FAILED ===");
            GD.Print(ok ? "=== SPACECRAFT DEMO OK ===" : $"=== SPACECRAFT DEMO FAILED (ships={ships} view={_arena.LiveShipCount} expected={expected}) ===");
            GetTree().Quit(ok ? 0 : 1);
        }

        // ── IKlothoSessionObserver ──

        public void OnSessionCreated(KlothoSession session, SessionEntryKind kind)
        {
            _session = session;
            _readySent = false;
            _hud.SetReadyPressed(false);
            _hud.SetLobbyMode(Hud.LobbyMode.InSession);
            _driver.Attach(session);
        }

        public void OnGameStart()
        {
            _hud.SetLobbyMode(Hud.LobbyMode.Playing);
            _arena.Initialize(_session.Engine, _session.LocalPlayerId, _orders);
        }

        public void OnStateChanged(KlothoState state)
        {
            if (state == KlothoState.Running && _session != null && _arena.LiveShipCount == 0)
                OnGameStart();
        }

        public void OnSessionStopping()
        {
            _arena.Cleanup();
            _orders.Clear();
        }

        public void OnSessionStopped()
        {
            _session = null;
            _readySent = false;
            _hud.SetReadyPressed(false);
            _hud.SetLobbyMode(Hud.LobbyMode.Idle);
            _hud.SetStatus("Idle");
        }

        public void OnIdleDisconnected(DisconnectReason reason)
        {
            _hud.SetLobbyMode(Hud.LobbyMode.Idle);
            _hud.SetStatus($"Disconnected ({reason})");
        }

        public void OnMatchAborted(AbortReason reason) => _hud.SetStatus($"Aborted: {reason}");

        // Called by the view callbacks on the synced GameOverEvent.
        public void ShowGameOver(int winnerPlayerId)
        {
            if (_session == null) return;
            _hud.ShowResult(winnerPlayerId, _session.LocalPlayerId);
        }

        public override void _ExitTree()
        {
            if (_session != null) _driver?.DetachAndStop();
            _arena?.Cleanup();
        }
    }

    // Non-deterministic side: forwards the synced game-over event to the HUD.
    public sealed class SpacecraftViewCallbacks : IViewCallbacks
    {
        readonly Main _main;
        IKlothoEngine _engine;
        Action<int, SimulationEvent> _onSynced;

        public SpacecraftViewCallbacks(Main main) { _main = main; }

        public void OnGameStart(IKlothoEngine engine)
        {
            _engine = engine;
            _onSynced = (tick, evt) => { if (evt is GameOverEvent go) _main.ShowGameOver(go.WinnerPlayerId); };
            engine.OnSyncedEvent += _onSynced;
        }

        public void OnTickExecuted(int tick) { }
        public void OnLateJoinActivated(IKlothoEngine engine) { }
    }
}
