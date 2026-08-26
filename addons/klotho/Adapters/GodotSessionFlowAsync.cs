// Task-based join helpers for Godot. Extension methods on KlothoSessionFlow that fold the two-step
// "connect then CreateForConnection" the samples otherwise inline. The returned Task completes when
// the handshake does — driven by the caller pumping transport.PollEvents each frame (e.g.
// GodotSessionDriver). Logger and DeviceIdProvider are pulled from flow directly (public accessors).
// onStarted forwards the in-flight KlothoConnection so its Update() can be pumped each frame,
// enforcing the client-side connect/reconnect timeout.
using System;
using System.Threading.Tasks;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Network;

namespace xpTURN.Klotho.Godot
{
    public static class GodotSessionFlowAsync
    {
        // P2P guest join — no room handshake. roomId is -1 (P2P has no rooms).
        public static async Task<KlothoSession> JoinP2PAsync(
            this KlothoSessionFlow flow,
            INetworkTransport transport,
            string host, int port,
            ISessionConfig sessionConfigSeed,
            Action<KlothoConnection> onStarted = null)
        {
            var result = await GodotConnectionAsync.ConnectAsync(
                transport, host, port,
                logger: flow.Logger,
                deviceIdProvider: flow.DeviceIdProvider,
                onStarted: onStarted,
                identityProvider: flow.IdentityProvider,
                claimedDisplayName: flow.ClaimedDisplayName);
            return flow.CreateForConnection(result, roomId: -1, sessionConfigSeed);
        }

        // ServerDriven guest join — sends a RoomHandshakeMessage pre-join, then CreateForConnection
        // with the same roomId.
        public static async Task<KlothoSession> JoinServerDrivenAsync(
            this KlothoSessionFlow flow,
            INetworkTransport transport,
            string host, int port, int roomId,
            ISessionConfig sessionConfigSeed,
            Action<KlothoConnection> onStarted = null)
        {
            var result = await GodotConnectionAsync.ConnectAsync(
                transport, host, port,
                logger: flow.Logger,
                deviceIdProvider: flow.DeviceIdProvider,
                preJoinMessage: new RoomHandshakeMessage { RoomId = roomId },
                onStarted: onStarted,
                identityProvider: flow.IdentityProvider,
                claimedDisplayName: flow.ClaimedDisplayName);
            return flow.CreateForConnection(result, roomId, sessionConfigSeed);
        }

        // Cold-start reconnect from persisted credentials. Connects to creds.RemoteAddress/Port and
        // restores the session slot (roomId from the credentials). DeviceId is already embedded in
        // the credentials — no separate deviceIdProvider needed.
        public static async Task<KlothoSession> ReconnectAsync(
            this KlothoSessionFlow flow,
            INetworkTransport transport,
            PersistedReconnectCredentials creds,
            ISessionConfig sessionConfigSeed,
            Action<KlothoConnection> onStarted = null)
        {
            var result = await GodotConnectionAsync.ReconnectAsync(
                transport, creds,
                logger: flow.Logger,
                onStarted: onStarted);
            return flow.CreateForConnection(result, creds.RoomId, sessionConfigSeed);
        }
    }
}
