// Node that drives a KlothoSession through Godot's _Process loop. It pumps the bound transport — even before a session is attached, so the
// connect handshake (driven by transport.PollEvents) can complete — then, once a session is attached,
// pumps the session each frame with pre/post hooks. The view interpolation is self-driven by
// EntityViewUpdaterNode._Process, so the driver knows nothing about views.
//
// dt source is selectable via DtSource (default WallClock): WallClock measures real elapsed time
// (DateTimeOffset.UtcNow, engine-independent), EngineFrame uses Godot's _Process delta. _Process computes it;
// Tick(dt) is the explicit-drive entry for deterministic-step or headless tick loops.
//
// An in-flight KlothoConnection (registered via TrackConnection) is pumped each frame so its Update()
// enforces the client-side connect/reconnect timeout. The same in-flight state also gates the
// transport-disconnect routing (a drop mid-handshake is owned by the connect Task, not treated as idle),
// replacing the flow.IsConnecting check the driver would otherwise need — so reconnect routing needs no core change.
using System;
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Network;

namespace xpTURN.Klotho.Godot
{
    // Source of the per-frame dt that drives the session. WallClock measures real elapsed time
    // (engine-independent); EngineFrame uses Godot's _Process delta (honors engine frame pacing).
    public enum SessionDtSource { WallClock, EngineFrame }

    public partial class GodotSessionDriver : Node
    {
        public KlothoSession Session { get; private set; }

        // dt source for the session step. Default: WallClock (engine-independent real time).
        public SessionDtSource DtSource { get; set; } = SessionDtSource.WallClock;

        // Steady-state hooks (fired with the same dt that drives the session). PreSessionUpdate is the
        // place to capture input. Exceptions propagate.
        public event Action<KlothoSession, float> PreSessionUpdate;
        public event Action<KlothoSession, float> PostSessionUpdate;
        // Lifecycle transition — fired before the session stops (game-initiated or framework-internal).
        public event Action<KlothoSession> Stopping;
        // Transport dropped while no session and no in-flight connect (genuinely idle) — game resets UI.
        public event Action<DisconnectReason> OnIdleDisconnected;

        private INetworkTransport _transport;
        private KlothoConnection _inflight;   // in-flight connect/reconnect (pumped for timeout, gates routing)
        private long _lastMs;
        private bool _stopping;

        // Bind the main transport once (in _Ready) so it is pumped while idle — the connect handshake
        // completes via PollEvents before any session exists. Also routes transport disconnects.
        public void BindTransport(INetworkTransport transport)
        {
            if (_transport != null) _transport.OnDisconnected -= OnTransportDisconnected;
            _transport = transport;
            if (_transport != null) _transport.OnDisconnected += OnTransportDisconnected;
        }

        // Register the in-flight connection from GodotConnectionAsync/GodotSessionFlowAsync (onStarted)
        // so its Update() is pumped each frame (timeout watchdog) until it completes.
        public void TrackConnection(KlothoConnection connection) => _inflight = connection;

        // Attach a session created by an entry method (StartHostAndListen / CreateForConnection).
        public void Attach(KlothoSession session)
        {
            Session = session;
            _lastMs = 0;
        }

        // Stop intent: fire Stopping, then Stop the session. try/finally guarantees the session stops and
        // is detached even if a Stopping subscriber throws.
        public void DetachAndStop(bool keepReconnectCredentials = false, bool saveReplay = true)
        {
            if (_stopping) return;
            var s = Session;
            if (s == null) return;

            _stopping = true;
            try { Stopping?.Invoke(s); }
            finally { s.Stop(keepReconnectCredentials, saveReplay); Session = null; _stopping = false; }
        }

        public override void _Process(double delta)
            => Tick(DtSource == SessionDtSource.EngineFrame ? (float)delta : StepDt());

        // Drive one step. Public so headless/deterministic loops can pump with an explicit dt.
        public void Tick(float dt)
        {
            _transport?.PollEvents();   // idle pump — lets the connect/reconnect handshake complete
            PumpInflight();             // timeout watchdog for an in-flight connect/reconnect

            var s = Session;
            if (s == null) return;

            PreSessionUpdate?.Invoke(s, dt);
            if (s.IsStopped) { SelfDetach(s); return; }

            s.Update(dt);
            if (s.IsStopped) { SelfDetach(s); return; }

            PostSessionUpdate?.Invoke(s, dt);
        }

        private void PumpInflight()
        {
            if (_inflight == null) return;
            if (_inflight.IsCompleted) { _inflight = null; return; }
            _inflight.Update();
        }

        // Transport disconnect routing. The in-flight gate
        // replaces the flow.IsConnecting check.
        private void OnTransportDisconnected(DisconnectReason reason)
        {
            if (_inflight != null && !_inflight.IsCompleted) return;   // handshake — connect Task owns it
            if (Session == null) { OnIdleDisconnected?.Invoke(reason); return; }

            // Spectator session (NetworkService == null) uses its own transport — main-transport drop is
            // irrelevant. Stop only a non-Playing host/guest session; Playing → no-op (NetworkService auto-reconnects).
            var ns = Session.NetworkService;
            if (ns != null && ns.Phase != SessionPhase.Playing)
                Session.RequestClientShutdown();
        }

        // The session stopped without DetachAndStop (framework-internal stop). Fire Stopping once and
        // detach so this does not re-fire every frame.
        private void SelfDetach(KlothoSession s)
        {
            try { Stopping?.Invoke(s); }
            finally { Session = null; }
        }

        public override void _ExitTree()
        {
            if (_transport != null) _transport.OnDisconnected -= OnTransportDisconnected;
            DetachAndStop(keepReconnectCredentials: true, saveReplay: false);
        }

        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private float StepDt()
        {
            long now = NowMs();
            float dt = (_lastMs > 0) ? (now - _lastMs) * 0.001f : 0f;
            _lastMs = now;
            return dt;
        }
    }
}
