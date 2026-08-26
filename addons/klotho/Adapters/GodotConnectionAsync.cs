using System;
using System.Threading.Tasks;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Logging;
using xpTURN.Klotho.Network;

namespace xpTURN.Klotho.Godot
{
    // Wraps the callback-based KlothoConnection.Connect/Reconnect (core API) with a standard Task
    // (no UniTask / GodotTask dependency).
    //
    // The handshake is driven by transport.PollEvents() pumped each frame by the caller
    // (GodotSessionDriver._Process or a headless tick loop), so no internal yield loop is needed.
    // onStarted reports the in-flight KlothoConnection so the caller (driver) can pump its Update()
    // each frame — that enforces the client-side connect/reconnect timeout (Update is the watchdog).
    public static class GodotConnectionAsync
    {
        public static Task<ConnectionResult> ConnectAsync(
            INetworkTransport transport, string host, int port,
            IKLogger logger = null,
            NetworkMessageBase preJoinMessage = null,
            IDeviceIdProvider deviceIdProvider = null,
            Action<KlothoConnection> onStarted = null,
            IPlayerIdentityProvider identityProvider = null,
            string claimedDisplayName = null)
        {
            var tcs = new TaskCompletionSource<ConnectionResult>();
            var connection = KlothoConnection.Connect(
                transport, host, port,
                onCompleted: result => tcs.TrySetResult(result),
                onFailed: ex => tcs.TrySetException(ex),
                logger: logger,
                preJoinMessage: preJoinMessage,
                deviceIdProvider: deviceIdProvider,
                identityProvider: identityProvider,
                claimedDisplayName: claimedDisplayName);
            onStarted?.Invoke(connection);
            return tcs.Task;
        }

        // Cold-start reconnect from persisted credentials. Connects to creds.RemoteAddress/Port and
        // restores the session slot. Mirrors ConnectAsync (handshake driven by PollEvents).
        public static Task<ConnectionResult> ReconnectAsync(
            INetworkTransport transport, PersistedReconnectCredentials creds,
            IKLogger logger = null,
            Action<KlothoConnection> onStarted = null)
        {
            var tcs = new TaskCompletionSource<ConnectionResult>();
            var connection = KlothoConnection.Reconnect(
                transport, creds,
                onCompleted: result => tcs.TrySetResult(result),
                onFailed: ex => tcs.TrySetException(ex),
                logger: logger);
            onStarted?.Invoke(connection);
            return tcs.Task;
        }
    }
}
