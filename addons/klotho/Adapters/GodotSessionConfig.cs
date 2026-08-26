// SessionConfig authored from the Godot editor (Resource implementing ISessionConfig).
// Implements ISessionConfig so it can be injected into KlothoFlowSetup / StartHostAndListen directly.
// [GlobalClass] surfaces it in the editor's "New Resource" menu; [Export] fields persist to .tres.
// Default [Export] values mirror the engine's built-in session config.
using global::Godot;
using xpTURN.Klotho.Core;

namespace xpTURN.Klotho.Godot
{
    [GlobalClass]
    public partial class GodotSessionConfig : Resource, ISessionConfig
    {
        // Determinism
        [Export] public int RandomSeed { get; set; } = 0;

        // Membership
        [Export] public int MaxPlayers { get; set; } = 4;
        [Export] public int MinPlayers { get; set; } = 2;
        [Export] public int MaxSpectators { get; set; } = 0;

        // LateJoin / Reconnect Policy
        [Export] public bool AllowLateJoin { get; set; } = true;
        [Export] public int LateJoinDelayTicks { get; set; } = 10;
        [Export] public int ReconnectTimeoutMs { get; set; } = 60000;
        [Export] public int ValidationTimeoutMs { get; set; } = 5000;
        [Export] public int ReconnectMaxRetries { get; set; } = 3;

        // LateJoin / Reconnect Tuning
        [Export] public int LateJoinDelaySafety { get; set; } = 2;
        [Export] public int RttSanityMaxMs { get; set; } = 240;

        // Chain-Stall Watchdog
        [Export] public int MinStallAbortTicks { get; set; } = 600;

        // Match Start Countdown
        [Export] public int CountdownDurationMs { get; set; } = 3000;

        // Match End Grace
        [Export] public int AbortGraceMs { get; set; } = 1500;
        [Export] public EndGracePolicy EndGracePolicy { get; set; } = EndGracePolicy.Continue;
        [Export] public int EndGraceMs { get; set; } = 5000;
        [Export] public int ClientShutdownGraceMs { get; set; } = 4500;
    }
}
