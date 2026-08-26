// Cold-start auto-reconnect helper (engine-free).
using System;
using System.Threading;
using xpTURN.Klotho.Network;

namespace xpTURN.Klotho.Godot
{
    public static class GodotAutoReconnect
    {
        // Cold-start reconnect from persisted credentials.
        // Returns true when reconnectFn was invoked (credentials valid).
        // Returns false when there were no credentials, or they failed validity / version check.
        // Caller owns: game-policy prefilter, UI state transition, CancellationTokenSource lifetime.
        public static bool TryStart(
            IReconnectCredentialsStore store,
            long nowUnixMs,
            string currentAppVersion,
            Action<CancellationToken> reconnectFn,
            CancellationToken ct)
        {
            var creds = store.Load();
            if (creds == null) return false;
            if (!store.IsValid(creds, nowUnixMs, currentAppVersion))
            {
                store.Clear();
                return false;
            }
            reconnectFn(ct);
            return true;
        }
    }
}
